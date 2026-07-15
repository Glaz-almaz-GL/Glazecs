namespace Glazecs.Modules.FMMS.Abstractions.Models
{
    /// <summary>
    /// Результат сканирования файла (неизменяемая модель данных).
    /// </summary>
    public readonly record struct ScannedFile
    {
        public int Id { get; init; }
        public string Name { get; init; }
        public string FullPath { get; init; }

        /// <summary>
        /// Расширение файла (в нижнем регистре, с точкой, например ".txt").
        /// </summary>
        public string Extension { get; init; }

        public long Size { get; init; }
        public int PagesCount { get; init; }
        public bool IsArchive { get; init; }
        public bool IsArchiveEntry { get; init; }
        public long CompressedSize { get; init; }

        /// <summary>
        /// Распакованный размер (для записей архива).
        /// </summary>
        public long UncompressedSize { get; init; }

        /// <summary>
        /// Словарь вычисленных хешей (ключ = имя алгоритма, значение = хеш).
        /// </summary>
        public IReadOnlyDictionary<string, string> Hashes { get; init; }
    }
}