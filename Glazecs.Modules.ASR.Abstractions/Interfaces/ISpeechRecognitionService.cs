using Glazecs.Modules.ASR.Abstractions.Models;

namespace Glazecs.Modules.ASR.Abstractions.Interfaces
{
    /// <summary>
    /// Представляет унифицированный контракт для сервиса распознавания речи (ASR).
    /// Поддерживает как пакетную обработку файлов, так и потоковое распознавание.
    /// </summary>
    public interface ISpeechRecognitionService : IDisposable
    {
        string Name { get; }
        string Description { get; }
        bool Initialized { get; }

        /// <summary>
        /// Выполняет транскрипцию аудиопотока в реальном времени.
        /// </summary>
        /// <param name="audioStream">Абстрактный поток с аудиоданными (например, с микрофона или из сети).</param>
        /// <param name="cancellationToken">Токен для отмены асинхронной операции.</param>
        /// <returns>Поток результатов распознавания. Для стриминга включает как промежуточные (IsFinal=false), так и финальные (IsFinal=true) сегменты.</returns>
        IAsyncEnumerable<ISpeechRecognitionResult> TranscribeAsync(
            Stream audioStream,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Выполняет пакетную транскрипцию аудиофайла.
        /// </summary>
        /// <param name="filePath">Локальный путь к аудиофайлу.</param>
        /// <param name="cancellationToken">Токен для отмены асинхронной операции.</param>
        /// <returns>Поток результатов распознавания. Обычно все результаты возвращаются с IsFinal=true.</returns>
        IAsyncEnumerable<ISpeechRecognitionResult> TranscribeAsync(
            string filePath,
            CancellationToken cancellationToken = default);

        Task<bool> InitializeAsync(IProgress<SpeechInitializeProgress>? progress = null, CancellationToken cancellationToken = default);
    }
}
