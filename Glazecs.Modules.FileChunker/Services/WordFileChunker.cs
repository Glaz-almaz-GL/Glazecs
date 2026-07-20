using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Glazecs.Modules.FileChunker.Abstractions.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Glazecs.Modules.FileChunker.Services
{
    /// <summary>
    /// Реализация чанкера для документов Microsoft Word (.docx).
    /// Извлекает текст по абзацам с помощью OpenXml SDK.
    /// </summary>
    public sealed class WordFileChunker(
        ILogger<WordFileChunker>? logger = null,
        IHeaderFormatter? defaultHeaderFormatter = null) : FileChunkerBase(logger, defaultHeaderFormatter)
    {

        /// <inheritdoc />
        public override string Name => "Word";

        /// <inheritdoc />
        public override IReadOnlyCollection<string> SupportedExtensions => [".docx"];

        /// <inheritdoc />
        protected override async Task ProcessStreamAsync(
            Func<Stream> streamFactory,
            ChunkingOptions options,
            ChunkingState state,
            CancellationToken ct)
        {
            using Stream stream = streamFactory();
            string fileName = GetFileNameFromStream(stream);
            long fileSize = stream.Length;

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Обработка Word-файла: {FileName}, размер: {Size} байт", fileName, fileSize);
            }

            using WordprocessingDocument document = WordprocessingDocument.Open(stream, false);
            Body? body = document.MainDocumentPart?.Document?.Body;

            if (body == null)
            {
                _logger?.LogWarning("Word-документ не содержит тела (Body). Файл: {FileName}", fileName);
                return;
            }

            StringBuilder batchBuffer = new();
            long currentBatchBytes = 0;
            long maxBatchSize = options.MaxChunkSizeBytes;

            foreach (string text in body.Elements<Paragraph>().Select(p => p.InnerText))
            {
                ct.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                string line = text + Environment.NewLine;
                int lineBytes = Encoding.UTF8.GetByteCount(line);

                if (batchBuffer.Length > 0 && currentBatchBytes + lineBytes > maxBatchSize)
                {
                    await ProcessBatchAsync(batchBuffer.ToString(), fileName, fileSize, options, state, ct);
                    batchBuffer.Clear();
                    currentBatchBytes = 0;
                }

                batchBuffer.Append(line);
                currentBatchBytes += lineBytes;
            }

            if (batchBuffer.Length > 0)
            {
                await ProcessBatchAsync(batchBuffer.ToString(), fileName, fileSize, options, state, ct);
            }
        }
    }
}