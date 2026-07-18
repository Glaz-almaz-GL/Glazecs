using Glazecs.Modules.ASR.Abstractions.Interfaces;
using Glazecs.Modules.ASR.Resources.Languages;
using Glazecs.Modules.ASR.Whisper.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Text;

namespace Glazecs.Modules.ASR.Components.Pages;

public partial class SpeechRecognitionPage(
    ILogger<SpeechRecognitionPage>? logger,
    ISnackbar snackbar,
    IStringLocalizer<AsrResources> localizer) : ComponentBase, IDisposable
{
    #region Injection & Fields

    private readonly ILogger<SpeechRecognitionPage>? _logger = logger;
    private readonly ISnackbar _snackbar = snackbar;
    private readonly IStringLocalizer<AsrResources> _localizer = localizer;

    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _downloadCts;
    private bool _disposed;

    private readonly Dictionary<DevicePlatform, IEnumerable<string>> _audioFileTypes = new()
    {
        { DevicePlatform.iOS,     [ "public.audio", "public.mp3", "public.wav" ] },
        { DevicePlatform.Android, [ "audio/*", "application/ogg" ] },
        { DevicePlatform.WinUI,   [ ".mp3", ".wav", ".m4a", ".flac", ".ogg" ] },
        { DevicePlatform.Tizen,   [ "audio/*" ] },
        { DevicePlatform.macOS,   [ "public.audio", "public.mp3", "public.wav" ] }
    };

    #endregion

    #region State

    private string _filePath = string.Empty;
    private string _resultText = string.Empty;
    private bool _isProcessing;
    private bool _isModelReady;
    private bool _isDownloadingModel;
    private double _downloadProgress;
    private int _processedChunksCount;
    private ISpeechRecognitionService? _selectedService;

    #endregion

    #region Lifecycle

    protected override async Task OnInitializedAsync()
    {
        // Проверяем готовность модели при выборе сервиса
        if (_selectedService != null)
        {
            await CheckModelStatusAsync();
        }
    }

    #endregion

    #region Model Management

    private async Task OnServiceChanged(ISpeechRecognitionService? service)
    {
        _selectedService = service;
        await CheckModelStatusAsync();
        StateHasChanged();
    }

    private async Task CheckModelStatusAsync()
    {
        if (_selectedService == null)
        {
            _isModelReady = false;
            return;
        }

        try
        {
            _isModelReady = await _selectedService.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при проверке статуса модели");
            _isModelReady = false;
        }
    }

    private async Task DownloadModelAsync()
    {
        if (_selectedService == null)
        {
            _snackbar.Add(_localizer["ASR_Error_No_Service"], Severity.Warning);
            return;
        }

        _isDownloadingModel = true;
        _downloadProgress = 0;
        _downloadCts = new CancellationTokenSource();

        try
        {
            Progress<double> progress = new(value =>
            {
                _downloadProgress = value;
                InvokeAsync(StateHasChanged);
            });

            if (_selectedService is not WhisperRecognitionService whisperService)
            {
                _snackbar.Add("Выбранный сервис не поддерживает загрузку моделей", Severity.Error);
                return;
            }

            bool success = await whisperService.DownloadModelAsync(progress, _downloadCts.Token);

            if (success)
            {
                _isModelReady = true;
                _snackbar.Add(_localizer["ASR_Model_Downloaded_Success"], Severity.Success);
            }
            else
            {
                _snackbar.Add(_localizer["ASR_Model_Download_Failed"], Severity.Error);
            }
        }
        catch (OperationCanceledException)
        {
            _snackbar.Add("Загрузка модели отменена", Severity.Info);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при загрузке модели");
            _snackbar.Add($"Ошибка загрузки: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isDownloadingModel = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task CancelDownloadAsync()
    {
        if (_downloadCts is null)
        {
            return;
        }

        await _downloadCts.CancelAsync();
    }

    #endregion

    #region Recognition

    private async Task StartRecognitionAsync()
    {
        if (!await ValidateBeforeStartAsync())
        {
            return;
        }

        if (_logger?.IsEnabled(LogLevel.Information) == true)
        {
            _logger?.LogInformation("Начало распознавания файла: {FilePath} с использованием {ServiceName}",
                _filePath, _selectedService!.Name);
        }

        await InitializeStateAsync();

        try
        {
            CancellationToken token = _cts!.Token;
            StringBuilder stringBuilder = new();

            await foreach (ISpeechRecognitionResult chunk in _selectedService!.TranscribeAsync(_filePath, token))
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                stringBuilder.Append(chunk.Text);
                _resultText = stringBuilder.ToString();
                _processedChunksCount++;
                await InvokeAsync(StateHasChanged);
            }

            if (!token.IsCancellationRequested)
            {
                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger?.LogInformation("Распознавание успешно завершено. Всего чанков: {Count}", _processedChunksCount);
                }

                _snackbar.Add(_localizer["ASR_Completed_Success"], Severity.Success);
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger?.LogInformation(ex, "Распознавание отменено пользователем.");
            _snackbar.Add(_localizer["ASR_Cancelled"], Severity.Warning);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при распознавании файла: {FilePath}", _filePath);
            _snackbar.Add($"{_localizer["Common_Error"]}: {ex.Message}", Severity.Error,
                config => config.RequireInteraction = true);
        }
        finally
        {
            await FinalizeRecognitionAsync();
        }
    }

    private async Task<bool> ValidateBeforeStartAsync()
    {
        if (string.IsNullOrWhiteSpace(_filePath))
        {
            _snackbar.Add(_localizer["ASR_Error_No_File"], Severity.Warning);
            return false;
        }

        if (_selectedService == null)
        {
            _snackbar.Add(_localizer["ASR_Error_No_Service"], Severity.Warning);
            return false;
        }

        if (!_isModelReady)
        {
            _snackbar.Add("Сначала загрузите модель распознавания", Severity.Warning);
            return false;
        }

        if (!File.Exists(_filePath))
        {
            _snackbar.Add("Указанный файл не найден. Проверьте путь.", Severity.Error);
            return false;
        }

        return true;
    }

    private async Task InitializeStateAsync()
    {
        _isProcessing = true;
        _resultText = string.Empty;
        _processedChunksCount = 0;
        _cts = new CancellationTokenSource();

        // Убедимся, что модель готова (повторная проверка)
        if (!_isModelReady)
        {
            await CheckModelStatusAsync();
        }
    }

    private void CancelRecognitionAsync()
    {
        _logger?.LogDebug("Запрошена отмена распознавания.");
        _cts?.Cancel();
    }

    private async Task FinalizeRecognitionAsync()
    {
        _isProcessing = false;
        _cts?.Dispose();
        _cts = null;
        await InvokeAsync(StateHasChanged);
    }

    #endregion

    #region Keyboard Shortcuts

    private async Task OnPageKeyDown(KeyboardEventArgs e)
    {
        if (_isProcessing || _isDownloadingModel)
        {
            return;
        }

        if (e.CtrlKey && e.Code == "Enter")
        {
            await StartRecognitionAsync();
        }
    }

    #endregion

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _cts?.Dispose();
                _downloadCts?.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}