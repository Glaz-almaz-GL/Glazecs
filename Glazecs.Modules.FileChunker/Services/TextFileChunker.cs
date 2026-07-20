using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Glazecs.Modules.FileChunker.Abstractions.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Glazecs.Modules.FileChunker.Services
{
    /// <summary>
    /// Реализация чанкера для текстовых файлов (.txt, .cs, .md и др.).
    /// Извлекает текст построчно, сохраняя целостность строк и соблюдая лимит размера чанка.
    /// </summary>
    public sealed class TextFileChunker(
        ILogger<TextFileChunker>? logger = null,
        IHeaderFormatter? defaultHeaderFormatter = null) : FileChunkerBase(logger, defaultHeaderFormatter)
    {
        /// <inheritdoc />
        public override string Name => "Text";

        /// <inheritdoc />
        public override IReadOnlyCollection<string> SupportedExtensions => [".txt", ".cs", ".md"];

        /// <inheritdoc />
        protected override async Task ProcessStreamAsync(
            Func<Stream> streamFactory,
            ChunkingOptions options,
            ChunkingState state,
            CancellationToken ct)
        {
            using Stream sourceStream = streamFactory();

            // Гарантируем, что чтение начинается с начала потока, если он поддерживает позиционирование
            if (sourceStream.CanSeek)
            {
                sourceStream.Position = 0;
            }

            string fileName = GetFileNameFromStream(sourceStream);
            long fileSize = sourceStream.CanSeek ? sourceStream.Length : 0;

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Обработка текстового файла: {FileName}, размер: {Size} байт", fileName, fileSize);
            }

            // Используем UTF-8 для согласованности с подсчетом байтов
            using StreamReader reader = new(sourceStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);

            StringBuilder batchBuffer = new();
            long currentBatchBytes = 0;
            long maxBatchSize = options.MaxChunkSizeBytes;

            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                ct.ThrowIfCancellationRequested();

                // ReadLineAsync удаляет символы новой строки, добавляем их обратно для сохранения форматирования
                string lineContent = line + Environment.NewLine;
                int lineBytes = Encoding.UTF8.GetByteCount(lineContent);

                // Если добавление текущей строки превысит лимит, и буфер не пустой — сбрасываем буфер в обработку
                if (currentBatchBytes > 0 && currentBatchBytes + lineBytes > maxBatchSize)
                {
                    await ProcessBatchAsync(batchBuffer.ToString(), fileName, fileSize, options, state, ct);
                    batchBuffer.Clear();
                    currentBatchBytes = 0;
                }

                batchBuffer.Append(lineContent);
                currentBatchBytes += lineBytes;
            }

            // Обработка остатка данных в буфере после завершения чтения файла
            if (batchBuffer.Length > 0)
            {
                await ProcessBatchAsync(batchBuffer.ToString(), fileName, fileSize, options, state, ct);
            }
        }
    }
}