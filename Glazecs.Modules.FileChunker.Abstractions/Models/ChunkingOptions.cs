using Glazecs.Modules.FileChunker.Abstractions.Interfaces;

namespace Glazecs.Modules.FileChunker.Abstractions.Models
{
    /// <summary>
    /// Опции операции чанкинга файлов.
    /// </summary>
    /// <param name="OutputDirectory">Директория для сохранения результирующих чанков.</param>
    /// <param name="Rules">Коллекция правил для предобработки текста. Если null, используется пустая коллекция.</param>
    /// <param name="MaxChunkSizeBytes">Максимальный размер одного чанка в байтах. По умолчанию 20 МБ.</param>
    /// <param name="HeaderTemplate">Шаблон заголовка с плейсхолдерами (например, "{FileName} - Part {FilePart}"). Может быть null.</param>
    /// <param name="HeaderFormatter">Форматтер для обработки шаблона заголовка. Если null, используется дефолтный <see cref="TemplateHeaderFormatter"/>.</param>
    public readonly record struct ChunkingOptions
    {
        /// <summary>
        /// Директория для сохранения результирующих чанков.
        /// </summary>
        public string OutputDirectory { get; init; }

        /// <summary>
        /// Коллекция правил для предобработки текста.
        /// </summary>
        public IReadOnlyList<IChunkRule> Rules { get; init; }

        /// <summary>
        /// Максимальный размер одного чанка в байтах.
        /// </summary>
        public long MaxChunkSizeBytes { get; init; }

        /// <summary>
        /// Шаблон заголовка с плейсхолдерами.
        /// </summary>
        public string? HeaderTemplate { get; init; }

        /// <summary>
        /// Форматтер для обработки шаблона заголовка.
        /// </summary>
        public IHeaderFormatter? HeaderFormatter { get; init; }

        /// <summary>
        /// Максимальный размер чанка по умолчанию (20 МБ).
        /// </summary>
        public const long DefaultMaxChunkSizeBytes = 20L * 1024L * 1024L;

        /// <summary>
        /// Инициализирует новый экземпляр <see cref="ChunkingOptions"/> с указанными параметрами.
        /// </summary>
        /// <param name="outputDirectory">Директория для сохранения чанков.</param>
        /// <param name="rules">Коллекция правил. Может быть null.</param>
        /// <param name="maxChunkSizeBytes">Максимальный размер чанка в байтах.</param>
        /// <param name="headerTemplate">Шаблон заголовка.</param>
        /// <param name="headerFormatter">Форматтер заголовка.</param>
        /// <exception cref="ArgumentException">Выбрасывается, если <paramref name="outputDirectory"/> является null или пустой строкой.</exception>
        public ChunkingOptions(
            string outputDirectory,
            IEnumerable<IChunkRule>? rules = null,
            long maxChunkSizeBytes = DefaultMaxChunkSizeBytes,
            string? headerTemplate = null,
            IHeaderFormatter? headerFormatter = null)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Output directory cannot be null or empty.", nameof(outputDirectory));
            }

            OutputDirectory = outputDirectory;
            Rules = rules?.ToList() ?? [];
            MaxChunkSizeBytes = maxChunkSizeBytes > 0
                ? maxChunkSizeBytes
                : throw new ArgumentOutOfRangeException(nameof(maxChunkSizeBytes), "Max chunk size must be greater than zero.");
            HeaderTemplate = headerTemplate;
            HeaderFormatter = headerFormatter;
        }
    }
}