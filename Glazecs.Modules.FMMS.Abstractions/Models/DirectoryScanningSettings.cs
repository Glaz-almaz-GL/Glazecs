using Glazecs.Modules.FMMS.Abstractions.Enums;

namespace Glazecs.Modules.FMMS.Abstractions.Models
{
    public sealed class DirectoryScanningSettings
    {
        public HashSet<string> CustomArchiveExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz", ".bz2", ".tb2", ".xz",
            ".txz", ".lz", ".tlz", ".z", ".lzma", ".lzo", ".ar", ".cpio", ".iso",
            ".dmg", ".wim", ".esd", ".squashfs", ".cramfs", ".jar", ".war", ".apk",
            ".xpi", ".epub", ".s7z"
        };

        public FileSizeType DisplayedSizeType { get; set; } = FileSizeType.MB;
        public bool IncludeHidden { get; set; } = false;
    }
}
