using Glazecs.Modules.FMMS.Abstractions.Interfaces;
using Glazecs.Modules.FMMS.Abstractions.Models;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Glazecs.Modules.FMMS.Services
{
    /// <summary>
    /// Сканирование дерева директорий: для каждой директории — суммарный размер и число файлов
    /// вместе со всеми поддиректориями.
    /// </summary>
    /// <remarks>
    /// Дерево обходится один раз: каждая директория перечисляется одним вызовом, который отдаёт
    /// и файлы (их размер уже есть в данных перечисления), и поддиректории. Итоги поддиректорий
    /// складываются в родителя снизу вверх.
    /// </remarks>
    internal sealed class DirectoryScannerService(ILogger<DirectoryScannerService>? logger = null) : IDirectoryScannerService
    {
        private readonly ILogger<DirectoryScannerService>? _logger = logger;

        #region Enumeration Options

        /// <summary>
        /// Опции перечисления содержимого директории с включёнными Hidden.
        /// </summary>
        private static readonly EnumerationOptions EntriesEnumerationOptionsWithHidden = new()
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false,
            BufferSize = 4096
        };

        /// <summary>
        /// Опции перечисления содержимого директории без Hidden (фильтрация на уровне API).
        /// </summary>
        private static readonly EnumerationOptions EntriesEnumerationOptions = new()
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden,
            ReturnSpecialDirectories = false,
            BufferSize = 4096
        };

        #endregion

        /// <summary>
        /// Директория дерева: собственные файлы, затем — итог вместе с поддиректориями.
        /// </summary>
        private sealed class DirectoryNode(string fullName, int parentIndex)
        {
            public string FullName { get; } = fullName;

            /// <summary>
            /// Индекс родителя в списке узлов; -1 у корня.
            /// </summary>
            public int ParentIndex { get; } = parentIndex;

            public long Size { get; set; }

            public int FilesCount { get; set; }
        }

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

            List<DirectoryNode> nodes = await Task.Run(() =>
            {
                return CollectDirectoryTree(new DirectoryInfo(rootPath), settings.IncludeHidden, cancellationToken);
            }, cancellationToken).ConfigureAwait(false);

            if (nodes.Count == 0)
            {
                progress?.Report(100);
                yield break;
            }

            // Более длинный путь — глубже в дереве: дети идут раньше родителей, и к моменту выдачи
            // директории итоги всех её поддиректорий уже сложены в неё
            int[] order = [.. Enumerable.Range(0, nodes.Count).OrderByDescending(i => nodes[i].FullName.Length)];

            int totalDirs = nodes.Count;
            int rootLength = Path.TrimEndingDirectorySeparator(rootPath).Length + 1;
            int processedDirs = 0;
            int lastReportedProgress = -1;
            int index = 1;

            foreach (int nodeIndex in order)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DirectoryNode node = nodes[nodeIndex];

                if (node.ParentIndex >= 0)
                {
                    DirectoryNode parent = nodes[node.ParentIndex];
                    parent.Size += node.Size;
                    parent.FilesCount += node.FilesCount;
                }

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
                    FullPath = node.FullName,
                    RelativePath = node.FullName.Length > rootLength ? node.FullName[rootLength..] : "\\",
                    Size = node.Size,
                    FilesCount = node.FilesCount
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
        /// Обходит дерево от корня и собирает директории с размером и числом их собственных файлов.
        /// </summary>
        /// <remarks>
        /// Корень берётся всегда, даже скрытый: пользователь указал его явно.
        /// </remarks>
        private List<DirectoryNode> CollectDirectoryTree(
            DirectoryInfo rootDir,
            bool includeHidden,
            CancellationToken cancellationToken)
        {
            rootDir.Refresh();

            bool isRootHidden = (int)rootDir.Attributes == -1 ||
                                (rootDir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;

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

            EnumerationOptions options = includeHidden ? EntriesEnumerationOptionsWithHidden : EntriesEnumerationOptions;
            Stack<(DirectoryInfo Directory, int ParentIndex)> stack = new();
            stack.Push((rootDir, -1));

            List<DirectoryNode> result = new(capacity: 256);

            while (stack.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                (DirectoryInfo currentDir, int parentIndex) = stack.Pop();
                DirectoryNode node = new(currentDir.FullName, parentIndex);
                int nodeIndex = result.Count;
                result.Add(node);

                ReadDirectoryEntries(currentDir, nodeIndex, node, options, stack, cancellationToken);
            }

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Собрано директорий: {Count}", result.Count);
            }

            return result;
        }

        /// <summary>
        /// Одним перечислением считает собственные файлы директории и кладёт её поддиректории в стек обхода.
        /// </summary>
        private void ReadDirectoryEntries(
            DirectoryInfo directory,
            int nodeIndex,
            DirectoryNode node,
            EnumerationOptions options,
            Stack<(DirectoryInfo Directory, int ParentIndex)> stack,
            CancellationToken cancellationToken)
        {
            try
            {
                foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos("*", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (entry is FileInfo file)
                    {
                        node.Size += file.Length;
                        node.FilesCount++;
                    }
                    else if (entry is DirectoryInfo subDirectory)
                    {
                        stack.Push((subDirectory, nodeIndex));
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug(ex, "Не удалось прочитать содержимое директории: \"{FullName}\"", directory.FullName);
                }
            }
        }
    }
}
