namespace Glazecs.Modules.FMMS.Abstractions.Models
{
    public readonly record struct ScannedDirectory
    {
        /// <summary>
        /// Идентификатор
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        ///
        /// </summary>
        public string RelativePath { get; init; }

        /// <summary>
        ///
        /// </summary>
        public string FullPath { get; init; }

        /// <summary>
        ///
        /// </summary>
        public long Size { get; init; }

        /// <summary>
        ///
        /// </summary>
        public int FilesCount { get; init; }
    }
}
