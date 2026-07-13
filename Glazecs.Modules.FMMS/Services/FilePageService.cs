using Glazecs.Modules.FMMS.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;

namespace Glazecs.Modules.FMMS.Services
{
    /// <summary>
    /// Сервис для подсчёта страниц в PDF-файлах с использованием легковесной библиотеки UglyToad.PdfPig.
    /// </summary>
    internal sealed class FilePageService(ILogger<FilePageService>? logger = null) : IFilePageService
    {
        private readonly ILogger<FilePageService>? _logger = logger;
        private const int DefaultBufferSize = 81920;

        #region Synchronous Methods

        /// <inheritdoc/>
        public int GetPagesCountInPdf(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PDF-файл не найден", filePath);
            }

            using FileStream stream = new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                DefaultBufferSize,
                useAsync: false);

            return GetPagesCountInPdf(stream);
        }

        /// <inheritdoc/>
        public int GetPagesCountInPdf(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            if (!stream.CanRead)
            {
                throw new ArgumentException("Поток не поддерживает чтение", nameof(stream));
            }

            try
            {
                using PdfDocument document = PdfDocument.Open(stream, new ParsingOptions { UseLenientParsing = true });
                int pagesCount = document.NumberOfPages;

                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug("Количество страниц в PDF: {PagesCount}", pagesCount);
                }

                return pagesCount;
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException("PDF-файл защищён паролем или недоступен", ex);
            }
            catch (Exception ex) when (IsPdfProcessingException(ex))
            {
                throw new InvalidOperationException("PDF-файл повреждён или имеет некорректный формат", ex);
            }
        }

        /// <inheritdoc/>
        public bool TryGetPagesCountInPdf(string filePath, out int pagesCount)
        {
            pagesCount = 0;

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger?.LogDebug("Файл не найден или путь некорректен: \"{FilePath}\"", filePath);
                }

                return false;
            }

            try
            {
                using FileStream stream = new(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    DefaultBufferSize,
                    useAsync: false);

                return TryGetPagesCountInPdf(stream, out pagesCount);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger?.LogDebug(ex, "Ошибка доступа к файлу: \"{FilePath}\"", filePath);
                }

                return false;
            }
        }

        /// <inheritdoc/>
        public bool TryGetPagesCountInPdf(Stream stream, out int pagesCount)
        {
            pagesCount = 0;

            if (stream is null || !stream.CanRead)
            {
                _logger?.LogDebug("Поток равен null или не поддерживает чтение");
                return false;
            }

            try
            {
                using PdfDocument document = PdfDocument.Open(stream, new ParsingOptions { UseLenientParsing = true });
                pagesCount = document.NumberOfPages;

                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug("Количество страниц в PDF: {PagesCount}", pagesCount);
                }

                return true;
            }
            catch (Exception ex) when (IsPdfProcessingException(ex))
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger?.LogDebug(ex, "Ошибка обработки PDF: {Message}", ex.Message);
                }

                return false;
            }
        }

        #endregion

        #region Asynchronous Methods

        /// <inheritdoc/>
        public async Task<int> GetPagesCountInPdfAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            return !File.Exists(filePath)
                ? throw new FileNotFoundException("PDF-файл не найден", filePath)
                : await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return GetPagesCountInPdf(filePath);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<int> GetPagesCountInPdfAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return GetPagesCountInPdf(stream);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<(bool Success, int PagesCount)> TryGetPagesCountInPdfAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger?.LogDebug("Файл не найден или путь некорректен: \"{FilePath}\"", filePath);
                }

                return (false, 0);
            }

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool success = TryGetPagesCountInPdf(filePath, out int pagesCount);
                return (success, pagesCount);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<(bool Success, int PagesCount)> TryGetPagesCountInPdfAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream is null || !stream.CanRead)
            {
                _logger?.LogDebug("Поток равен null или не поддерживает чтение");
                return (false, 0);
            }

            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool success = TryGetPagesCountInPdf(stream, out int pagesCount);
                return (success, pagesCount);
            }, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Определяет, является ли исключение ожидаемым при обработке PDF.
        /// </summary>
        private static bool IsPdfProcessingException(Exception ex)
        {
            return ex is PdfDocumentFormatException
                or IOException
                or ArgumentException
                or UnauthorizedAccessException;
        }

        #endregion
    }
}