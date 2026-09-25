using Glazecs.Modules.FMMS.Abstractions.Interfaces;
using Glazecs.Modules.FMMS.Abstractions.Models;
using Glazecs.Modules.Hash.Abstractions.Enums;
using Glazecs.Modules.Hash.Abstractions.Extensions;
using Glazecs.Modules.Hash.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

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
        /// Размер блока чтения файла при хешировании: крупные блоки быстрее при последовательном чтении.
        /// </summary>
        private const int HashReadBufferSize = 1024 * 1024;

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
        /// <para>При <see cref="HashingSettings.CalculateInParallel"/> одновременно обрабатывается до
        /// <see cref="HashingSettings.MaxDegreeOfParallelism"/> файлов (0 — по числу ядер), но результаты
        /// выдаются строго в порядке обхода — номера файлов идут подряд.</para>
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

            int dirPathLength = Path.TrimEndingDirectorySeparator(directoryPath).Length + 1;
            List<(string AlgorithmName, IHashProvider Provider)> hashProviders = InitializeHashProviders(settings);
            int degreeOfParallelism = GetDegreeOfParallelism(settings.Hashing);

            // Задачи обработки идут в канал в порядке обхода и забираются в том же порядке.
            // Семафор ограничивает число файлов в работе, ёмкость канала — забегание вперёд готовых результатов.
            Channel<Task<ScannedFile?>> pending = Channel.CreateBounded<Task<ScannedFile?>>(
                new BoundedChannelOptions(degreeOfParallelism * 2) { SingleReader = true, SingleWriter = true });

            // Без using: при досрочной остановке задачи файлов ещё могут вызвать Release,
            // а SemaphoreSlim без AvailableWaitHandle освобождать не требуется
            SemaphoreSlim workers = new(degreeOfParallelism);
            using CancellationTokenSource producerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task producer = ProduceFileTasksAsync(filePaths, pending.Writer, workers, dirPathLength, settings, hashProviders, producerCts.Token);

            int processedFiles = 0;

            try
            {
                // Ошибка перечисления файлов приходит сюда же — через завершение канала
                await foreach (Task<ScannedFile?> fileTask in pending.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    ScannedFile? scannedFile = await fileTask.ConfigureAwait(false);

                    if (scannedFile.HasValue)
                    {
                        yield return scannedFile.Value;
                    }

                    processedFiles++;
                    progress?.Report(processedFiles);
                }
            }
            finally
            {
                // Потребитель мог остановиться раньше (отмена, выход из цикла) — останавливаем производителя
                // и дожидаемся его, не пробрасывая отмену: семафор освобождается только после этого
                await producerCts.CancelAsync().ConfigureAwait(false);
                await Task.WhenAny(producer).ConfigureAwait(false);
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
                return Directory.EnumerateFiles(directoryPath, "*", EnumerationOptions);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger?.LogWarning(ex, "Access denied to directory: {DirectoryPath}", directoryPath);
                return null;
            }
        }

        /// <summary>
        /// Число файлов, обрабатываемых одновременно.
        /// </summary>
        private static int GetDegreeOfParallelism(HashingSettings settings)
        {
            if (!settings.CalculateInParallel)
            {
                return 1;
            }

            return settings.MaxDegreeOfParallelism > 0 ? settings.MaxDegreeOfParallelism : Environment.ProcessorCount;
        }

        /// <summary>
        /// Обходит файлы и для каждого запускает обработку в пуле потоков, складывая задачи в канал по порядку.
        /// </summary>
        private Task ProduceFileTasksAsync(
            IEnumerable<string> filePaths,
            ChannelWriter<Task<ScannedFile?>> writer,
            SemaphoreSlim workers,
            int dirPathLength,
            FilesScanningSettings settings,
            IReadOnlyList<(string AlgorithmName, IHashProvider Provider)> hashProviders,
            CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                Exception? error = null;

                try
                {
                    int fileIndex = 0;

                    foreach (string filePath in filePaths)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int index = ++fileIndex;

                        // Место в канале — до захвата слота: иначе слоты держали бы файлы, которых ещё никто не ждёт
                        if (!await writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false))
                        {
                            break;
                        }

                        await workers.WaitAsync(cancellationToken).ConfigureAwait(false);

                        Task<ScannedFile?> fileTask = Task.Run(() =>
                        {
                            try
                            {
                                return TryProcessSingleFile(filePath, dirPathLength, index, settings, hashProviders, cancellationToken);
                            }
                            finally
                            {
                                workers.Release();
                            }
                        }, CancellationToken.None);

                        await writer.WriteAsync(fileTask, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    writer.TryComplete(error);
                }
            }, cancellationToken);
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
        /// <param name="fileIndex">Порядковый номер файла в обходе.</param>
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
        private ScannedFile? TryProcessSingleFile(
            string filePath,
            int dirPathLength,
            int fileIndex,
            FilesScanningSettings settings,
            IReadOnlyList<(string AlgorithmName, IHashProvider Provider)> hashProviders,
            CancellationToken cancellationToken)
        {
            try
            {
                return ProcessFile(filePath, dirPathLength, fileIndex, hashProviders, settings, cancellationToken);
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
        /// <param name="fileIndex">Порядковый номер файла в обходе.</param>
        /// <param name="hashProviders">Список провайдеров хеширования.</param>
        /// <param name="settings">Настройки сканирования (пользовательские расширения архивов, правила подсчёта страниц).</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Объект <see cref="ScannedFile"/> с заполненными метаданными.</returns>
        private ScannedFile ProcessFile(string filePath,
            int dirPathLength,
            int fileIndex,
            IReadOnlyList<(string AlgorithmName, IHashProvider Provider)> hashProviders,
            FilesScanningSettings settings,
            CancellationToken cancellationToken)
        {
            FileInfo fileInfo = new(filePath);
            string fileExtension = fileInfo.Extension.ToLowerInvariant();
            long fileSize = fileInfo.Length;

            string relativeFilePath = GetRelativePath(filePath, dirPathLength);
            int pagesCount = GetPagesCount(fileExtension, filePath, settings.PagesCountCustomRules);
            Dictionary<string, string> hashes = CalculateHashes(settings.Hashing, fileSize, filePath, hashProviders, cancellationToken);

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
        /// <param name="fileExtension">Расширение файла в нижнем регистре.</param>
        /// <param name="filePath">Полный путь к файлу.</param>
        /// <param name="pagesCountCustomRules">Пользовательские правила подсчёта страниц по расширению.</param>
        /// <returns>Количество страниц, <c>-1</c> в случае ошибки чтения PDF, либо <c>0</c>, если подсчёт не применим.</returns>
        /// <remarks>
        /// <para>Для PDF-файлов используется <see cref="IFilePageService.TryGetPagesCountInPdf(string, out int)"/>.</para>
        /// <para>Для остальных расширений проверяется наличие пользовательского правила в
        /// <see cref="FilesScanningSettings.PagesCountCustomRules"/>.</para>
        /// </remarks>
        private int GetPagesCount(string fileExtension, string filePath, Dictionary<string, int> pagesCountCustomRules)
        {
            if (string.IsNullOrEmpty(fileExtension))
            {
                return 0;
            }

            if (fileExtension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return TryGetPdfPagesCount(filePath);
            }
            else if (pagesCountCustomRules.TryGetValue(fileExtension, out int customPages))
            {
                return customPages;
            }

            return 0;
        }

        /// <summary>
        /// Пытается подсчитать количество страниц в PDF-файле.
        /// </summary>
        /// <param name="filePath">Путь к PDF-файлу.</param>
        /// <returns>Количество страниц при успехе, либо <c>-1</c> в случае ошибки.</returns>
        /// <remarks>
        /// При неудаче или исключении записывает ошибку в лог и возвращает <c>-1</c>,
        /// не прерывая процесс сканирования.
        /// </remarks>
        private int TryGetPdfPagesCount(string filePath)
        {
            try
            {
                if (_filePageService.TryGetPagesCountInPdf(filePath, out int pagesCount))
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
        /// Вычисляет хеши содержимого файла всеми настроенными алгоритмами за одно чтение файла.
        /// </summary>
        /// <param name="settings">Настройки хеширования (формат вывода, лимит размера).</param>
        /// <param name="fileSize">Размер файла в байтах.</param>
        /// <param name="filePath">Полный путь к файлу.</param>
        /// <param name="hashProviders">Список провайдеров хеширования.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        /// <returns>Словарь «имя алгоритма → хеш»; пустой, если хешировать нечего или файл не прочитан.</returns>
        /// <remarks>
        /// <para>Каждый прочитанный блок подаётся сразу всем алгоритмам, поэтому файл читается один раз,
        /// сколько бы алгоритмов ни было выбрано.</para>
        /// <para>Ошибка чтения не выбрасывает файл из результатов: он остаётся в таблице без хешей,
        /// ошибка пишется в лог.</para>
        /// </remarks>
        private Dictionary<string, string> CalculateHashes(
            HashingSettings settings,
            long fileSize,
            string filePath,
            IReadOnlyList<(string AlgorithmName, IHashProvider Provider)> hashProviders,
            CancellationToken cancellationToken)
        {
            if (hashProviders.Count == 0 || !IsEligibleForHashing(settings, fileSize))
            {
                return [];
            }

            List<(string AlgorithmName, IIncrementalHasher Hasher)> hashers = new(hashProviders.Count);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(HashReadBufferSize);

            try
            {
                foreach ((string algorithmName, IHashProvider provider) in hashProviders)
                {
                    hashers.Add((algorithmName, provider.CreateIncrementalHasher()));
                }

                // bufferSize: 1 — без внутреннего буфера FileStream: читаем сразу крупными блоками
                using (FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.SequentialScan))
                {
                    int bytesRead;

                    while ((bytesRead = stream.Read(buffer, 0, HashReadBufferSize)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        foreach ((string _, IIncrementalHasher hasher) in hashers)
                        {
                            hasher.Append(buffer, 0, bytesRead);
                        }
                    }
                }

                Dictionary<string, string> results = new(hashers.Count);

                foreach ((string algorithmName, IIncrementalHasher hasher) in hashers)
                {
                    string hash = hasher.GetHash().ToFormattedString(settings.OutputFormat);
                    results[algorithmName] = hash;
                    LogHash(algorithmName, hash);
                }

                return results;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger?.LogError(ex, "Failed to calculate hashes for \"{FilePath}\"", filePath);
                return [];
            }
            finally
            {
                foreach ((string _, IIncrementalHasher hasher) in hashers)
                {
                    hasher.Dispose();
                }

                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Проверяет, подлежит ли файл хешированию с учётом лимита размера.
        /// </summary>
        /// <param name="settings">Настройки хеширования.</param>
        /// <param name="fileSize">Размер файла в байтах.</param>
        /// <returns><see langword="true"/>, если файл не превышает лимит или лимит не задан; иначе <see langword="false"/>.</returns>
        private static bool IsEligibleForHashing(HashingSettings settings, long fileSize)
        {
            return settings.MaxFileSizeBytes <= 0 || fileSize <= settings.MaxFileSizeBytes;
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
