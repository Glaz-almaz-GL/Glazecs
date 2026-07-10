using Glazecs.Modules.FMMS.Abstractions.Interfaces;
using Glazecs.Modules.FMMS.Abstractions.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Glazecs.Modules.FMMS.Services
{
    internal sealed class DirectoryScannerService(ILogger<DirectoryScannerService>? logger = null) : IDirectoryScannerService
    {
        private readonly ILogger<DirectoryScannerService>? _logger = logger;

        public async IAsyncEnumerable<ScannedDirectory> ScanDirectoryAsync(
             string rootPath,
             DirectoryScanningSettings settings,
             IProgress<double> progress,
             [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(rootPath))
            {
                _logger?.LogWarning("The specified directory was not found: \"{Path}\"", rootPath);
                yield break;
            }

            var allDirectories = await Task.Run(async () =>
            {
                return CollectDirectories(new DirectoryInfo(rootPath), settings.IncludeHidden, cancellationToken);
            }, cancellationToken);

            var sortedDirectories = allDirectories.OrderByDescending(d => d.FullName.Length).ToList();

            int totalDirs = sortedDirectories.Count;
            if (totalDirs == 0)
            {
                progress?.Report(100);
                yield break;
            }

            int rootLength = Path.TrimEndingDirectorySeparator(rootPath).Length + 1;
            int processedDirs = 0;

            var dirStats = new Dictionary<string, (long Size, int FilesCount)>(StringComparer.OrdinalIgnoreCase);

            foreach (var dir in sortedDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (currentSizeBytes, currentFilesCount) = await Task.Run(() =>
                {
                    var (size, count) = CalculateDirectoryStats(dir, settings.IncludeHidden, cancellationToken);

                    AggregateSubdirectoriesStats(dir, dirStats, ref size, ref count, settings.IncludeHidden);

                    return (size, count);
                }, cancellationToken).ConfigureAwait(false);

                dirStats[dir.FullName] = (currentSizeBytes, currentFilesCount);

                processedDirs++;
                progress?.Report(CalculateProgress(processedDirs, totalDirs));

                yield return new ScannedDirectory
                {
                    FullPath = dir.FullName,
                    RelativePath = dir.FullName.Length > rootLength ? dir.FullName[rootLength..] : "\\",
                    Size = currentSizeBytes,
                    FilesCount = currentFilesCount
                };
            }
        }

        /// <summary>
        /// Вычисляет прогресс в процентах, избегая деления на ноль.
        /// </summary>
        private static double CalculateProgress(int processedDirs, int totalDirs)
        {
            return totalDirs > 0 ? (double)processedDirs / totalDirs * 100 : 100;
        }

        /// <summary>
        /// Подсчитывает размер и количество файлов в указанной директории (без учета вложенных).
        /// </summary>
        private (long Size, int FilesCount) CalculateDirectoryStats(
            DirectoryInfo dir,
            bool includeHidden,
            CancellationToken ct)
        {
            long size = 0;
            int count = 0;

            try
            {
                foreach (var file in dir.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                {
                    ct.ThrowIfCancellationRequested();

                    if (!includeHidden && (file.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                    {
                        continue;
                    }

                    count++;
                    size += file.Length;
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                _logger?.LogError(ex, "Error reading files: \"{FullName}\"", dir.FullName);
            }

            return (size, count);
        }

        /// <summary>
        /// Суммирует размеры и количество файлов из уже обработанных вложенных директорий.
        /// </summary>
        private void AggregateSubdirectoriesStats(
            DirectoryInfo dir,
            Dictionary<string, (long Size, int FilesCount)> dirStats,
            ref long currentSizeBytes,
            ref int currentFilesCount,
            bool includeHidden)
        {
            try
            {
                foreach (var subDir in dir.EnumerateDirectories())
                {
                    if (!includeHidden && (subDir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        continue;

                    if (dirStats.TryGetValue(subDir.FullName, out var subStats))
                    {
                        currentSizeBytes += subStats.Size;
                        currentFilesCount += subStats.FilesCount;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting subdirectories for summarization: \"{FullName}\"", dir.FullName);
            }
        }

        /// <summary>
        /// Рекурсивно собирает все директории в список.
        /// </summary>
        private List<DirectoryInfo> CollectDirectories(DirectoryInfo rootDir, bool includeHidden, CancellationToken ct)
        {
            var result = new List<DirectoryInfo>();
            var stack = new Stack<DirectoryInfo>();

            if ((rootDir.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden || includeHidden)
            {
                stack.Push(rootDir);
            }

            while (stack.Count > 0)
            {
                ct.ThrowIfCancellationRequested();

                var currentDir = stack.Pop();
                result.Add(currentDir);

                try
                {
                    foreach (var subDir in currentDir.EnumerateDirectories())
                    {
                        if (!includeHidden && (subDir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                            continue;

                        stack.Push(subDir);
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
                {
                    _logger?.LogError(ex, "Enumeration error: \"{FullName}\"", currentDir.FullName);
                }
            }

            return result;
        }
    }
}
