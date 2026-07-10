namespace Glazecs.Modules.FMMS.Abstractions.Interfaces
{
    /// <summary>
    /// Сервис для работы со страницами файлов
    /// </summary>
    public interface IFilePageService
    {
        #region Synchronous Methods

        /// <summary>
        /// Подсчитать количество страниц в PDF-файле
        /// </summary>
        /// <param name="filePath">Путь к PDF-файлу</param>
        /// <returns>Количество страниц</returns>
        /// <exception cref="System.IO.FileNotFoundException">Если файл не найден</exception>
        /// <exception cref="System.UnauthorizedAccessException">
        /// Если файл защищён паролем или недоступен для чтения
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// Если файл повреждён или имеет некорректный формат
        /// </exception>
        int GetPagesCountInPdf(string filePath);

        /// <summary>
        /// Подсчитать количество страниц в PDF-потоке
        /// </summary>
        /// <param name="stream">Поток с данными PDF</param>
        /// <returns>Количество страниц</returns>
        /// <exception cref="System.UnauthorizedAccessException">
        /// Если файл защищён паролем
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// Если файл повреждён или имеет некорректный формат
        /// </exception>
        int GetPagesCountInPdf(Stream stream);

        /// <summary>
        /// Попытаться подсчитать количество страниц в PDF-файле
        /// </summary>
        /// <param name="filePath">Путь к PDF-файлу</param>
        /// <param name="pagesCount">Количество страниц при успехе</param>
        /// <returns><see langword="true"/>, если подсчёт успешен; иначе <see langword="false"/></returns>
        bool TryGetPagesCountInPdf(string filePath, out int pagesCount);

        /// <summary>
        /// Попытаться подсчитать количество страниц в PDF-потоке
        /// </summary>
        /// <param name="stream">Поток с данными PDF</param>
        /// <param name="pagesCount">Количество страниц при успехе</param>
        /// <returns><see langword="true"/>, если подсчёт успешен; иначе <see langword="false"/></returns>
        bool TryGetPagesCountInPdf(Stream stream, out int pagesCount);

        #endregion

        #region Asynchronous Methods

        /// <summary>
        /// Подсчитать количество страниц в PDF-файле
        /// </summary>
        /// <param name="filePath">Путь к PDF-файлу</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Количество страниц</returns>
        /// <exception cref="System.IO.FileNotFoundException">Если файл не найден</exception>
        /// <exception cref="System.UnauthorizedAccessException">
        /// Если файл защищён паролем или недоступен для чтения
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// Если файл повреждён или имеет некорректный формат
        /// </exception>
        Task<int> GetPagesCountInPdfAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Подсчитать количество страниц в PDF-потоке
        /// </summary>
        /// <param name="stream">Поток с данными PDF</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Количество страниц</returns>
        /// <exception cref="System.UnauthorizedAccessException">
        /// Если файл защищён паролем
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// Если файл повреждён или имеет некорректный формат
        /// </exception>
        Task<int> GetPagesCountInPdfAsync(Stream stream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Попытаться подсчитать количество страниц в PDF-файле
        /// </summary>
        /// <param name="filePath">Путь к PDF-файлу</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Кортеж: (успех, количество страниц)</returns>
        Task<(bool Success, int PagesCount)> TryGetPagesCountInPdfAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Попытаться подсчитать количество страниц в PDF-потоке
        /// </summary>
        /// <param name="stream">Поток с данными PDF</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Кортеж: (успех, количество страниц)</returns>
        Task<(bool Success, int PagesCount)> TryGetPagesCountInPdfAsync(Stream stream, CancellationToken cancellationToken = default);

        #endregion
    }
}