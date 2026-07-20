using Glazecs.Modules.ASR.Abstractions.Interfaces;
using Glazecs.Modules.ASR.Abstractions.Models;
using Glazecs.Modules.ASR.Resources.Languages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Text;

namespace Glazecs.Modules.ASR.Components.Pages;

public partial class SpeechRecognitionPage : ComponentBase, IDisposable
{
    #region Injection & Fields

    [Inject] private IEnumerable<ISpeechRecognitionService> SpeechServices { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<AsrResources> L { get; set; } = default!;
    [Inject] private ILogger<SpeechRecognitionPage> Logger { get; set; } = default!;

    private CancellationTokenSource? _recognitionCts;
    private CancellationTokenSource? _initializingCts;
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
    private bool _isInitializing;
    private int _processedChunksCount;

    private string _initProgressMessage = string.Empty;
    private double _initProgressPercent;

    private readonly Progress<SpeechInitializeProgress> _initializingProgress = new();
    private ISpeechRecognitionService? _selectedService;

    #endregion

    protected override void OnInitialized()
    {
        // Корректная привязка прогресса к UI
        _initializingProgress.ProgressChanged += (_, progress) =>
        {
            _initProgressMessage = progress.Message;
            _initProgressPercent = progress.Percent;

            InvokeAsync(StateHasChanged);
        };

        base.OnInitialized();
    }

    #region Model Management

    private void OnServiceChanged(ISpeechRecognitionService? service)
    {
        _selectedService = service;
        _initProgressMessage = string.Empty;
        _initProgressPercent = 0;
    }

    private async Task InitializeServiceAsync()
    {
        if (_selectedService == null || _selectedService.Initialized)
        {
            return;
        }

        _isInitializing = true;
        _initProgressMessage = L["ASR_Initializing_Started"];
        await InvokeAsync(StateHasChanged);

        try
        {
            _initializingCts = new();
            await _selectedService.InitializeAsync(_initializingProgress, _initializingCts.Token);

            _initializingCts.Dispose();
            _initializingCts = null;

            Snackbar.Add(L["ASR_Initialized_Success"], Severity.Success);
        }
        catch (OperationCanceledException ex)
        {
            Logger.LogInformation(ex, "Инициализация сервиса была отменена.");
            Snackbar.Add(L["ASR_Initialization_Cancelled"], Severity.Warning);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ошибка при инициализации сервиса распознавания.");
            Snackbar.Add($"{L["Common_Error"]}: {ex.Message}", Severity.Error, config => config.RequireInteraction = true);
        }
        finally
        {
            _isInitializing = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    #endregion

    #region Recognition

    private async Task StartRecognitionAsync()
    {
        if (_selectedService != null && !_selectedService.Initialized)
        {
            await InitializeServiceAsync();

            if (!_selectedService.Initialized)
            {
                return;
            }
        }

        if (!ValidateBeforeStart())
        {
            return;
        }

        if (Logger.IsEnabled(LogLevel.Information))
        {
            Logger.LogInformation("Начало распознавания файла: {FilePath} с использованием {ServiceName}",
                _filePath, _selectedService!.Name);
        }

        InitializeState();

        try
        {
            CancellationToken token = _recognitionCts!.Token;
            StringBuilder stringBuilder = new();

            await foreach (ISpeechRecognitionResult chunk in _selectedService!.TranscribeAsync(_filePath, token))
            {
                token.ThrowIfCancellationRequested();

                stringBuilder.Append(chunk.Text);
                _resultText = stringBuilder.ToString();
                _processedChunksCount++;

                await InvokeAsync(StateHasChanged);
            }

            if (!token.IsCancellationRequested)
            {
                if (Logger.IsEnabled(LogLevel.Information))
                {
                    Logger.LogInformation("Распознавание успешно завершено. Всего чанков: {Count}", _processedChunksCount);
                }

                Snackbar.Add(L["ASR_Completed_Success"], Severity.Success);
            }
        }
        catch (OperationCanceledException ex)
        {
            Logger.LogInformation(ex, "Распознавание отменено пользователем.");
            Snackbar.Add(L["ASR_Cancelled"], Severity.Warning);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Ошибка при распознавании файла: {FilePath}", _filePath);
            Snackbar.Add($"{L["Common_Error"]}: {ex.Message}", Severity.Error,
                config => config.RequireInteraction = true);
        }
        finally
        {
            FinalizeRecognition();
        }
    }

    private bool ValidateBeforeStart()
    {
        if (string.IsNullOrWhiteSpace(_filePath))
        {
            Snackbar.Add(L["ASR_Error_No_File"], Severity.Warning);
            return false;
        }

        if (_selectedService == null)
        {
            Snackbar.Add(L["ASR_Error_No_Service"], Severity.Warning);
            return false;
        }

        if (!File.Exists(_filePath))
        {
            Snackbar.Add(L["ASR_Error_File_Not_Found"], Severity.Error);
            return false;
        }

        return true;
    }

    private void InitializeState()
    {
        _isProcessing = true;
        _resultText = string.Empty;
        _processedChunksCount = 0;
        _recognitionCts = new CancellationTokenSource();
    }

    private void CancelRecognition()
    {
        Logger.LogDebug("Запрошена отмена распознавания.");
        _recognitionCts?.Cancel();
    }

    private void CancelInitializing()
    {
        Logger.LogDebug("Запршена отмена инициализации сервиса.");
        _initializingCts?.Cancel();
    }

    private void FinalizeRecognition()
    {
        _isProcessing = false;
        _recognitionCts?.Dispose();
        _recognitionCts = null;
        StateHasChanged();
    }

    #endregion

    #region Keyboard Shortcuts

    private async Task OnPageKeyDown(KeyboardEventArgs e)
    {
        if (_isProcessing || _isInitializing)
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
                CancelRecognition();
                CancelInitializing();
                _recognitionCts?.Dispose();
                _initializingCts?.Dispose();
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