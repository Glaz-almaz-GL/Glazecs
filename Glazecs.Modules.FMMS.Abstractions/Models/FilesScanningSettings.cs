using Glazecs.Modules.FMMS.Abstractions.Enums;

namespace Glazecs.Modules.FMMS.Abstractions.Models
{
    /// <summary>
    /// Настройки сканирования файлов
    /// </summary>
    public sealed record class FilesScanningSettings
    {
        #region General

        /// <summary>
        /// Единица измерения отображаемого размера файлов
        /// </summary>
        public FileSizeType DisplayedSizeType { get; set; } = FileSizeType.MB;

        /// <summary>
        /// Сканировать ли содержимое архивов
        /// </summary>
        public bool ScanArchives { get; set; } = true;

        /// <summary>
        /// Пользовательские расширения, считающиеся архивами
        /// </summary>
        public HashSet<string> CustomArchiveExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz", ".bz2", ".tb2", ".xz",
            ".txz", ".lz", ".tlz", ".z", ".lzma", ".lzo", ".ar", ".cpio", ".iso",
            ".dmg", ".wim", ".esd", ".squashfs", ".cramfs", ".jar", ".war", ".apk",
            ".xpi", ".epub", ".s7z"
        };

        #endregion

        #region Page Count

        /// <summary>
        /// Пользовательские правила подсчёта страниц для расширений
        /// </summary>
        public Dictionary<string, int> PagesCountCustomRules { get; set; } = [];

        #endregion

        #region Hashing

        /// <summary>
        /// Настройки вычисления хешей
        /// </summary>
        public HashingSettings Hashing { get; set; } = new();

        #endregion

        #region Display

        /// <summary>
        /// Настройки видимости колонок
        /// </summary>
        public AnalyzeFileSettings AnalyzeSettings { get; set; } = new();

        #endregion
    }
}