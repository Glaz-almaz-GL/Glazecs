using Glazecs.Modules.FMMS.Abstractions.Interfaces;
using Glazecs.Modules.FMMS.Abstractions.Models;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Glazecs.Modules.FMMS.Services
{
    internal sealed class DirectoryScannerService(ILogger<DirectoryScannerService>? logger = null) : IDirectoryScannerService
    {
        private readonly ILogger<DirectoryScannerService>? _logger = logger;

        #region Enumeration Options

        /// <summary>
        /// Опции обхода директорий с включёнными Hidden.
        /// </summary>
        private static readonly EnumerationOptions DirEnumerationOptionsWithHidden = new()
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false,
            BufferSize = 4096
        };

        /// <summary>
        /// Опции обхода директорий без Hidden (фильтрация на уровне API).
        /// </summary>
        private static readonly EnumerationOptions DirEnumerationOptions = new()
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden,
            ReturnSpecialDirectories = false,
            BufferSize = 4096
        };

        /// <summary>
        /// Опции перечисления файлов с включёнными Hidden.
        /// </summary>
        private static readonly EnumerationOptions FileEnumerationOptionsWithHidden = new()
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            BufferSize = 4096
        };

        /// <summary>
        /// Опции перечисления файлов без Hidden (фильтрация на уровне API).
        /// </summary>
        private static readonly EnumerationOptions FileEnumerationOptions = new()
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden,
            BufferSize = 4096
        };

        #endregion

        public async IAsyncEnumerable<ScannedDirectory> ScanDirectoryAsync(
             string rootPath,
             DirectoryScanningSettings settings,
             IProgress<double>? progress = null,
             [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(rootPath))
            {
                _logger?.LogWarning("Директория не найдена: \"{Path}\"", rootPath);
                yield break;
            }

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Начало сканирования: {Path}, IncludeHidden: {IncludeHidden}",
                rootPath, settings.IncludeHidden);
            }

            List<DirectoryInfo> allDirectories = await Task.Run(() =>
            {
                return CollectDirectories(new DirectoryInfo(rootPath), settings.IncludeHidden, cancellationToken);
            }, cancellationToken).ConfigureAwait(false);

            if (allDirectories.Count == 0)
            {
                progress?.Report(100);
                yield break;
            }

            List<DirectoryInfo> sortedDirectories = [.. allDirectories.OrderByDescending(d => d.FullName.Length)];

            allDirectories.Clear();
            allDirectories.TrimExcess();

            int totalDirs = sortedDirectories.Count;
            int rootLength = Path.TrimEndingDirectorySeparator(rootPath).Length + 1;
            int processedDirs = 0;
            int lastReportedProgress = -1;

            Dictionary<string, (long Size, int FilesCount)> dirStats = new(totalDirs);
            int index = 1;

            foreach (DirectoryInfo dir in sortedDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                (long currentSizeBytes, int currentFilesCount) = await Task.Run(() =>
                {
                    (long size, int count) = CalculateDirectoryStats(dir, settings.IncludeHidden, cancellationToken);
                    AggregateSubdirectoriesStats(dir, dirStats, ref size, ref count);
                    return (size, count);
                }, cancellationToken).ConfigureAwait(false);

                dirStats[dir.FullName] = (currentSizeBytes, currentFilesCount);

                processedDirs++;

                int currentProgress = (int)CalculateProgress(processedDirs, totalDirs);
                if (currentProgress > lastReportedProgress)
                {
                    progress?.Report(currentProgress);
                    lastReportedProgress = currentProgress;
                }

                yield return new ScannedDirectory
                {
                    Id = index,
                    FullPath = dir.FullName,
                    RelativePath = dir.FullName.Length > rootLength ? dir.FullName[rootLength..] : "\\",
                    Size = currentSizeBytes,
                    FilesCount = currentFilesCount
                };

                index++;
            }

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Сканирование завершено. Обработано директорий: {Count}", processedDirs);
            }
        }

        private static double CalculateProgress(int processedDirs, int totalDirs)
        {
            return totalDirs > 0 ? (double)processedDirs / totalDirs * 100 : 100;
        }

        /// <summary>
        /// Подсчитывает размер и количество файлов в указанной директории (без учёта вложенных).
        /// Фильтрация Hidden выполняется на уровне Win32 API через <see cref="EnumerationOptions.AttributesToSkip"/>.
        /// </summary>
        private (long Size, int FilesCount) CalculateDirectoryStats(
            DirectoryInfo dir,
            bool includeHidden,
            CancellationToken ct)
        {
            long size = 0;
            int count = 0;

            EnumerationOptions options = includeHidden ? FileEnumerationOptionsWithHidden : FileEnumerationOptions;

            try
            {
                foreach (FileInfo file in dir.EnumerateFiles("*", options))
                {
                    ct.ThrowIfCancellationRequested();
                    count++;
                    size += file.Length;
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug(ex, "Не удалось прочитать файлы: \"{FullName}\"", dir.FullName);
                }
            }

            return (size, count);
        }

        /// <summary>
        /// Суммирует размеры и количество файлов из уже обработанных вложенных директорий.
        /// Использует <see cref="DirEnumerationOptionsWithHidden"/>, т.к. все необходимые директории
        /// уже присутствуют в <paramref name="dirStats"/> (с учётом их атрибутов Hidden).
        /// </summary>
        private void AggregateSubdirectoriesStats(
            DirectoryInfo dir,
            Dictionary<string, (long Size, int FilesCount)> dirStats,
            ref long currentSizeBytes,
            ref int currentFilesCount)
        {
            try
            {
                // Используем опции с Hidden, т.к. нам нужно найти ВСЕ поддиректории,
                // которые уже были обработаны и сохранены в dirStats
                foreach (DirectoryInfo subDir in dir.EnumerateDirectories("*", DirEnumerationOptionsWithHidden))
                {
                    if (dirStats.TryGetValue(subDir.FullName, out (long Size, int FilesCount) subStats))
                    {
                        currentSizeBytes += subStats.Size;
                        currentFilesCount += subStats.FilesCount;
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug(ex, "Не удалось получить поддиректории для агрегации: \"{FullName}\"", dir.FullName);
                }
            }
        }

        /// <summary>
        /// Рекурсивно собирает все директории в список, начиная с указанной корневой.
        /// Корневая директория всегда добавляется в результат, даже если она имеет атрибут Hidden,
        /// так как пользователь явно указал её для сканирования.
        /// </summary>
        private List<DirectoryInfo> CollectDirectories(
            DirectoryInfo rootDir,
            bool includeHidden,
            CancellationToken cancellationToken)
        {
            rootDir.Refresh();

            bool isRootHidden = (int)rootDir.Attributes == -1 ||
                                (rootDir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;

            Stack<DirectoryInfo> stack = new();

            if ((int)rootDir.Attributes == -1)
            {
                _logger?.LogWarning(
                    "Не удалось получить атрибуты корневой директории {Path}. Добавляем в стек.",
                    rootDir.FullName);
            }
            else if (isRootHidden && !includeHidden && _logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(
                "Корневая директория {Path} имеет атрибут Hidden, но пользователь явно указал её. Добавляем в стек.",
                rootDir.FullName);
            }

            stack.Push(rootDir);

            List<DirectoryInfo> result = new(capacity: 256);

            while (stack.Count > 0)
            {
                CollectSubDirectories(stack, includeHidden, result, cancellationToken);
            }

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Собрано директорий: {Count}", result.Count);
            }

            return result;
        }

        /// <summary>
        /// Извлекает одну директорию из стека и добавляет её в результирующий список,
        /// а её валидные поддиректории помещает обратно в стек для последующей обработки.
        /// </summary>
        /// <remarks>
        /// Фильтрация Hidden выполняется на уровне Win32 API через <see cref="EnumerationOptions.AttributesToSkip"/>.
        /// Дополнительная ручная проверка не требуется.
        /// </remarks>
        private void CollectSubDirectories(
            Stack<DirectoryInfo> stack,
            bool includeHidden,
            List<DirectoryInfo> result,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DirectoryInfo currentDir = stack.Pop();
            result.Add(currentDir);

            EnumerationOptions options = includeHidden ? DirEnumerationOptionsWithHidden : DirEnumerationOptions;

            try
            {
                // Hidden-директории уже отфильтрованы на уровне API при includeHidden = false
                foreach (DirectoryInfo subDir in currentDir.EnumerateDirectories("*", options))
                {
                    stack.Push(subDir);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug(ex, "Не удалось перечислить поддиректории: \"{FullName}\"", currentDir.FullName);
                }
            }
        }
    }
}