using System.Collections.Immutable;

namespace Glazecs.Modules.FileChunker.Abstractions.Models
{
    /// <summary>
    /// Контекст метаданных для генерации заголовка чанка.
    /// Содержит все данные, доступные для подстановки в шаблон.
    /// </summary>
    public readonly record struct ChunkMetadataContext
    {
        /// <summary>
        /// Имя исходного файла (без пути).
        /// </summary>
        public string FileName { get; init; }

        /// <summary>
        /// Полный путь к исходному файлу.
        /// </summary>
        public string OriginalPath { get; init; }

        /// <summary>
        /// Номер текущей части файла (1-based, если файл был разбит на несколько чанков).
        /// </summary>
        public int FilePartNumber { get; init; }

        /// <summary>
        /// Общее количество частей, на которые разбит исходный файл.
        /// </summary>
        public int TotalFileParts { get; init; }

        /// <summary>
        /// Размер исходного файла в байтах.
        /// </summary>
        public long FileSizeBytes { get; init; }

        /// <summary>
        /// Индекс текущего чанка в общем списке результатов (0-based).
        /// </summary>
        public int ChunkIndex { get; init; }

        /// <summary>
        /// Дата и время генерации чанка.
        /// </summary>
        public DateTime GeneratedAt { get; init; }

        /// <summary>
        /// Словарь для кастомных метаданных, которые могут быть добавлены модулями или правилами.
        /// Ключ — имя плейсхолдера (без скобок), значение — подставляемое значение.
        /// </summary>
        /// <remarks>
        /// Используется <see cref="ImmutableDictionary{TKey, TValue}"/> для обеспечения неизменяемости value type.
        /// </remarks>
        public IImmutableDictionary<string, object> CustomProperties { get; init; }

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="ChunkMetadataContext"/> с указанными параметрами.
        /// </summary>
        /// <param name="fileName">Имя файла.</param>
        /// <param name="originalPath">Полный путь к файлу.</param>
        /// <param name="filePartNumber">Номер части файла.</param>
        /// <param name="totalFileParts">Общее количество частей.</param>
        /// <param name="fileSizeBytes">Размер файла в байтах.</param>
        /// <param name="chunkIndex">Индекс чанка.</param>
        /// <param name="generatedAt">Дата генерации.</param>
        /// <param name="customProperties">Кастомные свойства.</param>
#pragma warning disable S107
        public ChunkMetadataContext(
            string fileName,
            string originalPath,
            long fileSizeBytes,
            int filePartNumber = 1,
            int totalFileParts = 1,
            int chunkIndex = 0,
            DateTime generatedAt = default,
            IImmutableDictionary<string, object>? customProperties = null)
#pragma warning restore S107
        {
            FileName = fileName ?? string.Empty;
            OriginalPath = originalPath ?? string.Empty;
            FilePartNumber = filePartNumber > 0 ? filePartNumber : 1;
            TotalFileParts = totalFileParts > 0 ? totalFileParts : 1;
            FileSizeBytes = fileSizeBytes >= 0L ? fileSizeBytes : 0L;
            ChunkIndex = chunkIndex >= 0 ? chunkIndex : 0;
            GeneratedAt = generatedAt == default ? DateTime.UtcNow : generatedAt;
            CustomProperties = customProperties ?? ImmutableDictionary<string, object>.Empty;
        }
    }
}