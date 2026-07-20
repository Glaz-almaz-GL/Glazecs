using Glazecs.Modules.ASR.Abstractions.Interfaces;
using Glazecs.Modules.ASR.Abstractions.Models;
using Glazecs.Modules.ASR.Whisper.Extensions;
using Glazecs.Modules.ASR.Whisper.Resources.Languages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using Whisper.net;
using Whisper.net.Ggml;

namespace Glazecs.Modules.ASR.Whisper.Services;

public sealed class WhisperRecognitionService : ISpeechRecognitionService
{
    #region Fields

    private readonly ILogger<WhisperRecognitionService>? _logger;
    private readonly string _modelsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "WhisperModels");

    private readonly Lazy<WhisperFactory> _lazyFactory;
    private readonly Lazy<WhisperProcessor> _lazyProcessor;
    private bool _disposed;

    #endregion

    #region Properties

    // Реализация свойства интерфейса, ожидаемого в UI
    public bool Initialized { get; private set; }

    public string Name => $"Whisper ({GgmlType})";
    public string Description => _localizer["Whisper_Description"];
    public GgmlType GgmlType { get; }

    private readonly IStringLocalizer<WhisperResources> _localizer;
    private string ModelFileName => GgmlType.ToModelFileName();
    private string ModelPath => Path.Combine(_modelsPath, ModelFileName);

    #endregion

    #region Constructor

    public WhisperRecognitionService(
        GgmlType ggmlType,
        IStringLocalizer<WhisperResources> localizer,
        ILogger<WhisperRecognitionService>? logger = null)
    {
        GgmlType = ggmlType;
        _localizer = localizer;
        _logger = logger;

        // Инициализация отложена до первого вызова TranscribeAsync или явной инициализации
        _lazyFactory = new Lazy<WhisperFactory>(() => WhisperFactory.FromPath(ModelPath));
        _lazyProcessor = new Lazy<WhisperProcessor>(() => _lazyFactory.Value
            .CreateBuilder()
            .WithLanguage("auto")
            .Build());

        // Проверяем начальное состояние
        Initialized = File.Exists(ModelPath);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Инициализирует сервис: проверяет наличие модели и загружает её при необходимости.
    /// </summary>
    public async Task<bool> InitializeAsync(IProgress<SpeechInitializeProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (Initialized)
        {
            progress?.Report(new SpeechInitializeProgress(100, 100, _localizer["Whisper_Model_Already_Initialized"]));
            return true;
        }

        progress?.Report(new SpeechInitializeProgress(0, 100, _localizer["Whisper_Checking_Model"]));

        if (!File.Exists(ModelPath))
        {
            bool success = await DownloadModelAsync(progress, cancellationToken);
            if (!success)
            {
                throw new InvalidOperationException(_localizer["Whisper_Model_Download_Failed"]);
            }
        }

        try
        {
            // Проверяем, что файл не поврежден, инициируя создание фабрики
            _ = _lazyFactory.Value;
            Initialized = true;
            progress?.Report(new SpeechInitializeProgress(100, 100, _localizer["Whisper_Model_Initialized_Success"]));
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при инициализации фабрики Whisper из пути {ModelPath}", ModelPath);
            Initialized = false;

            throw new InvalidOperationException("Ошибка инициализации модели. Возможно, файл поврежден.", ex);
        }
    }

    public async IAsyncEnumerable<ISpeechRecognitionResult> TranscribeAsync(
        Stream audioStream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (audioStream == null || !audioStream.CanRead)
        {
            _logger?.LogWarning("Попытка транскрипции невалидного или пустого потока.");
            yield break;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!Initialized)
        {
            await InitializeAsync(null, cancellationToken);
        }

        await foreach (SegmentData chunk in _lazyProcessor.Value.ProcessAsync(audioStream, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            SpeechRecognitionToken[] tokens = [.. chunk.Tokens
                .Select(t => new SpeechRecognitionToken(
                    Text: t.Text,
                    Start: TimeSpan.FromMilliseconds(t.Start),
                    End: TimeSpan.FromMilliseconds(t.End),
                    Confidence: t.Probability
                ))];

            yield return new SpeechRecognitionResult(
                Text: chunk.Text,
                Start: chunk.Start,
                End: chunk.End,
                IsFinal: true,
                Confidence: chunk.Probability,
                Tokens: tokens
            );
        }
    }

    public async IAsyncEnumerable<ISpeechRecognitionResult> TranscribeAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            _logger?.LogWarning("Файл для транскрипции не найден: {FilePath}", filePath);
            yield break;
        }

        await using FileStream fileStream = new(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);

        await foreach (ISpeechRecognitionResult result in TranscribeAsync(fileStream, cancellationToken))
        {
            yield return result;
        }
    }

    /// <summary>
    /// Загружает модель по требованию с отслеживанием прогресса.
    /// </summary>
    private async Task<bool> DownloadModelAsync(IProgress<SpeechInitializeProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (File.Exists(ModelPath))
        {
            progress?.Report(new SpeechInitializeProgress(100, 100, _localizer["Whisper_Model_Already_Downloaded"]));
            return true;
        }

        try
        {
            Directory.CreateDirectory(_modelsPath);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Начало загрузки модели {ModelName}...", ModelFileName);
            }

            progress?.Report(new SpeechInitializeProgress(0, 100, _localizer["Whisper_Connecting_To_Server"]));

            await using Stream modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType, cancellationToken: cancellationToken);

            bool canReportProgress = modelStream.CanSeek;
            long totalBytes = canReportProgress ? modelStream.Length : 0;
            long downloadedBytes = 0;
            byte[] buffer = new byte[81920]; // 80 KB buffer
            int bytesRead;

            await using FileStream fileWriter = new(ModelPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            while ((bytesRead = await modelStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await fileWriter.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                if (canReportProgress && totalBytes > 0)
                {
                    downloadedBytes += bytesRead;
                    int percent = (int)(downloadedBytes * 100 / totalBytes);
                    progress?.Report(new SpeechInitializeProgress(Math.Min(percent, 99), 100, _localizer["Whisper_Downloading_Model"]));
                }
            }

            progress?.Report(new SpeechInitializeProgress(100, 100, _localizer["Whisper_Model_Downloaded_Success"]));

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Модель {ModelName} успешно загружена.", ModelFileName);
            }

            return true;
        }
        catch (OperationCanceledException ex)
        {
            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger?.LogInformation(ex, "Загрузка модели {ModelName} была отменена пользователем.", ModelFileName);
            }

            CleanupPartialFile();
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при загрузке модели {ModelName}.", ModelFileName);
            CleanupPartialFile();

            return false;
        }
    }

    #endregion

    #region Private Methods

    private void CleanupPartialFile()
    {
        if (File.Exists(ModelPath))
        {
            try { File.Delete(ModelPath); }
            catch { /* Ignore errors */}
        }
    }

    #endregion

    #region IDisposable Implementation

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_lazyFactory.IsValueCreated)
                {
                    _lazyFactory.Value.Dispose();
                }

                if (_lazyProcessor.IsValueCreated)
                {
                    _lazyProcessor.Value.Dispose();
                }
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