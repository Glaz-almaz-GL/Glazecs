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

    // Lazy<T> по умолчанию гарантирует потокобезопасную инициализацию (ExecutionAndPublication)
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

        _lazyFactory = new Lazy<WhisperFactory>(() => WhisperFactory.FromPath(ModelPath));
        _lazyProcessor = new Lazy<WhisperProcessor>(() => _lazyFactory.Value
            .CreateBuilder()
            .WithLanguage("auto")
            .Build());
    }

    #endregion

    #region Public Methods

    public async IAsyncEnumerable<ISpeechRecognitionResult> TranscribeAsync(
        Stream audioStream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (audioStream == null || !audioStream.CanRead)
        {
            _logger?.LogWarning("Попытка транскрипции невалидного или пустого потока.");
            yield break;
        }

        EnsureInitialized();

        await foreach (SegmentData chunk in _lazyProcessor.Value.ProcessAsync(audioStream, cancellationToken))
        {
            // Collection expression (C# 12) для эффективного создания массива
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
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        await foreach (ISpeechRecognitionResult result in TranscribeAsync(fileStream, cancellationToken))
        {
            yield return result;
        }
    }

    /// <summary>
    /// Загружает модель, если она еще не была загружена.
    /// Метод идемпотентен: если модель уже существует, загрузка пропускается.
    /// </summary>
    public async Task<bool> DownloadModelAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(ModelPath))
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger?.LogDebug("Модель {ModelName} уже существует. Загрузка пропущена.", ModelFileName);
            }

            return true;
        }

        try
        {
            Directory.CreateDirectory(_modelsPath);

            await using Stream modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(GgmlType, cancellationToken: cancellationToken);
            await using FileStream fileWriter = new(ModelPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);

            await modelStream.CopyToAsync(fileWriter, cancellationToken);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Модель {ModelName} успешно загружена.", ModelFileName);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при загрузке модели {ModelName}.", ModelFileName);
            return false;
        }
    }

    public async Task<bool> InitializeAsync()
    {
        return await DownloadModelAsync();
    }

    #endregion

    #region Private Methods

    private void EnsureInitialized()
    {
        if (!File.Exists(ModelPath))
        {
            throw new FileNotFoundException($"Модель Whisper не найдена по пути: {ModelPath}. Сначала вызовите DownloadModelAsync().");
        }

        _ = _lazyProcessor.Value;
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