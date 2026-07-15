using Glazecs.Modules.FMMS.Abstractions.Enums;

namespace Glazecs.Modules.FMMS.Abstractions.Models
{
    /// <summary>
    /// Настройки видимости колонок
    /// </summary>
    public sealed class AnalyzeFileSettings
    {
        /// <summary>
        /// Видимость стандартных колонок
        /// </summary>
        public Dictionary<AnalyzeField, bool> FieldsToAnalyze { get; set; } = new()
        {
            { AnalyzeField.Id, true },
            { AnalyzeField.Name, true },
            { AnalyzeField.PagesCount, true },
            { AnalyzeField.Extension, true },
            { AnalyzeField.FullPath, false },
            { AnalyzeField.IsArchive, false },
            { AnalyzeField.IsArchiveEntry, false },
            { AnalyzeField.CompressedSize, false },
            { AnalyzeField.UnCompressedSize, false },
            { AnalyzeField.Size, true }
        };
    }
}
