namespace Glazecs.Modules.FMMS.Abstractions.Models
{
    /// <summary>
    /// Результат сканирования файла
    /// </summary>
    public readonly record struct ScannedFile
    {
        /// <summary>
        /// Идентификатор
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// Относительный путь файла
        /// </summary>
        public string Name { get; init; }

        /// <summary>
        /// Полный путь
        /// </summary>
        public string FullPath { get; init; }

        /// <summary>
        /// Расширение файла (в нижнем регистре, с точкой)
        /// </summary>
        public string Extension { get; init; }

        /// <summary>
        /// Размер файла в байтах
        /// </summary>
        public long Size { get; init; }

        /// <summary>
        /// Количество страниц (для PDF и других поддерживаемых форматов)
        /// </summary>
        public int PagesCount { get; init; }

        /// <summary>
        /// Является ли файл архивом
        /// </summary>
        public bool IsArchive { get; init; }

        /// <summary>
        /// Является ли элемент записью в архиве
        /// </summary>
        public bool IsArchiveEntry { get; init; }

        /// <summary>
        /// Сжатый размер (для записей архива)
        /// </summary>
        public long CompressedSize { get; init; }

        /// <summary>
        /// Распакованный размер (для записей архива)
        /// </summary>
        public long UnCompressedSize { get; init; }

        /// <summary>
        /// Вычисленные хеши (ключ = имя алгоритма, значение = хеш в заданном формате)
        /// </summary>
        /// <example>
        /// { "SHA-256" = "e3b0c44298fc1c14...", "MD5" = "d41d8cd98f00b204..." }
        /// </example>
        public Dictionary<string, string> Hashes { get; init; }
    }
}