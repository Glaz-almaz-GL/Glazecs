using Glazecs.Modules.FileChunker.Abstractions.Models;

namespace Glazecs.Modules.FileChunker.Abstractions.Interfaces
{
    /// <summary>
    /// Интерфейс основного модуля для объединения и разбивки файлов на чанки.
    /// </summary>
    public interface IFileChunker
    {
        string Name { get; }
        IReadOnlyCollection<string> SupportedExtensions { get; }

        Task<IReadOnlyCollection<ChunkResult>> ProcessAsync(
            IEnumerable<string> filePaths,
            ChunkingOptions chunkingOptions,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<ChunkResult>> ProcessAsync(
            IEnumerable<Func<Stream>> streamFactories,
            ChunkingOptions chunkingOptions,
            CancellationToken cancellationToken = default);
    }
}
