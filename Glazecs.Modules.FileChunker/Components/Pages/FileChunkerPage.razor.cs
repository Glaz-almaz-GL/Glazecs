using CommunityToolkit.Maui.Storage;
using Glazecs.Modules.FileChunker.Abstractions.Interfaces;
using Glazecs.Modules.FileChunker.Abstractions.Models;
using Glazecs.Modules.FileChunker.Abstractions.Rules;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Glazecs.Modules.FileChunker.Components.Pages
{
    public partial class FileChunkerPage : ComponentBase
    {
        #region State and Properties

        private readonly List<SelectedFileInfo> _selectedFiles = [];
        private IReadOnlyCollection<ChunkResult> _results = [];

        private string? _outputDirectory;
        private long _maxChunkSizeMB = 20;
        private string _headerTemplate = string.Empty;

        private bool _removePunctuation;
        private bool _useStopWords;
        private string _stopWordsText = string.Empty;

        private readonly FilePickerFileType customFileType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.WinUI, new[] { ".docx" } },
            { DevicePlatform.macOS, new[] { "docx" } },
            { DevicePlatform.iOS, new[] { "org.openxmlformats.wordprocessing.document" } },
            { DevicePlatform.Android, new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" } }
        });

        private bool _isProcessing;

        /// <summary>
        /// Список доступных плейсхолдеров для шаблона заголовка с их описаниями.
        /// </summary>
        private readonly List<PlaceholderInfo> _placeholders =
        [
            new("{FileName}", "Имя файла (без пути)"),
            new("{OriginalPath}", "Полный исходный путь к файлу"),
            new("{FilePart}", "Номер текущей части файла"),
            new("{TotalParts}", "Общее количество частей файла"),
            new("{FileSize}", "Размер файла в байтах (с разделителями)"),
            new("{ChunkIndex}", "Индекс итогового чанка"),
            new("{Date}", "Дата генерации (yyyy-MM-dd)"),
            new("{DateTime}", "Дата и время генерации (yyyy-MM-dd HH:mm:ss)")
        ];

        #endregion

        #region Placeholder Management

        /// <summary>
        /// Вставляет указанный плейсхолдер в конец поля шаблона заголовка.
        /// </summary>
        /// <param name="placeholder">Текст плейсхолдера для вставки.</param>
        private void InsertPlaceholder(string placeholder)
        {
            if (string.IsNullOrEmpty(_headerTemplate))
            {
                _headerTemplate = placeholder;
            }
            else
            {
                _headerTemplate += placeholder;
            }

            StateHasChanged();
        }

        #endregion

        #region File and Folder Selection

        private async Task SelectFiles()
        {
            try
            {
                IEnumerable<FileResult>? result = await FilePicker.Default.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "Выберите документы Word",
                    FileTypes = customFileType
                });

                if (result != null)
                {
                    foreach (string? fullPath in result.Select(r => r.FullPath))
                    {
                        FileInfo fileInfo = new(fullPath);
                        if (!_selectedFiles.Any(f => f.Path == fullPath))
                        {
                            _selectedFiles.Add(new SelectedFileInfo
                            {
                                Name = fileInfo.Name,
                                Path = fullPath,
                                Extension = fileInfo.Extension,
                                Size = fileInfo.Length
                            });
                        }
                    }
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Ошибка при выборе файлов: {ex.Message}", Severity.Error);
            }
        }

        private void RemoveFile(SelectedFileInfo file)
        {
            _selectedFiles.Remove(file);
            StateHasChanged();
        }

        private async Task SelectOutputFolder()
        {
            try
            {
#pragma warning disable CA1416
                FolderPickerResult folderResult = await FolderPicker.Default.PickAsync();
#pragma warning restore CA1416

                if (folderResult.IsSuccessful)
                {
                    _outputDirectory = folderResult.Folder.Path;
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Ошибка при выборе папки: {ex.Message}", Severity.Error);
            }
        }

        #endregion

        #region Processing Logic

        private async Task ProcessFiles()
        {
            if (_selectedFiles.Count == 0 || string.IsNullOrWhiteSpace(_outputDirectory))
            {
                Snackbar.Add("Выберите файлы и выходную папку.", Severity.Warning);
                return;
            }

            try
            {
                _isProcessing = true;
                _results = [];
                StateHasChanged();

                List<IChunkRule> rules = BuildRules();
                ChunkingOptions options = new(
                    outputDirectory: _outputDirectory,
                    rules: rules,
                    maxChunkSizeBytes: _maxChunkSizeMB * 1024 * 1024,
                    headerTemplate: string.IsNullOrWhiteSpace(_headerTemplate) ? null : _headerTemplate
                );

                IEnumerable<string> filePaths = _selectedFiles.Select(f => f.Path);
                _results = await FileChunker.ProcessAsync(filePaths, options, CancellationToken.None);

                Snackbar.Add($"Обработка завершена. Создано чанков: {_results.Count}", Severity.Success);
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Ошибка при обработке: {ex.Message}", Severity.Error);
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
        /// <param name="Key">Текст плейсхолдера (например, "{FileName}").</param>
        /// <param name="Description">Краткое описание назначения плейсхолдера.</param>
        private sealed record PlaceholderInfo(string Key, string Description);

        #endregion
    }
}