using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Glazecs.Modules.FileChunker.Abstractions.Models;
using Glazecs.Modules.FileChunker.Abstractions.Rules;
using Glazecs.Modules.FileChunker.Resources.Languages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace Glazecs.Modules.FileChunker.Components.Pages
{
    public partial class FileChunkerPage(
        IEnumerable<IFileChunker> fileChunkers,
        IStringLocalizer<FileChunkerResources> localizer) : ComponentBase
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

        private readonly Dictionary<string, IFileChunker> _chunkerByExtension = BuildChunkerMap(fileChunkers);
        private readonly IStringLocalizer<FileChunkerResources> _localizer = localizer;

        private static readonly Dictionary<string, HashSet<string>> FormatExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["word"] = new(StringComparer.OrdinalIgnoreCase) { ".docx" },
            ["pdf"] = new(StringComparer.OrdinalIgnoreCase) { ".pdf" },
            ["all"] = new(StringComparer.OrdinalIgnoreCase) { ".docx", ".pdf" }
        };

        /// <summary>
        /// Список доступных плейсхолдеров для шаблона заголовка.
        /// Описание берётся из локализатора.
        /// </summary>
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
                foreach (string extension in chunker.SupportedExtensions.Where(extension => !map.ContainsKey(extension)))
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

        private async Task SelectSourceFolder()
        {
            try
            {
#pragma warning disable CA1416
                var folderResult = await CommunityToolkit.Maui.Storage.FolderPicker.Default.PickAsync();
#pragma warning restore CA1416

                if (folderResult.IsSuccessful)
                {
                    _sourceDirectory = folderResult.Folder.Path;
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add(_localizer["ErrorSelectFolder", ex.Message].Value, Severity.Error);
            }
        }

        private async Task SelectOutputFolder()
        {
            try
            {
#pragma warning disable CA1416
                var folderResult = await CommunityToolkit.Maui.Storage.FolderPicker.Default.PickAsync();
#pragma warning restore CA1416

                if (folderResult.IsSuccessful)
                {
                    _outputDirectory = folderResult.Folder.Path;
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add(_localizer["ErrorSelectFolder", ex.Message].Value, Severity.Error);
            }
        }

        private void ScanSourceFolder()
        {
            if (string.IsNullOrWhiteSpace(_sourceDirectory) || !Directory.Exists(_sourceDirectory))
            {
                Snackbar.Add(_localizer["WarningSelectExistingFolder"].Value, Severity.Warning);
                return;
            }

            try
            {
                HashSet<string> allowedExtensions = FormatExtensions[_selectedFormat];

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
                    _selectedFiles.Add(new SelectedFileInfo
                    {
                        Name = fileInfo.Name,
                        Path = fileInfo.FullName,
                        Extension = fileInfo.Extension,
                        Size = fileInfo.Length
                    });
                }

                if (_selectedFiles.Count == 0)
                {
                    Snackbar.Add(_localizer["InfoNoFilesInFormat", _selectedFormat].Value, Severity.Info);
                }
                else
                {
                    Snackbar.Add(_localizer["SuccessScan", _selectedFiles.Count].Value, Severity.Success);
                }

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Snackbar.Add(_localizer["ErrorScanFolder", ex.Message].Value, Severity.Error);
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
                Snackbar.Add(_localizer["WarningSelectFilesAndFolder"].Value, Severity.Warning);
                return;
            }

            try
            {
                _isProcessing = true;
                _results = [];
                StateHasChanged();

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
                        Snackbar.Add(_localizer["WarningNoChunker", group.Key].Value, Severity.Warning);
                        continue;
                    }

                    IEnumerable<string> filePaths = group.Select(f => f.Path);
                    IReadOnlyCollection<ChunkResult> chunkResults = await chunker.ProcessAsync(
                        filePaths, options, CancellationToken.None);

                    aggregatedResults.AddRange(chunkResults);
                }

                _results = aggregatedResults;
                Snackbar.Add(_localizer["SuccessProcessing", _results.Count].Value, Severity.Success);
            }
            catch (Exception ex)
            {
                Snackbar.Add(_localizer["ErrorProcessing", ex.Message].Value, Severity.Error);
            }
            finally
            {
                _isProcessing = false;
                StateHasChanged();
            }
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

        private sealed class SelectedFileInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Path { get; set; } = string.Empty;
            public string Extension { get; set; } = string.Empty;
            public long Size { get; set; }
        }

        /// <summary>
        /// Модель плейсхолдера для шаблона заголовка.
        /// </summary>
        private sealed record PlaceholderInfo(string Key, string DescriptionResource);

        #endregion
    }
}