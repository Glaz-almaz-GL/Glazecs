using Glazecs.Modules.FMMS.Abstractions.Interfaces;
using iText.Kernel.Exceptions;
using iText.Kernel.Pdf;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Glazecs.Modules.FMMS.Services
{
    /// <summary>
    /// Сервис для подсчёта страниц в PDF-файлах
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
                using PdfReader pdfReader = new(stream);
                using PdfDocument pdf = new(pdfReader);
                int pagesCount = pdf.GetNumberOfPages();

                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug("Количество страниц в PDF: {PagesCount}", pagesCount);
                }

                return pagesCount;
            }
            catch (BadPasswordException ex)
            {
                throw new UnauthorizedAccessException("PDF-файл защищён паролем", ex);
            }
            catch (PdfException ex)
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
                    _logger.LogDebug("Файл не найден или путь некорректен: \"{FilePath}\"", filePath);
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
                    _logger.LogDebug(ex, "Ошибка доступа к файлу: \"{FilePath}\"", filePath);
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
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug("Поток равен null или не поддерживает чтение");
                }
                return false;
            }

            try
            {
                using PdfReader pdfReader = new(stream);
                using PdfDocument pdf = new(pdfReader);
                pagesCount = pdf.GetNumberOfPages();

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
                    _logger.LogDebug(ex, "Ошибка обработки PDF: {Message}", ex.Message);
                }
                return false;
            }
        }

        #endregion

        #region Asynchronous Methods

        /// <inheritdoc/>
        public Task<int> GetPagesCountInPdfAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PDF-файл не найден", filePath);
            }

            return Task.Run(() => GetPagesCountInPdf(filePath), cancellationToken);
        }

        /// <inheritdoc/>
        public Task<int> GetPagesCountInPdfAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stream);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.Run(() => GetPagesCountInPdf(stream), cancellationToken);
        }

        /// <inheritdoc/>
        public Task<(bool Success, int PagesCount)> TryGetPagesCountInPdfAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug("Файл не найден или путь некорректен: \"{FilePath}\"", filePath);
                }
                return Task.FromResult((false, 0));
            }

            return Task.Run(() =>
            {
                bool success = TryGetPagesCountInPdf(filePath, out int pagesCount);
                return (success, pagesCount);
            }, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<(bool Success, int PagesCount)> TryGetPagesCountInPdfAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream is null || !stream.CanRead)
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug("Поток равен null или не поддерживает чтение");
                }
                return Task.FromResult((false, 0));
            }

            cancellationToken.ThrowIfCancellationRequested();

            return Task.Run(() =>
            {
                bool success = TryGetPagesCountInPdf(stream, out int pagesCount);
                return (success, pagesCount);
            }, cancellationToken);
        }

        #endregion

        #region Private Methods

        private static bool IsPdfProcessingException(Exception ex)
        {
            return ex is BadPasswordException
                || ex is PdfException
                || ex is IOException
                || ex is ArgumentException;
        }

        #endregion
    }
}
