namespace Glazecs.Modules.FMMS.Abstractions.Models
{
    public record class ScannedDirectory
    {
        public string RelativePath { get; init; } = string.Empty;
        public string FullPath { get; init; } = string.Empty;
        public long Size { get; init; } = 0;
        public int FilesCount { get; init; }
    }
}
