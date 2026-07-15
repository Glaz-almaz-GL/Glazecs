namespace Glazecs.Modules.FMMS.Abstractions.Models
{
    /// <summary>
    /// Результат сканирования директории.
    /// </summary>
    public readonly record struct ScannedDirectory
    {
        /// <summary>
        /// Уникальный идентификатор записи.
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// Относительный путь директории.
        /// </summary>
        public string RelativePath { get; init; }

        /// <summary>
        /// Абсолютный (полный) путь к директории.
        /// </summary>
        public string FullPath { get; init; }

        /// <summary>
        /// Суммарный размер всех файлов в директории (в байтах).
        /// </summary>
        public long Size { get; init; }

        /// <summary>
        /// Общее количество файлов внутри директории и её поддиректорий.
        /// </summary>
        public int FilesCount { get; init; }
    }
}