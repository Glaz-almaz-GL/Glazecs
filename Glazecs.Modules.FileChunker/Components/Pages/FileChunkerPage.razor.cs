using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Glazecs.Modules.FileChunker.Abstractions.Models;
using Glazecs.Modules.FileChunker.Abstractions.Rules;
using Glazecs.Modules.FileChunker.Resources.Languages;
using Glazecs.Shared.Core.Platforms;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace Glazecs.Modules.FileChunker.Components.Pages
{
    public partial class FileChunkerPage(
        IEnumerable<IFileChunker> fileChunkers,
        IStringLocalizer<FileChunkerResources> localizer,
        ISnackbar snackbar,
        ILogger<FileChunkerPage>? logger = null) : ComponentBase
    {
        #region State and Properties

        private readonly List<SelectedFileInfo> _selectedFiles = [];
        private List<ChunkResult> _results = [];

        private string? _outputDirectory;
        private string? _sourceDirectory;
        private string _selectedFormat = "all";
        private bool _scanSubfolders;

        private long _maxChunkSizeMB = 20;
        private string _headerTemplate = string.Empty;

        private bool _removePunctuation;
        private bool _useStopWords;
        private string _stopWordsText = string.Empty;

        private bool _isProcessing;
        private CancellationTokenSource? _cts;

        private readonly Dictionary<string, IFileChunker> _chunkerByExtension = BuildChunkerMap(fileChunkers);
        private readonly IStringLocalizer<FileChunkerResources> _localizer = localizer;
        private readonly ISnackbar _snackbar = snackbar;
        private readonly ILogger<FileChunkerPage>? _logger = logger;

        private static readonly Dictionary<string, HashSet<string>> FormatExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["word"] = new(StringComparer.OrdinalIgnoreCase) { ".docx" },
            ["pdf"] = new(StringComparer.OrdinalIgnoreCase) { ".pdf" },
            ["all"] = new(StringComparer.OrdinalIgnoreCase) { ".docx", ".pdf" }
        };

        private List<PlaceholderInfo> _placeholders = [];

        protected override void OnInitialized()
        {
            _placeholders =
            [
                new("{FileName}", _localizer["PlaceholderFileName"].Value),
                new("{OriginalPath}", _localizer["PlaceholderOriginalPath"].Value),
                new("{FilePart}", _localizer["PlaceholderFilePart"].Value),
                new("{TotalParts}", _localizer["PlaceholderTotalParts"].Value),
                new("{FileSize}", _localizer["PlaceholderFileSize"].Value),
                new("{ChunkIndex}", _localizer["PlaceholderChunkIndex"].Value),
                new("{Date}", _localizer["PlaceholderDate"].Value),
                new("{DateTime}", _localizer["PlaceholderDateTime"].Value)
            ];
        }

        #endregion

        #region Initialization

        private static Dictionary<string, IFileChunker> BuildChunkerMap(IEnumerable<IFileChunker> chunkers)
        {
            Dictionary<string, IFileChunker> map = new(StringComparer.OrdinalIgnoreCase);

            foreach (IFileChunker chunker in chunkers)
            {
                foreach (string? extension in chunker.SupportedExtensions.Where(extension => !map.ContainsKey(extension)))
                {
                    map[extension] = chunker;
                }
            }

            return map;
        }

        #endregion

        #region Placeholder Management

        private void InsertPlaceholder(string placeholder)
        {
            _headerTemplate += placeholder;
            StateHasChanged();
        }

        #endregion

        #region Folder Selection and Scanning

        private async Task SelectFolderAsync(Action<string> setPath, string errorKey)
        {
            if (!PlatformSupport.IsFolderPickerSupported)
            {
                string reason = PlatformSupport.GetFolderPickerUnsupportedReason()
                    ?? _localizer["PlatformNotSupported"].Value;
                _snackbar.Add(reason, Severity.Warning);
                return;
            }

            try
            {
#pragma warning disable CA1416
                var result = await CommunityToolkit.Maui.Storage.FolderPicker.Default.PickAsync();
#pragma warning restore CA1416

                if (result.IsSuccessful)
                {
                    setPath(result.Folder.Path);
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при выборе папки");
                _snackbar.Add(_localizer[errorKey, ex.Message].Value, Severity.Error);
            }
        }

        private Task SelectSourceFolder()
        {
            return SelectFolderAsync(path => _sourceDirectory = path, "ErrorSelectFolder");
        }

        private Task SelectOutputFolder()
        {
            return SelectFolderAsync(path => _outputDirectory = path, "ErrorSelectFolder");
        }

        private void ScanSourceFolder()
        {
            if (string.IsNullOrWhiteSpace(_sourceDirectory) || !Directory.Exists(_sourceDirectory))
            {
                _snackbar.Add(_localizer["WarningSelectExistingFolder"].Value, Severity.Warning);
                return;
            }

            if (!FormatExtensions.TryGetValue(_selectedFormat, out HashSet<string>? allowedExtensions))
            {
                _snackbar.Add(_localizer["ErrorUnknownFormat", _selectedFormat].Value, Severity.Error);
                return;
            }

            try
            {
                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger?.LogInformation("Начало сканирования папки: {Path}, формат: {Format}, рекурсия: {Recurse}",
                    _sourceDirectory, _selectedFormat, _scanSubfolders);
                }

                EnumerationOptions enumerationOptions = new()
                {
                    RecurseSubdirectories = _scanSubfolders,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.System
                };

                IEnumerable<FileInfo> files = Directory
                    .EnumerateFiles(_sourceDirectory, "*", enumerationOptions)
                    .Select(path => new FileInfo(path))
                    .Where(file => allowedExtensions.Contains(file.Extension));

                _selectedFiles.Clear();

                foreach (FileInfo fileInfo in files)
                {
                    _selectedFiles.Add(new SelectedFileInfo(
                        fileInfo.Name,
                        fileInfo.FullName,
                        fileInfo.Extension,
                        fileInfo.Length));
                }

                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger?.LogInformation("Найдено файлов: {Count}", _selectedFiles.Count);
                }

                if (_selectedFiles.Count == 0)
                {
                    _snackbar.Add(_localizer["InfoNoFilesInFormat", _selectedFormat].Value, Severity.Info);
                }
                else
                {
                    _snackbar.Add(_localizer["SuccessScan", _selectedFiles.Count].Value, Severity.Success);
                }

                StateHasChanged();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при сканировании папки: {Path}", _sourceDirectory);
                _snackbar.Add(_localizer["ErrorScanFolder", ex.Message].Value, Severity.Error);
            }
        }

        private void RemoveFile(SelectedFileInfo file)
        {
            _selectedFiles.Remove(file);
            StateHasChanged();
        }

        #endregion

        #region Processing Logic

        private async Task ProcessFiles()
        {
            if (_selectedFiles.Count == 0 || string.IsNullOrWhiteSpace(_outputDirectory))
            {
                _snackbar.Add(_localizer["WarningSelectFilesAndFolder"].Value, Severity.Warning);
                return;
            }

            _cts = new CancellationTokenSource();

            try
            {
                _isProcessing = true;
                _results.Clear();
                StateHasChanged();

                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("Начало обработки {Count} файлов", _selectedFiles.Count);
                }

                string normalizedTemplate = Environment.NewLine + _headerTemplate.Trim() + Environment.NewLine;
                List<IChunkRule> rules = BuildRules();

                ChunkingOptions options = new(
                    outputDirectory: _outputDirectory,
                    rules: rules,
                    maxChunkSizeBytes: _maxChunkSizeMB * 1024 * 1024,
                    headerTemplate: string.IsNullOrWhiteSpace(_headerTemplate) ? null : normalizedTemplate
                );

                List<ChunkResult> aggregatedResults = [];

                IEnumerable<IGrouping<string, SelectedFileInfo>> groupedByExtension = _selectedFiles
                    .GroupBy(f => f.Extension, StringComparer.OrdinalIgnoreCase);

                foreach (IGrouping<string, SelectedFileInfo> group in groupedByExtension)
                {
                    if (!_chunkerByExtension.TryGetValue(group.Key, out IFileChunker? chunker))
                    {
                        _snackbar.Add(_localizer["WarningNoChunker", group.Key].Value, Severity.Warning);
                        continue;
                    }

                    IEnumerable<string> filePaths = group.Select(f => f.Path);
                    IReadOnlyCollection<ChunkResult> chunkResults = await chunker.ProcessAsync(
                        filePaths, options, _cts.Token);

                    aggregatedResults.AddRange(chunkResults);
                }

                _results = aggregatedResults;
                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger?.LogInformation("Обработка завершена. Создано чанков: {Count}", _results.Count);
                }

                _snackbar.Add(_localizer["SuccessProcessing", _results.Count].Value, Severity.Success);
            }
            catch (OperationCanceledException ex)
            {
                _logger?.LogInformation(ex, "Обработка отменена пользователем");
                _snackbar.Add(_localizer["OperationCancelled"].Value, Severity.Info);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ошибка при обработке файлов");
                _snackbar.Add(_localizer["ErrorProcessing", ex.Message].Value, Severity.Error);
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                _isProcessing = false;
                StateHasChanged();
            }
        }




        private void CancelProcessing()
        {
            _cts?.Cancel();
        }

        private List<IChunkRule> BuildRules()
        {
            List<IChunkRule> rules = [];

            if (_removePunctuation)
            {
                rules.Add(new RemovePunctuationRule());
            }

            if (_useStopWords && !string.IsNullOrWhiteSpace(_stopWordsText))
            {
                string[] stopWords = _stopWordsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (stopWords.Length > 0)
                {
                    rules.Add(new DictionaryFilterRule(stopWords));
                }
            }

            return rules;
        }

        #endregion

        #region Models

        private sealed record SelectedFileInfo(
            string Name,
            string Path,
            string Extension,
            long Size);

        private sealed record PlaceholderInfo(string Key, string Description);

        #endregion
    }
}