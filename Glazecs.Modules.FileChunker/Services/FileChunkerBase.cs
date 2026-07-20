using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Glazecs.Modules.FileChunker.Abstractions.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Glazecs.Modules.FileChunker.Services
{
    /// <summary>
    /// Базовый абстрактный класс для всех реализаций чанкеров.
    /// Реализует общую логику оркестрации: обработку потоков, батчинг,
    /// разбиение крупных фрагментов, сохранение чанков и управление состоянием.
    /// </summary>
    public abstract class FileChunkerBase(ILogger? logger, IHeaderFormatter? defaultHeaderFormatter) : IFileChunker
    {
        protected readonly ILogger? _logger = logger;
        protected readonly IHeaderFormatter _defaultHeaderFormatter = defaultHeaderFormatter ?? new Abstractions.Formatters.TemplateHeaderFormatter();

        /// <inheritdoc />
        public abstract string Name { get; }

        /// <inheritdoc />
        public abstract IReadOnlyCollection<string> SupportedExtensions { get; }

        #region Public API (Template Methods)

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<ChunkResult>> ProcessAsync(
            IEnumerable<string> filePaths,
            ChunkingOptions chunkingOptions,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filePaths);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Начало обработки файлов ({Chunker}). Количество: {Count}",
                    Name, filePaths.Count());
            }

            IEnumerable<Func<Stream>> streamFactories = filePaths.Select(path =>
            {
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"File not found: {path}", path);
                }

                string extension = Path.GetExtension(path).ToLowerInvariant();
                return !SupportedExtensions.Contains(extension)
                    ? throw new NotSupportedException($"Unsupported file extension: {extension}. Supported: {string.Join(", ", SupportedExtensions)}")
                    : new Func<Stream>(() => File.OpenRead(path));
            });

            return await ProcessAsync(streamFactories, chunkingOptions, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyCollection<ChunkResult>> ProcessAsync(
            IEnumerable<Func<Stream>> streamFactories,
            ChunkingOptions chunkingOptions,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(streamFactories);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Начало обработки потоков ({Chunker}). Максимальный размер чанка: {MaxSize} байт",
                    Name, chunkingOptions.MaxChunkSizeBytes);
            }

            ChunkingState state = new();

            foreach (Func<Stream> streamFactory in streamFactories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ProcessStreamAsync(streamFactory, chunkingOptions, state, cancellationToken);
            }

            await FinalizeChunkingAsync(state, chunkingOptions);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Обработка ({Chunker}) завершена. Создано чанков: {Count}",
                    Name, state.Results.Count);
            }

            return state.Results;
        }

        #endregion

        #region Abstract Method (to be implemented by inheritors)

        /// <summary>
        /// Извлекает текст из потока и передаёт его в общую логику батчинга.
        /// </summary>
        protected abstract Task ProcessStreamAsync(
            Func<Stream> streamFactory,
            ChunkingOptions options,
            ChunkingState state,
            CancellationToken ct);

        #endregion

        #region Batch and Chunk Processing (Shared Logic)

        protected async Task ProcessBatchAsync(
            string batchContent,
            string fileName,
            long fileSize,
            ChunkingOptions options,
            ChunkingState state,
            CancellationToken ct)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Извлечено {Length} символов в батче из файла {FileName}",
                    batchContent.Length, fileName);
            }

            string processedText = ApplyRules(batchContent, options.Rules);

            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("После применения правил в батче: {Length} символов", processedText.Length);
            }

            string header = BuildHeader(fileName, fileSize, state.ChunkIndex, options);
            string contentWithHeader = header + processedText;
            int contentSize = Encoding.UTF8.GetByteCount(contentWithHeader);

            if (contentSize > options.MaxChunkSizeBytes)
            {
                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("Батч файла {FileName} превышает максимальный размер чанка. Разбиваем на части.", fileName);
                }

                state.ChunkIndex = await SplitLargeContentAsync(
                    contentWithHeader, options, fileName,
                    state.Results, state.ChunkIndex, ct);
            }
            else
            {
                if (state.CurrentChunkSize + contentSize > options.MaxChunkSizeBytes && state.CurrentChunkSize > 0)
                {
                    await SaveCurrentChunkAsync(state, options);
                }

                state.CurrentChunkContent.Append(contentWithHeader);
                if (!state.CurrentChunkSourceFiles.Contains(fileName))
                {
                    state.CurrentChunkSourceFiles.Add(fileName);
                }
                state.CurrentChunkSize += contentSize;
            }
        }

        private async Task FinalizeChunkingAsync(ChunkingState state, ChunkingOptions options)
        {
            if (state.CurrentChunkSize > 0)
            {
                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("Сохраняем последний чанк #{ChunkIndex}", state.ChunkIndex);
                }

                await SaveChunkAsync(
                    state.CurrentChunkContent.ToString(),
                    state.CurrentChunkSourceFiles,
                    options.OutputDirectory,
                    state.ChunkIndex,
                    state.Results);
            }
        }

        private async Task SaveCurrentChunkAsync(ChunkingState state, ChunkingOptions options)
        {
            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Текущий чанк заполнен. Сохраняем чанк #{ChunkIndex}", state.ChunkIndex);
            }

            await SaveChunkAsync(
                state.CurrentChunkContent.ToString(),
                state.CurrentChunkSourceFiles,
                options.OutputDirectory,
                state.ChunkIndex,
                state.Results);

            state.CurrentChunkContent.Clear();
            state.CurrentChunkSourceFiles.Clear();
            state.CurrentChunkSize = 0;
            state.ChunkIndex++;
        }

        private async Task<int> SplitLargeContentAsync(
            string content,
            ChunkingOptions chunkingOptions,
            string fileName,
            List<ChunkResult> results,
            int chunkIndex,
            CancellationToken cancellationToken)
        {
            IHeaderFormatter headerFormatter = chunkingOptions.HeaderFormatter ?? _defaultHeaderFormatter;
            string[] lines = content.Split([Environment.NewLine], StringSplitOptions.None);
            StringBuilder currentChunkContent = new();
            long currentChunkSize = 0L;
            int partNumber = 1;
            int totalParts = EstimateTotalParts(content.Length, chunkingOptions.MaxChunkSizeBytes);

            foreach (string line in lines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                byte[] lineBytes = Encoding.UTF8.GetBytes(line + Environment.NewLine);
                int lineSize = lineBytes.Length;

                if (currentChunkSize + lineSize > chunkingOptions.MaxChunkSizeBytes && currentChunkSize > 0)
                {
                    if (_logger?.IsEnabled(LogLevel.Information) == true)
                    {
                        _logger.LogInformation("Сохраняем часть файла {FileName}, часть {PartNumber} из {TotalParts}",
                            fileName, partNumber, totalParts);
                    }

                    ChunkMetadataContext metadataContext = new()
                    {
                        FileName = fileName,
                        OriginalPath = fileName,
                        FileSizeBytes = content.Length,
                        FilePartNumber = partNumber,
                        TotalFileParts = totalParts,
                        ChunkIndex = chunkIndex,
                        GeneratedAt = DateTime.UtcNow
                    };

                    string partHeader = headerFormatter.Format(
                        chunkingOptions.HeaderTemplate ?? string.Empty, metadataContext) + Environment.NewLine;
                    string chunkContent = partHeader + currentChunkContent.ToString();

                    // Передаем имя файла в списке, чтобы SaveChunkAsync мог использовать его расширение
                    await SaveChunkAsync(chunkContent, [fileName], chunkingOptions.OutputDirectory, chunkIndex, results);

                    currentChunkContent.Clear();
                    currentChunkSize = 0;
                    chunkIndex++;
                    partNumber++;
                }

                currentChunkContent.AppendLine(line);
                currentChunkSize += lineSize;
            }

            if (currentChunkSize > 0)
            {
                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("Сохраняем последнюю часть файла {FileName}, часть {PartNumber} из {TotalParts}",
                        fileName, partNumber, totalParts);
                }

                ChunkMetadataContext metadataContext = new()
                {
                    FileName = fileName,
                    OriginalPath = fileName,
                    FileSizeBytes = content.Length,
                    FilePartNumber = partNumber,
                    TotalFileParts = totalParts,
                    ChunkIndex = chunkIndex,
                    GeneratedAt = DateTime.UtcNow
                };

                string partHeader = headerFormatter.Format(
                    chunkingOptions.HeaderTemplate ?? string.Empty, metadataContext) + Environment.NewLine;
                string chunkContent = partHeader + currentChunkContent.ToString();

                await SaveChunkAsync(chunkContent, [fileName], chunkingOptions.OutputDirectory, chunkIndex, results);
                chunkIndex++;
            }

            return chunkIndex;
        }

        /// <summary>
        /// Сохраняет чанк на диск, формируя уникальное имя файла на основе исходного имени и расширения.
        /// </summary>
        private async Task SaveChunkAsync(
            string content,
            List<string> sourceFiles,
            string outputDirectory,
            int chunkIndex,
            List<ChunkResult> results)
        {
            // Берем первый файл из списка как основной для именования (или "merged", если их несколько)
            string primarySourceFile = sourceFiles.FirstOrDefault() ?? "merged_files";

            string sourceNameWithoutExt = Path.GetFileNameWithoutExtension(primarySourceFile);
            string sourceExtension = Path.GetExtension(primarySourceFile).TrimStart('.');

            // Санитизация имени файла: удаляем недопустимые символы для безопасности файловой системы
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitizedName = string.IsNullOrWhiteSpace(sourceNameWithoutExt)
                ? "unknown"
                : new string([.. sourceNameWithoutExt.Where(c => !invalidChars.Contains(c))]);

            // Формируем уникальное имя: chunk_0001_ИмяФайла.docx.txt
            // Это гарантирует, что файлы с одинаковым расширением, но разным содержимым, не перезапишут друг друга.
            string outputFileName = $"chunk_{chunkIndex:D4}_{sanitizedName}.{sourceExtension}.txt";
            string outputPath = Path.Combine(outputDirectory, outputFileName);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Сохранение чанка: {OutputPath}", outputPath);
            }

            await File.WriteAllTextAsync(outputPath, content, Encoding.UTF8);

            FileInfo fileInfo = new(outputPath);
            ChunkResult result = new(
                Guid.NewGuid(),
                outputFileName,
                outputPath,
                fileInfo.Length,
                sourceFiles);

            results.Add(result);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Чанк сохранен: {OutputFileName}, размер: {Size} байт",
                    outputFileName, fileInfo.Length);
            }
        }

        #endregion

        #region Text Processing and Rules

        private string BuildHeader(string fileName, long fileSize, int chunkIndex, ChunkingOptions options)
        {
            if (string.IsNullOrEmpty(options.HeaderTemplate))
            {
                return string.Empty;
            }

            IHeaderFormatter headerFormatter = options.HeaderFormatter ?? _defaultHeaderFormatter;
            ChunkMetadataContext metadataContext = new()
            {
                FileName = fileName,
                OriginalPath = fileName,
                FileSizeBytes = fileSize,
                FilePartNumber = 1,
                TotalFileParts = 1,
                ChunkIndex = chunkIndex,
                GeneratedAt = DateTime.UtcNow
            };

            return headerFormatter.Format(options.HeaderTemplate, metadataContext) + Environment.NewLine;
        }

        private string ApplyRules(string content, IEnumerable<IChunkRule> rules)
        {
            string result = content;

            foreach (IChunkRule rule in rules)
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug("Применение правила: {RuleName}", rule.Name);
                }

                result = rule.Apply(result);
            }

            return result;
        }

        #endregion

        #region Helper Methods

        protected static string GetFileNameFromStream(Stream stream)
        {
            return stream is FileStream fileStream
                ? Path.GetFileName(fileStream.Name)
                : $"unknown_{Guid.NewGuid():N}";
        }

        protected static int EstimateTotalParts(long contentLength, long maxChunkSizeBytes)
        {
            return (int)Math.Ceiling((double)contentLength / maxChunkSizeBytes);
        }

        #endregion

        #region Internal State

        protected sealed class ChunkingState
        {
            public StringBuilder CurrentChunkContent { get; } = new();
            public List<string> CurrentChunkSourceFiles { get; } = [];
            public long CurrentChunkSize { get; set; } = 0;
            public int ChunkIndex { get; set; } = 0;
            public List<ChunkResult> Results { get; } = [];
        }

        #endregion
    }
}