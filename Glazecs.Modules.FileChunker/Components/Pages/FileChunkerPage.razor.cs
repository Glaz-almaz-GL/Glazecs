using Glazecs.Modules.FileChunker.Abstractions.Attributes;
using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Glazecs.Modules.FileChunker.Abstractions.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Glazecs.Modules.FileChunker.Components.Pages
{
    public partial class FileChunkerPage : ComponentBase
    {
        #region Injection

        [Inject] private IEnumerable<IFileChunker> FileChunkers { get; set; } = default!;
        [Inject] private IEnumerable<IChunkRule> ChunkRules { get; set; } = default!;
        [Inject] private ISnackbar Snackbar { get; set; } = default!;
        [Inject] private IStringLocalizer<Resources.Languages.FileChunkerResources> L { get; set; } = default!;
        [Inject] private ILogger<FileChunkerPage> Logger { get; set; } = default!;

        #endregion

        #region State and Properties

        private readonly List<SelectedFileInfo> _selectedFiles = [];
        private List<ChunkResult> _results = [];

        private string? _sourceDirectory;
        private string? _outputDirectory;
        private bool _scanSubfolders;
        private long _maxChunkSizeMB = 20;
        private string _headerTemplate = string.Empty;

        private bool _isProcessing;
        private CancellationTokenSource? _cts;

        private IReadOnlyCollection<IFileChunker> SelectedChunkers { get; set; } = [];
        private List<RuleState> RuleStates { get; set; } = [];

        #endregion

        #region Initialization

        protected override void OnInitialized()
        {
            // Инициализируем список состояний правил (по умолчанию выключены)
            RuleStates = [.. ChunkRules.Select(rule => new RuleState(rule, false))];
            base.OnInitialized();
        }

        #endregion

        #region Scanning

        private void ScanSourceFolder()
        {
            if (string.IsNullOrWhiteSpace(_sourceDirectory) || !Directory.Exists(_sourceDirectory))
            {
                Snackbar.Add(L["WarningSelectExistingFolder"].Value, Severity.Warning);
                return;
            }

            if (SelectedChunkers.Count == 0)
            {
                Snackbar.Add(L["WarningSelectAtLeastOneChunker"].Value, Severity.Warning);
                return;
            }

            try
            {
                if (Logger.IsEnabled(LogLevel.Information))
                {
                    Logger.LogInformation("Начало сканирования: {Path}, рекурсия: {Recurse}", _sourceDirectory, _scanSubfolders);
                }

                HashSet<string> allowedExtensions = SelectedChunkers
                    .SelectMany(c => c.SupportedExtensions)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                EnumerationOptions enumerationOptions = new()
                {
                    RecurseSubdirectories = _scanSubfolders,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.System
                };

                IEnumerable<string> files = Directory.EnumerateFiles(_sourceDirectory, "*", enumerationOptions)
                    .Where(path => allowedExtensions.Contains(Path.GetExtension(path)));

                _selectedFiles.Clear();
                foreach (string path in files)
                {
                    FileInfo fileInfo = new(path);
                    _selectedFiles.Add(new SelectedFileInfo(fileInfo.Name, fileInfo.FullName, fileInfo.Extension, fileInfo.Length));
                }

                if (Logger.IsEnabled(LogLevel.Information))
                {
                    Logger.LogInformation("Найдено файлов: {Count}", _selectedFiles.Count);
                }

                Snackbar.Add(_selectedFiles.Count > 0
                ? L["SuccessScan", _selectedFiles.Count].Value
                : L["InfoNoFilesInFormat"].Value,
                _selectedFiles.Count > 0 ? Severity.Success : Severity.Info);

                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Ошибка при сканировании папки: {Path}", _sourceDirectory);
                Snackbar.Add(L["ErrorScanFolder", ex.Message].Value, Severity.Error);
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
                Snackbar.Add(L["WarningSelectFilesAndFolder"].Value, Severity.Warning);
                return;
            }

            _cts = new CancellationTokenSource();

            try
            {
                _isProcessing = true;
                _results.Clear();
                StateHasChanged();

                if (Logger.IsEnabled(LogLevel.Information))
                {
                    Logger.LogInformation("Начало обработки {Count} файлов", _selectedFiles.Count);
                }

                // 1. Формируем список активных правил из НОВОГО списка _ruleStates
                List<IChunkRule> activeRules = [.. RuleStates
                    .Where(state => state.IsEnabled)
                    .Select(state => state.Rule)];

                string? normalizedTemplate = string.IsNullOrWhiteSpace(_headerTemplate)
                    ? null
                    : Environment.NewLine + _headerTemplate.Trim() + Environment.NewLine;

                ChunkingOptions options = new(
                    outputDirectory: _outputDirectory,
                    rules: activeRules,
                    maxChunkSizeBytes: _maxChunkSizeMB * 1024 * 1024,
                    headerTemplate: normalizedTemplate
                );

                List<ChunkResult> aggregatedResults = [];

                IEnumerable<IGrouping<IFileChunker?, string>> filesByChunker = _selectedFiles.GroupBy(
                    file => GetChunkerForExtension(file.Extension),
                    file => file.Path
                );

                foreach (IGrouping<IFileChunker?, string> group in filesByChunker)
                {
                    IFileChunker? chunker = group.Key;
                    if (chunker == null)
                    {
                        Logger.LogWarning("Не найден чанкер для файлов с расширением {Extension}", group.First().Split('.')[^1]);
                        continue;
                    }

                    if (Logger.IsEnabled(LogLevel.Information))
                    {
                        Logger.LogInformation("Обработка {Count} файлов через {ChunkerName}", group.Count(), chunker.Name);
                    }

                    IReadOnlyCollection<ChunkResult> chunkResults = await chunker.ProcessAsync(group, options, _cts.Token);
                    aggregatedResults.AddRange(chunkResults);
                }

                _results = aggregatedResults;

                if (Logger.IsEnabled(LogLevel.Information))
                {
                    Logger.LogInformation("Обработка завершена. Создано чанков: {Count}", _results.Count);
                }

                Snackbar.Add(L["SuccessProcessing", _results.Count].Value, Severity.Success);
            }
            catch (OperationCanceledException ex)
            {
                Logger.LogInformation(ex, "Обработка отменена пользователем");
                Snackbar.Add(L["OperationCancelled"].Value, Severity.Info);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Критическая ошибка при обработке файлов");
                Snackbar.Add(L["ErrorProcessing", ex.Message].Value, Severity.Error);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _isProcessing = false;
                StateHasChanged();
            }
        }

        private void CancelProcessing()
        {
            _cts?.Cancel();
        }

        private IFileChunker? GetChunkerForExtension(string extension)
        {
            return SelectedChunkers.FirstOrDefault(c =>
                c.SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
        }

        #endregion

        #region Helper Methods

        private static bool TryGetEditorComponentType(IChunkRule rule, [NotNullWhen(true)] out Type? editorType)
        {
            ChunkRuleEditorAttribute? attribute = rule.GetType()
                .GetCustomAttribute<ChunkRuleEditorAttribute>();

            editorType = attribute?.EditorComponentType;
            return editorType != null;
        }

        #endregion

        #region Models

        private sealed record SelectedFileInfo(
            string Name,
            string Path,
            string Extension,
            long Size);

        private sealed record RuleState
        {
            public IChunkRule Rule { get; set; }
            public bool IsEnabled { get; set; }

            public RuleState(IChunkRule rule, bool isEnabled)
            {
                Rule = rule;
                IsEnabled = isEnabled;
            }
        }

        #endregion
    }
}