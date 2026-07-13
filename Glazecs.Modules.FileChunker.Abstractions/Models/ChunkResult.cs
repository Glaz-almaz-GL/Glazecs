using System.Collections.Immutable;

namespace Glazecs.Modules.FileChunker.Abstractions.Models
{
    /// <summary>
    /// Результат операции чанкинга (один итоговый файл).
    /// </summary>
    /// <param name="ChunkId">Уникальный идентификатор чанка.</param>
    /// <param name="OutputFileName">Имя выходного файла.</param>
    /// <param name="OutputPath">Полный путь к сохраненному файлу.</param>
    /// <param name="SizeInBytes">Размер чанка в байтах.</param>
    /// <param name="SourceFileNames">Коллекция имен исходных файлов, вошедших в этот чанк.</param>
    public readonly record struct ChunkResult
    {
        /// <summary>
        /// Уникальный идентификатор чанка.
        /// </summary>
        public Guid ChunkId { get; init; }

        /// <summary>
        /// Имя выходного файла.
        /// </summary>
        public string OutputFileName { get; init; }

        /// <summary>
        /// Полный путь к сохраненному файлу.
        /// </summary>
        public string OutputPath { get; init; }

        /// <summary>
        /// Размер чанка в байтах.
        /// </summary>
        public long SizeInBytes { get; init; }

        /// <summary>
        /// Коллекция имен исходных файлов, вошедших в этот чанк.
        /// </summary>
        /// <remarks>
        /// Используется <see cref="ImmutableArray{T}"/> для обеспечения неизменяемости value type.
        /// </remarks>
        public ImmutableArray<string> SourceFileNames { get; init; }

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="ChunkResult"/> с указанными параметрами.
        /// </summary>
        /// <param name="chunkId">Идентификатор чанка.</param>
        /// <param name="outputFileName">Имя выходного файла.</param>
        /// <param name="outputPath">Путь к файлу.</param>
        /// <param name="sizeInBytes">Размер в байтах.</param>
        /// <param name="sourceFileNames">Исходные файлы.</param>
        /// <exception cref="ArgumentException">Выбрасывается, если обязательные параметры являются null или пустыми.</exception>
        public ChunkResult(
            Guid chunkId,
            string outputFileName,
            string outputPath,
            long sizeInBytes,
            IEnumerable<string>? sourceFileNames = null)
        {
            if (chunkId == Guid.Empty)
            {
                throw new ArgumentException("Chunk ID cannot be empty.", nameof(chunkId));
            }

            ChunkId = chunkId;
            OutputFileName = outputFileName ?? throw new ArgumentNullException(nameof(outputFileName));
            OutputPath = outputPath ?? throw new ArgumentNullException(nameof(outputPath));
            SizeInBytes = sizeInBytes >= 0L ? sizeInBytes : throw new ArgumentOutOfRangeException(nameof(sizeInBytes), "Size cannot be negative.");
            SourceFileNames = sourceFileNames?.ToImmutableArray() ?? [];
        }
    }
}