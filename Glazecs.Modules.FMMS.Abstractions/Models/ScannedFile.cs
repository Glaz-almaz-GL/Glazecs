namespace Glazecs.Modules.FMMS.Abstractions.Models
{
    /// <summary>
    /// Результат сканирования файла
    /// </summary>
    public sealed class ScannedFile
    {
        /// <summary>
        /// Идентификатор
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Относительный путь файла
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Полный путь
        /// </summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>
        /// Расширение файла (в нижнем регистре, с точкой)
        /// </summary>
        public string Extension { get; set; } = string.Empty;

        /// <summary>
        /// Размер файла в байтах
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Количество страниц (для PDF и других поддерживаемых форматов)
        /// </summary>
        public int PagesCount { get; set; }

        /// <summary>
        /// Является ли файл архивом
        /// </summary>
        public bool IsArchive { get; set; }

        /// <summary>
        /// Является ли элемент записью в архиве
        /// </summary>
        public bool IsArchiveEntry { get; set; }

        /// <summary>
        /// Сжатый размер (для записей архива)
        /// </summary>
        public long CompressedSize { get; set; }

        /// <summary>
        /// Распакованный размер (для записей архива)
        /// </summary>
        public long UnCompressedSize { get; set; }

        /// <summary>
        /// Вычисленные хеши (ключ = имя алгоритма, значение = хеш в заданном формате)
        /// </summary>
        /// <example>
        /// { "SHA-256" = "e3b0c44298fc1c14...", "MD5" = "d41d8cd98f00b204..." }
        /// </example>
        public Dictionary<string, string> Hashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Получить хеш по имени алгоритма
        /// </summary>
        public string? GetHash(string algorithmName)
        {
            return Hashes.TryGetValue(algorithmName, out string? hash) ? hash : null;
        }

        /// <summary>
        /// Установить хеш для указанного алгоритма
        /// </summary>
        public void SetHash(string algorithmName, string hash)
        {
            Hashes[algorithmName] = hash;
        }
    }
}