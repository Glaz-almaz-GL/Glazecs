using Glazecs.Modules.FMMS.Abstractions.Interfaces;
using Glazecs.Modules.FMMS.Abstractions.Models;
using Glazecs.Modules.Hash.Abstractions.Enums;
using Glazecs.Modules.Hash.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Glazecs.Modules.FMMS.Services
{
    /// <summary>
    /// Сервис для рекурсивного сканирования директорий с вычислением метаданных файлов,
    /// подсчётом страниц и вычислением хешей.
    /// </summary>
    /// <remarks>
    /// <para>Поддерживает асинхронную потоковую обработку файлов через <see cref="IAsyncEnumerable{T}"/>.</para>
    /// <para>Настройки сканирования (алгоритмы хеширования, лимиты размера, параллелизм)
    /// задаются через <see cref="FilesScanningSettings"/>.</para>
    /// <para>Провайдеры хешей получаются из <see cref="IHashProviderFactory"/>,
    /// что позволяет динамически добавлять новые алгоритмы без изменения кода сервиса.</para>
    /// </remarks>
    internal sealed class FileScannerService(
        IHashProviderFactory hashProviderFactory,
        IFilePageService filePageService,
        ILogger<FileScannerService>? logger = null) : IFileScannerService
    {
        private readonly IHashProviderFactory _hashProviderFactory = hashProviderFactory;
        private readonly IFilePageService _filePageService = filePageService;
        private readonly ILogger<FileScannerService>? _logger = logger;

        /// <summary>
        /// Опции перечисления файлов при рекурсивном сканировании директорий.
        /// </summary>
        /// <remarks>
        /// <para><see cref="EnumerationOptions.RecurseSubdirectories"/>: включён рекурсивный обход поддиректорий.</para>
        /// <para><see cref="EnumerationOptions.IgnoreInaccessible"/>: недоступные директории пропускаются без исключения.</para>
        /// <para><see cref="EnumerationOptions.AttributesToSkip"/>: репарс-пойнты (симлинки, junction) игнорируются
        /// для предотвращения зацикливания.</para>
        /// </remarks>
        private static readonly EnumerationOptions EnumerationOptions = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false
        };

        #region Public API

        /// <summary>
        /// Рекурсивно сканирует указанную директорию и возвращает метаданные найденных файлов
        /// в виде асинхронного потока.
        /// </summary>
        /// <param name="directoryPath">Путь к корневой директории для сканирования.</param>
        /// <param name="settings">Настройки сканирования: алгоритмы хеширования, лимиты, правила подсчёта страниц.</param>
        /// <param name="progress">Прогресс-репортёр для отслеживания количества обработанных файлов.</param>
        /// <param name="cancellationToken">Токен отмены для прерывания операции сканирования.</param>
        /// <returns>Асинхронный поток объектов <see cref="ScannedFile"/> с метаданными каждого найденного файла.</returns>
        /// <remarks>
        /// <para>Метод работает лениво: файлы обрабатываются по мере перечисления,
        /// что позволяет начинать обработку до завершения полного обхода директории.</para>
        /// <para>При ошибке доступа к файлу или директории операция не прерывается —
        /// проблемный элемент пропускается с записью в лог.</para>
        /// <para>Общее количество файлов заранее неизвестно, поэтому <paramref name="progress"/>
        /// возвращает абсолютное число обработанных файлов, а не процент.</para>
        /// </remarks>
        public async IAsyncEnumerable<ScannedFile> ScanDirectoryAsync(
            string directoryPath,
            FilesScanningSettings settings,
            IProgress<int>? progress = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            IEnumerable<string>? filePaths = TryEnumerateFiles(directoryPath);
            if (filePaths is null)
            {
                yield break;
            }

            int processedFiles = 0;
            int fileIndex = 0;
            int dirPathLength = Path.TrimEndingDirectorySeparator(directoryPath).Length + 1;
            List<(string AlgorithmName, IHashProvider Provider)> hashProviders = InitializeHashProviders(settings);

            foreach (string filePath in filePaths)
            {
                fileIndex++;

                cancellationToken.ThrowIfCancellationRequested();

                ScannedFile? scannedFile = await TryProcessSingleFileAsync(
                    filePath, dirPathLength, fileIndex, settings, hashProviders, cancellationToken).ConfigureAwait(false);

                if (scannedFile.HasValue)
                {
                    yield return scannedFile.Value;
                }

                processedFiles++;
                progress?.Report(processedFiles);
            }
        }

        #endregion

        #region Scanning Helpers

        /// <summary>
        /// Безопасно получает ленивое перечисление файлов в указанной директории.
        /// </summary>
        /// <param name="directoryPath">Путь к директории для перечисления.</param>
        /// <returns>Перечисление путей к файлам, либо <see langword="null"/>, если доступ к директории запрещён.</returns>
        /// <remarks>
        /// При возникновении <see cref="UnauthorizedAccessException"/> возвращает <see langword="null"/>
        /// и записывает предупреждение в лог, не прерывая выполнение.
        /// </remarks>
        private IEnumerable<string>? TryEnumerateFiles(string directoryPath)
        {
            try
            {
                return Directory.EnumerateFiles(directoryPath, "*.*", EnumerationOptions);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger?.LogWarning(ex, "Access denied to directory: {DirectoryPath}", directoryPath);
                return null;
            }
        }

        /// <summary>
        /// Инициализирует список провайдеров хеширования на основе настроек сканирования.
        /// </summary>
        /// <param name="settings">Настройки сканирования, содержащие список требуемых алгоритмов.</param>
        /// <returns>Список кортежей, содержащих имя алгоритма и соответствующий провайдер.</returns>
        /// <remarks>
        /// <para>Провайдеры запрашиваются из <see cref="IHashProviderFactory"/> по имени алгоритма.</para>
        /// <para>Если провайдер для указанного алгоритма не зарегистрирован,
        /// он пропускается с записью предупреждения в лог.</para>
        /// <para>Список провайдеров инициализируется один раз перед началом обработки файлов
        /// для повышения производительности.</para>
        /// </remarks>
        private List<(string AlgorithmName, IHashProvider Provider)> InitializeHashProviders(FilesScanningSettings settings)
        {
            List<(string, IHashProvider)> providers = [];

            foreach (string algorithmName in settings.Hashing.AlgorithmsToCalculate)
            {
                if (_hashProviderFactory.TryGetProvider(algorithmName, out IHashProvider? provider) && provider is not null)
                {
                    providers.Add((algorithmName, provider));
                }
                else
                {
                    _logger?.LogWarning("Hash provider for algorithm '{Algorithm}' is not registered.", algorithmName);
                }
            }

            return providers;
        }

        /// <summary>
        /// Пытается обработать один файл: получить метаданные и вычислить хеши.
        /// </summary>
        /// <param name="filePath">Полный путь к файлу.</param>
        /// <param name="dirPathLength">Длина пути корневой директории (для вычисления относительного пути).</param>
        /// <param name="settings">Настройки сканирования.</param>
        /// <param name="hashProviders">Список провайдеров хеширования для вычисления хешей.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Объект <see cref="ScannedFile"/> с метаданными, либо <see langword="null"/> в случае ошибки.</returns>
        /// <remarks>
        /// <para>Исключения <see cref="UnauthorizedAccessException"/> и <see cref="IOException"/>
        /// перехватываются и логируются как ошибки — файл пропускается.</para>
        /// <para><see cref="OperationCanceledException"/> пробрасывается для корректной остановки
        /// асинхронного потока.</para>
        /// <para>Все прочие исключения перехватываются и логируются как непредвиденные ошибки.</para>
        /// </remarks>
        private async Task<ScannedFile?> TryProcessSingleFileAsync(
            string filePath,
            int dirPathLength,
            int fileIndex,
            FilesScanningSettings settings,
            IReadOnlyList<(string AlgorithmName, IHashProvider Provider)> hashProviders,
            CancellationToken cancellationToken = default)
        {
            try
            {
                ScannedFile scannedFile = await ProcessFileAsync(filePath, dirPathLength, fileIndex, hashProviders, settings, cancellationToken).ConfigureAwait(false);
                return scannedFile;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                _logger?.LogError(ex, "File skipped due to IO/Access error: \"{FilePath}\".", filePath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error processing file: \"{FilePath}\".", filePath);
            }

            return null;
        }

        #endregion

        #region File Processing

        /// <summary>
        /// Извлекает метаданные файла: относительный путь, расширение, размер, признак архива, количество страниц.
        /// </summary>
        /// <param name="filePath">Полный путь к файлу.</param>
        /// <param name="dirPathLength">Длина пути корневой директории (для вычисления относительного пути).</param>
        /// <param name="settings">Настройки сканирования (пользовательские расширения архивов, правила подсчёта страниц).</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Объект <see cref="ScannedFile"/> с заполненными метаданными.</returns>
        private async Task<ScannedFile> ProcessFileAsync(string filePath,
            int dirPathLength,
            int fileIndex,
            IReadOnlyList<(string AlgorithmName, IHashProvider Provider)> hashProviders,
            FilesScanningSettings settings,
            CancellationToken cancellationToken = default)
        {
            FileInfo fileInfo = new(filePath);
            string fileExtension = fileInfo.Extension.ToLowerInvariant();
            long fileSize = fileInfo.Length;

            string relativeFilePath = GetRelativePath(filePath, dirPathLength);
            int pagesCount = await GetPagesCountAsync(fileExtension, filePath, settings, cancellationToken).ConfigureAwait(false);
            Dictionary<string, string> hashes = await CalculateHashesAsync(settings, fileSize, filePath, hashProviders, cancellationToken).ConfigureAwait(false);

            ScannedFile result = new()
            {
                Id = fileIndex,
                Name = relativeFilePath,
                Extension = fileExtension,
                FullPath = fileInfo.FullName,
                Size = fileSize,
                PagesCount = pagesCount,
                Hashes = hashes,
                IsArchive = settings.CustomArchiveExtensions.Contains(fileInfo.Extension)
            };

            return result;
        }

        /// <summary>
        /// Вычисляет относительный путь файла относительно корневой директории.
        /// </summary>
        /// <param name="filePath">Полный путь к файлу.</param>
        /// <param name="dirPathLength">Длина пути корневой директории (включая завершающий разделитель).</param>
        /// <returns>Относительный путь файла, либо пустая строка, если файл находится в корне.</returns>
        private static string GetRelativePath(string filePath, int dirPathLength)
        {
            return filePath.Length > dirPathLength ? filePath[dirPathLength..] : string.Empty;
        }

        /// <summary>
        /// Определяет количество страниц файла на основе его расширения.
        /// </summary>
        /// <param name="file">Объект файла с метаданными (используется расширение).</param>
        /// <param name="filePath">Полный путь к файлу.</param>
        /// <param name="settings">Настройки сканирования (пользовательские правила подсчёта страниц).</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Количество страниц, <c>-1</c> в случае ошибки чтения PDF, либо <c>0</c>, если подсчёт не применим.</returns>
        /// <remarks>
        /// <para>Для PDF-файлов используется <see cref="IFilePageService.TryGetPagesCountInPdfAsync"/>.</para>
        /// <para>Для остальных расширений проверяется наличие пользовательского правила в
        /// <see cref="FilesScanningSettings.PagesCountCustomRules"/>.</para>
        /// </remarks>
        private async Task<int> GetPagesCountAsync(string fileExtension, string filePath, FilesScanningSettings settings, CancellationToken cancellationToken = default)
        {
            if (fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return await TryGetPdfPagesCountAsync(filePath, cancellationToken).ConfigureAwait(false);
            }
            else if (settings.PagesCountCustomRules.TryGetValue(fileExtension, out int customPages))
            {
                return customPages;
            }

            return 0;
        }

        /// <summary>
        /// Пытается подсчитать количество страниц в PDF-файле.
        /// </summary>
        /// <param name="filePath">Путь к PDF-файлу.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Количество страниц при успехе, либо <c>-1</c> в случае ошибки.</returns>
        /// <remarks>
        /// При неудаче или исключении записывает ошибку в лог и возвращает <c>-1</c>,
        /// не прерывая процесс сканирования.
        /// </remarks>
        private async Task<int> TryGetPdfPagesCountAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                (bool success, int pagesCount) = await _filePageService.TryGetPagesCountInPdfAsync(filePath, cancellationToken).ConfigureAwait(false);

                if (success)
                {
                    LogPdfPagesSuccess(filePath, pagesCount);
                    return pagesCount;
                }

                _logger?.LogWarning("Failed to get PDF pages count for \"{FilePath}\".", filePath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "File page count error for \"{FilePath}\"; Returned -1", filePath);
            }

            return -1;
        }

        /// <summary>
        /// Записывает в лог успешный результат подсчёта страниц PDF-файла.
        /// </summary>
        /// <param name="filePath">Путь к PDF-файлу.</param>
        /// <param name="count">Количество страниц.</param>
        private void LogPdfPagesSuccess(string filePath, int count)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("PDF pages count for {FilePath}: {Count}", filePath, count);
            }
        }

        #endregion

        #region Hashing

        /// <summary>
        /// Вычисляет хеши файла с использованием настроенных алгоритмов.
        /// </summary>
        /// <param name="settings">Настройки сканирования (формат вывода, параллелизм, лимит размера).</param>
        /// <param name="scannedFile">Объект файла, в который будут записаны вычисленные хеши.</param>
        /// <param name="filePath">Полный путь к файлу.</param>
        /// <param name="hashProviders">Список провайдеров хеширования.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <remarks>
        /// <para>Если список провайдеров пуст или файл превышает лимит размера — метод завершается без действий.</para>
        /// <para>При включённом <see cref="HashingSettings.CalculateInParallel"/> и наличии нескольких алгоритмов
        /// хеши вычисляются параллельно через <see cref="Task.WhenAll"/>.</para>
        /// <para>Результаты записываются в словарь <see cref="ScannedFile.Hashes"/>
        /// с ключом, равным имени алгоритма.</para>
        /// </remarks>
        private async Task<Dictionary<string, string>> CalculateHashesAsync(
            FilesScanningSettings settings,
            long fileSize,
            string filePath,
            IReadOnlyList<(string AlgorithmName, IHashProvider Provider)> hashProviders,
            CancellationToken cancellationToken = default)
        {
            if (hashProviders.Count == 0 || !IsEligibleForHashing(settings, fileSize))
            {
                return [];
            }

            if (settings.Hashing.CalculateInParallel && hashProviders.Count > 1)
            {
                // Parallel
                return await CalculateHashesInParallelAsync(hashProviders, filePath, settings.Hashing.OutputFormat, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Not Parallel
                return await CalculateHashesSequentiallyAsync(hashProviders, filePath, settings.Hashing.OutputFormat, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Проверяет, подлежит ли файл хешированию с учётом лимита размера.
        /// </summary>
        /// <param name="settings">Настройки хеширования.</param>
        /// <param name="scannedFile">Объект файла с информацией о размере.</param>
        /// <returns><see langword="true"/>, если файл не превышает лимит или лимит не задан; иначе <see langword="false"/>.</returns>
        private static bool IsEligibleForHashing(FilesScanningSettings settings, long fileSize)
        {
            return settings.Hashing.MaxFileSizeBytes <= 0 || fileSize <= settings.Hashing.MaxFileSizeBytes;
        }

        /// <summary>
        /// Вычисляет хеши файла параллельно для всех настроенных алгоритмов.
        /// </summary>
        /// <param name="providers">Список провайдеров хеширования.</param>
        /// <param name="filePath">Полный путь к файлу.</param>
        /// <param name="format">Формат вывода хеша (например, LowerHex, UpperHex).</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <remarks>
        /// <para>Все алгоритмы запускаются одновременно через <see cref="Task.WhenAll"/>.</para>
        /// <para>Каждый провайдер открывает файл независимо — это безопасно для SSD,
        /// но может вызвать троттлинг на HDD.</para>
        /// <para>Ошибки в одном алгоритме не влияют на вычисление остальных.</para>
        /// </remarks>
        private async Task<Dictionary<string, string>> CalculateHashesInParallelAsync(
            IReadOnlyList<(string AlgorithmName, IHashProvider Provider)> providers,
            string filePath,
            HashOutputFormat format,
            CancellationToken cancellationToken = default)
        {
            Dictionary<string, string> results = [];

            IEnumerable<Task<(string AlgorithmName, string Hash, bool Success)>> tasks = providers.Select(async p =>
            {
                (string? hash, bool success) = await ComputeSingleHashAsync(
                    p.Provider, p.AlgorithmName, filePath, format, cancellationToken)
                    .ConfigureAwait(false);

                return (p.AlgorithmName, Hash: hash, Success: success);
            });

            (string AlgorithmName, string Hash, bool Success)[] taskResults = await Task.WhenAll(tasks).ConfigureAwait(false);

            foreach ((string AlgorithmName, string Hash, bool Success) result in taskResults)
            {
                if (result.Success)
                {
                    results[result.AlgorithmName] = result.Hash;
                    LogHash(result.AlgorithmName, result.Hash);
                }
            }

            return results;
        }

        /// <summary>
        /// Вычисляет хеши файла последовательно для всех настроенных алгоритмов.
        /// </summary>
        /// <param name="providers">Список провайдеров хеширования.</param>
        /// <param name="filePath">Полный путь к файлу.</param>
        /// <param name="format">Формат вывода хеша.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <remarks>
        /// <para>Алгоритмы выполняются один за другим в порядке регистрации.</para>
        /// <para>На каждой итерации проверяется токен отмены для быстрого прерывания.</para>
        /// </remarks>
        private async Task<Dictionary<string, string>> CalculateHashesSequentiallyAsync(
            IReadOnlyList<(string AlgorithmName, IHashProvider Provider)> providers,
            string filePath,
            HashOutputFormat format,
            CancellationToken cancellationToken = default)
        {
            Dictionary<string, string> results = [];

            foreach ((string AlgorithmName, IHashProvider Provider) in providers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                (string Hash, bool Success) = await ComputeSingleHashAsync(Provider, AlgorithmName, filePath, format, cancellationToken).ConfigureAwait(false);

                if (Success)
                {
                    results[AlgorithmName] = Hash;
                    LogHash(AlgorithmName, Hash);
                }
            }

            return results;
        }

        /// <summary>
        /// Вычисляет хеш файла одним алгоритмом с обработкой ошибок.
        /// </summary>
        /// <param name="provider">Провайдер хеширования.</param>
        /// <param name="algorithmName">Имя алгоритма (используется для логирования и ключа в словаре).</param>
        /// <param name="filePath">Полный путь к файлу.</param>
        /// <param name="format">Формат вывода хеша.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Кортеж: имя алгоритма, вычисленный хеш (или пустая строка при ошибке), флаг успешности.</returns>
        /// <remarks>
        /// При возникновении исключения логирует ошибку и возвращает кортеж с <c>Success = false</c>,
        /// не прерывая выполнение остальных алгоритмов.
        /// </remarks>
        private async Task<(string Hash, bool Success)> ComputeSingleHashAsync(
            IHashProvider provider,
            string algorithmName,
            string filePath,
            HashOutputFormat format,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string hash = await provider.CalculateAsync(filePath, format, cancellationToken: cancellationToken).ConfigureAwait(false);
                return (hash, true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogError(ex, "Failed to calculate {Algorithm} for \"{FilePath}\"", algorithmName, filePath);
                return (string.Empty, false);
            }
        }

        /// <summary>
        /// Записывает в лог успешный результат вычисления хеша.
        /// </summary>
        /// <param name="algorithm">Имя алгоритма хеширования.</param>
        /// <param name="hash">Вычисленное значение хеша.</param>
        private void LogHash(string algorithm, string hash)
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("{Algorithm} hash calculated: \"{Hash}\"", algorithm, hash);
            }
        }

        #endregion
    }
}
