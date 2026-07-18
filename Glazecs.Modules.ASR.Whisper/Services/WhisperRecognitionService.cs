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
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WhisperModels");

    private readonly Lazy<WhisperFactory> _lazyFactory;
    private readonly Lazy<WhisperProcessor> _lazyProcessor;
    private bool _disposed;

    #endregion

    #region Properties

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

        // Инициализация отложена до первого вызова TranscribeAsync
        _lazyFactory = new Lazy<WhisperFactory>(() => WhisperFactory.FromPath(ModelPath));
        _lazyProcessor = new Lazy<WhisperProcessor>(() => _lazyFactory.Value
            .CreateBuilder()
            .WithLanguage("auto")
            .Build());
    }

    #endregion

    #region Public Methods
    public async Task<bool> InitializeAsync()
    {
        return File.Exists(ModelPath);
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

        await foreach (SegmentData chunk in _lazyProcessor.Value.ProcessAsync(audioStream, cancellationToken))
        {
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
        await using FileStream fileStream = new(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);

        await foreach (ISpeechRecognitionResult result in TranscribeAsync(fileStream, cancellationToken))
        {
            yield return result;
        }
    }

    /// <summary>
    /// Загружает модель по требованию с отслеживанием прогресса.
    /// </summary>
    public async Task<bool> DownloadModelAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (File.Exists(ModelPath))
        {
            progress?.Report(100.0);
            return true;
        }

        try
        {
            Directory.CreateDirectory(_modelsPath);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Начало загрузки модели {ModelName}...", ModelFileName);
            }

            await using Stream modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType, cancellationToken: cancellationToken);

            // Если поток поддерживает длину, мы можем рассчитать прогресс
            bool canReportProgress = modelStream.CanSeek;
            long totalBytes = canReportProgress ? modelStream.Length : 0;
            long downloadedBytes = 0;
            byte[] buffer = new byte[81920]; // 80 KB buffer
            int bytesRead;

            await using FileStream fileWriter = new(ModelPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            while ((bytesRead = await modelStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await fileWriter.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                if (canReportProgress)
                {
                    downloadedBytes += bytesRead;
                    double percent = (double)downloadedBytes / totalBytes * 100.0;
                    progress?.Report(Math.Min(percent, 99.9)); // 100% будет установлено после завершения
                }
            }

            progress?.Report(100.0);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Модель {ModelName} успешно загружена.", ModelFileName);
            }

            return true;
        }
        catch (OperationCanceledException ex)
        {
            _logger?.LogWarning(ex, "Загрузка модели {ModelName} была отменена.", ModelFileName);
            // Удаляем неполный файл
            if (File.Exists(ModelPath))
            {
                File.Delete(ModelPath);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при загрузке модели {ModelName}.", ModelFileName);
            if (File.Exists(ModelPath))
            {
                File.Delete(ModelPath);
            }

            return false;
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