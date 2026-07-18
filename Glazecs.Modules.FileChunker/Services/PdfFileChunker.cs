using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Glazecs.Modules.FileChunker.Abstractions.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Glazecs.Modules.FileChunker.Services
{
    /// <summary>
    /// Реализация чанкера для документов PDF (.pdf).
    /// Извлекает текст постранично с помощью библиотеки PdfPig.
    /// </summary>
    public sealed class PdfFileChunker(
        ILogger<PdfFileChunker>? logger = null,
        IHeaderFormatter? defaultHeaderFormatter = null) : FileChunkerBase(logger, defaultHeaderFormatter)
    {

        /// <inheritdoc />
        public override string ChunkerName => "PDF";

        /// <inheritdoc />
        public override IReadOnlyCollection<string> SupportedExtensions => [".pdf"];

        /// <inheritdoc />
        protected override async Task ProcessStreamAsync(
            Func<Stream> streamFactory,
            ChunkingOptions options,
            ChunkingState state,
            CancellationToken ct)
        {
            using MemoryStream memoryStream = new();
            using Stream sourceStream = streamFactory();

            string fileName = GetFileNameFromStream(sourceStream);
            await sourceStream.CopyToAsync(memoryStream, ct);

            memoryStream.Position = 0;
            long fileSize = memoryStream.Length;

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Обработка PDF файла: {FileName}, размер: {Size} байт", fileName, fileSize);
            }

            using PdfDocument document = PdfDocument.Open(memoryStream);

            if (document.NumberOfPages == 0)
            {
                _logger?.LogWarning("PDF документ не содержит страниц. Файл: {FileName}", fileName);
                return;
            }

            StringBuilder batchBuffer = new();
            long currentBatchBytes = 0;
            long maxBatchSize = options.MaxChunkSizeBytes;

            foreach (UglyToad.PdfPig.Content.Page page in document.GetPages())
            {
                ct.ThrowIfCancellationRequested();

                string pageText = ContentOrderTextExtractor.GetText(page);

                if (string.IsNullOrWhiteSpace(pageText))
                {
                    if (_logger?.IsEnabled(LogLevel.Debug) == true)
                    {
                        _logger.LogDebug("Страница {PageNumber} файла {FileName} не содержит текста",
                            page.Number, fileName);
                    }
                    continue;
                }

                string pageContent = pageText.TrimEnd() + Environment.NewLine + Environment.NewLine;
                int pageBytes = Encoding.UTF8.GetByteCount(pageContent);

                if (batchBuffer.Length > 0 && currentBatchBytes + pageBytes > maxBatchSize)
                {
                    await ProcessBatchAsync(batchBuffer.ToString(), fileName, fileSize, options, state, ct);
                    batchBuffer.Clear();
                    currentBatchBytes = 0;
                }

                batchBuffer.Append(pageContent);
                currentBatchBytes += pageBytes;
            }

            if (batchBuffer.Length > 0)
            {
                await ProcessBatchAsync(batchBuffer.ToString(), fileName, fileSize, options, state, ct);
            }
        }
    }
}