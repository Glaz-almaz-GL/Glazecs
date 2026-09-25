using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Glazecs.Modules.FileChunker.Abstractions.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Glazecs.Modules.FileChunker.Services
{
    /// <summary>
    /// Реализация чанкера для JSON-файлов. Читает JSON-файл построчно, накапливает строки в буфере и создает чанки, обеспечивая корректность JSON-структуры.
    /// </summary>
    public sealed class JsonFileChunker(
        ILogger<TextFileChunker>? logger = null,
        IHeaderFormatter? defaultHeaderFormatter = null) : FileChunkerBase(logger, defaultHeaderFormatter)
    {
        public override string Name => "JSON";

        public override IReadOnlyCollection<string> SupportedExtensions => [".json", ".jsonc"];

        protected override async Task ProcessStreamAsync(Func<Stream> streamFactory, ChunkingOptions options, ChunkingState state, CancellationToken ct)
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

            // Json требует корректного формата, поэтому мы будем читать файл построчно и накапливать строки в буфере до достижения максимального размера чанка
            // Но для корректного JSON мы должны убедиться, что мы не разрываем объекты или массивы на части. Поэтому мы будем накапливать строки до тех пор, пока не достигнем максимального размера чанка, а затем проверим, можем ли мы безопасно завершить текущий чанк.
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
                if (currentBatchBytes > 0 && currentBatchBytes + lineBytes > maxBatchSize && IsValidJson(batchBuffer.ToString()))
                {
                    await ProcessBatchAsync(batchBuffer.ToString(), fileName, fileSize, options, state, ct);
                    batchBuffer.Clear();
                    currentBatchBytes = 0;
                }

                batchBuffer.Append(lineContent);
                currentBatchBytes += lineBytes;
            }

            // Обработка остатка данных в буфере после завершения чтения файла
            await ProcessBatchAsync(batchBuffer.ToString(), fileName, fileSize, options, state, ct);
        }

        private static bool IsValidJson(string input)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(input);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
