using Glazecs.Modules.FMMS.Abstractions.Enums;
using Glazecs.Modules.FMMS.Abstractions.Interfaces;
using Glazecs.Modules.FMMS.Abstractions.Models;
using Glazecs.Modules.FMMS.Models;
using Glazecs.Modules.FMMS.Resources.Languages;
using Glazecs.Modules.FMMS.Services;
using Glazecs.Shared.Core.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using System.Diagnostics;
using System.Text;

namespace Glazecs.Modules.FMMS.Components.Pages
{
    public partial class FilesScanner : ComponentBase, IDisposable, IAsyncDisposable
    {
        #region Injection

        [Inject] private ILogger<FilesScanner> Logger { get; set; } = default!;
        [Inject] private IFileScannerService ScannerService { get; set; } = default!;
        [Inject] private FmmsSettingsService SettingsService { get; set; } = default!;
        [Inject] private IStringLocalizer<FmmsResources> L { get; set; } = default!;
        [Inject] private ISnackbar Snackbar { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

        #endregion

        #region State

        private string _dirPath = string.Empty;
        private bool _isScanning;
        private int _processedFilesCount;
        private CancellationTokenSource? _cts;
        private ScannedFile? _contextRow;
        private MudMenu _contextMenu = null!;
        private MudDataGrid<ScannedFile> _grid = null!;
        private ElementReference _gridContainer;
        private readonly List<ScannedFile> _scannedFiles = [];
        private readonly GridSelectionController<ScannedFile> _selection;
        private List<FileColumnConfig> _visibleColumnsCache = [];
        private bool _disposed;
        private const int ThrottleIntervalMs = 200;
        private const int BatchSize = 250;

        #endregion

        public FilesScanner()
        {
            _selection = new GridSelectionController<ScannedFile>(file => file.Id, () => InvokeAsync(StateHasChanged));
        }

        #region Properties

        private FileSizeType DisplayedSizeType => SettingsService.FilesScanningSettings.DisplayedSizeType;

        private bool HasMultipleSelection => _selection.Selected.Count > 1;

        #endregion

        #region Lifecycle

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await _selection.AttachAsync(JS, _gridContainer, _grid);
            }

            await _selection.AfterRenderAsync();
        }

        #endregion

        #region Scanning

        private async Task StartScanningAsync()
        {
            if (!ValidateScanningPath())
            {
                return;
            }

            if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.LogInformation(
                    "Начало сканирования файлов: {Path}, SizeType: {SizeType}",
                    _dirPath,
                    SettingsService.FilesScanningSettings.DisplayedSizeType);
            }

            Stopwatch sw = Stopwatch.StartNew();
            InitializeScanningState();

            try
            {
                await ProcessFilesAsync(sw);
            }
            catch (OperationCanceledException ex)
            {
                HandleScanningCancellation(ex, sw);
            }
            catch (Exception ex)
            {
                HandleScanningError(ex, sw);
            }
            finally
            {
                await FinalizeScanningAsync();
            }
        }

        private bool ValidateScanningPath()
        {
            if (string.IsNullOrWhiteSpace(_dirPath))
            {
                if (Logger.IsEnabled(LogLevel.Warning))
                {
                    Logger.LogWarning("Попытка начать сканирование без указания пути");
                }

                Snackbar.Add("Укажите путь для сканирования.", Severity.Warning);
                return false;
            }

            if (!Directory.Exists(_dirPath))
            {
                if (Logger.IsEnabled(LogLevel.Warning))
                {
                    Logger.LogWarning("Попытка начать сканирование несуществующей директории: {Path}", _dirPath);
                }

                Snackbar.Add("Указанная директория не существует.", Severity.Warning);
                return false;
            }

            return true;
        }

        private void InitializeScanningState()
        {
            _isScanning = true;
            _processedFilesCount = 0;
            _scannedFiles.Clear();
            _selection.Clear();
            _visibleColumnsCache.Clear();
            _cts = new CancellationTokenSource();
        }

        /// <remarks>
        /// Поток файлов читается вне потока UI (ConfigureAwait(false)): иначе каждый файл — отдельная задача
        /// в очереди UI, и при быстром сканировании нажатия клавиш ждали бы секундами. В UI уходят только пачки.
        /// </remarks>
        private async Task ProcessFilesAsync(Stopwatch sw)
        {
            List<ScannedFile> tempList = new(capacity: BatchSize);
            long lastUiUpdate = Environment.TickCount64;
            CancellationToken token = _cts?.Token ?? CancellationToken.None;
            int totalCount = 0;

            await foreach (ScannedFile file in ScannerService.ScanDirectoryAsync(
                _dirPath,
                SettingsService.FilesScanningSettings,
                progress: null,
                token).ConfigureAwait(false))
            {
                tempList.Add(file);

                long currentTime = Environment.TickCount64;
                bool isBatchFull = tempList.Count >= BatchSize;
                bool isTimeToUpdate = (currentTime - lastUiUpdate) >= ThrottleIntervalMs && tempList.Count > 0;

                if (isBatchFull || isTimeToUpdate)
                {
                    totalCount += tempList.Count;
                    await FlushBatchAsync(tempList).ConfigureAwait(false);
                    lastUiUpdate = Environment.TickCount64;
                }
            }

            if (tempList.Count > 0)
            {
                totalCount += tempList.Count;
                await FlushBatchAsync(tempList).ConfigureAwait(false);
            }

            sw.Stop();

            if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.LogInformation(
                    "Сканирование файлов завершено успешно. Найдено файлов: {Count}, Время: {ElapsedMs} мс",
                    totalCount, sw.ElapsedMilliseconds);
            }

            await InvokeAsync(() => Snackbar.Add(L["Scanner_Completed_Success"], Severity.Success));
        }

        /// <summary>
        /// Передаёт накопленную пачку в UI. Список таблицы меняется только в потоке UI.
        /// </summary>
        private async Task FlushBatchAsync(List<ScannedFile> tempList)
        {
            ScannedFile[] batch = [.. tempList];
            tempList.Clear();

            await InvokeAsync(() =>
            {
                _scannedFiles.AddRange(batch);
                _processedFilesCount = _scannedFiles.Count;

                if (Logger.IsEnabled(LogLevel.Trace))
                {
                    Logger.LogTrace("Пакетное обновление UI: добавлено {BatchSize} файлов. Всего: {Total}",
                        batch.Length, _scannedFiles.Count);
                }

                StateHasChanged();
            });
        }

        private void HandleScanningCancellation(OperationCanceledException ex, Stopwatch sw)
        {
            sw.Stop();

            if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.LogInformation(ex,
                    "Сканирование файлов отменено пользователем. Обработано файлов: {Count}, Время: {ElapsedMs} мс",
                    _scannedFiles.Count, sw.ElapsedMilliseconds);
            }

            Snackbar.Add(L["Scanner_Cancelled"], Severity.Warning);
        }

        private void HandleScanningError(Exception ex, Stopwatch sw)
        {
            sw.Stop();

            if (Logger.IsEnabled(LogLevel.Error))
            {
                Logger.LogError(ex,
                    "Ошибка при сканировании файлов: {Path}. Обработано файлов: {Count}, Время: {ElapsedMs} мс",
                    _dirPath, _scannedFiles.Count, sw.ElapsedMilliseconds);
            }

            Snackbar.Add($"{L["Common_Error"]}: {ex.Message}", Severity.Error);
        }

        private async Task FinalizeScanningAsync()
        {
            _isScanning = false;
            _cts?.Dispose();
            _cts = null;
            await InvokeAsync(StateHasChanged);
        }

        private void CancelScanning()
        {
            if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.LogInformation("Запрос отмены сканирования файлов. IsScanning: {IsScanning}", _isScanning);
            }

            _cts?.Cancel();
        }

        #endregion

        #region UI Interactions

        private async Task OpenMenuContent(DataGridRowClickEventArgs<ScannedFile> args)
        {
            _contextRow = args.Item;
            _selection.EnsureSelected(args.Item);

            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace("Открытие контекстного меню для файла: {Path}", args.Item.FullPath);
            }

            await _contextMenu.OpenMenuAsync(args.MouseEventArgs);
        }

        private void SelectedItemsChanged(HashSet<ScannedFile> items)
        {
            _selection.SyncFromGrid(items);

            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace("Изменение выбора файлов. Выбрано: {Count}", items?.Count ?? 0);
            }
        }

        #endregion

        #region Formatting

        public string FormatSize(double size)
        {
            return DisplayedSizeType switch
            {
                FileSizeType.Bit => $"{size.ToBits():F0} Bit",
                FileSizeType.B => $"{size:F0} B",
                FileSizeType.KB => $"{size.ToKiloBytes():F2} KB",
                FileSizeType.MB => $"{size.ToMegaBytes():F2} MB",
                FileSizeType.GB => $"{size.ToGigaBytes():F2} GB",
                FileSizeType.TB => $"{size.ToTeraBytes():F2} TB",
                FileSizeType.PB => $"{size.ToPetaBytes():F2} PB",
                _ => $"{size:F0} B"
            };
        }

        private static string GetHashValue(ScannedFile file, string algorithmName)
        {
            return file.Hashes != null && file.Hashes.TryGetValue(algorithmName, out string? hash) ? hash : "-";
        }

        #endregion

        #region Column Configuration

        private List<FileColumnConfig> GetVisibleColumnsConfig()
        {
            if (_visibleColumnsCache.Count > 0)
            {
                return _visibleColumnsCache;
            }

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Инициализация конфигурации видимых колонок");
            }

            List<FileColumnConfig> configs = [];
            AddStandardColumns(configs);
            AddHashColumns(configs);

            _visibleColumnsCache = configs;
            return configs;
        }

        private void AddStandardColumns(List<FileColumnConfig> configs)
        {
            FilesScanningSettings settings = SettingsService.FilesScanningSettings;
            AnalyzeFileSettings analyzeSettings = settings.AnalyzeSettings;

            TryAddColumn(configs, analyzeSettings, AnalyzeField.Id, true, "Table_Id",
                f => f.Id.ToString(), isRowNumber: true);

            TryAddColumn(configs, analyzeSettings, AnalyzeField.Name, true, "Table_Name",
                f => f.Name);

            TryAddColumn(configs, analyzeSettings, AnalyzeField.Extension, true, "Table_Extension",
                f => f.Extension);

            TryAddColumn(configs, analyzeSettings, AnalyzeField.Size, true, "Table_Size",
                f => FormatSize(f.Size));

            TryAddColumn(configs, analyzeSettings, AnalyzeField.PagesCount, true, "Table_Pages_Count",
                f => f.PagesCount > 0 ? f.PagesCount.ToString() : "-");

            TryAddColumn(configs, analyzeSettings, AnalyzeField.IsArchive, false, "Table_Is_Archive",
                f => f.IsArchive ? "Yes" : "No");

            TryAddColumn(configs, analyzeSettings, AnalyzeField.IsArchiveEntry, false, "Table_Is_Archive_Entry",
                f => f.IsArchiveEntry ? "Yes" : "No");

            TryAddColumn(configs, analyzeSettings, AnalyzeField.FullPath, false, "Table_Full_Path",
                f => f.FullPath);
        }

        private void AddHashColumns(List<FileColumnConfig> configs)
        {
            FilesScanningSettings settings = SettingsService.FilesScanningSettings;
            HashingSettings hashSettings = settings.Hashing;

            foreach (string algo in hashSettings.AlgorithmsToCalculate)
            {
                configs.Add(new FileColumnConfig(algo, f => GetHashValue(f, algo)));
            }
        }

        private static void TryAddColumn(
            List<FileColumnConfig> configs,
            AnalyzeFileSettings visibleSettings,
            AnalyzeField field,
            bool defaultValue,
            string headerKey,
            Func<ScannedFile, string> valueSelector,
            bool isRowNumber = false)
        {
            if (visibleSettings.FieldsToAnalyze.GetValueOrDefault(field, defaultValue))
            {
                configs.Add(new FileColumnConfig(headerKey, valueSelector) { IsRowNumber = isRowNumber });
            }
        }

        #endregion

        #region File Actions

        private void OpenFile(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                if (Logger.IsEnabled(LogLevel.Warning))
                {
                    Logger.LogWarning("Попытка открыть файл с пустым путём");
                }

                return;
            }

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Открытие файла: {Path}", filePath);
            }

            try
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });

                if (Logger.IsEnabled(LogLevel.Information))
                {
                    Logger.LogInformation("Файл успешно открыт: {Path}", filePath);
                }
            }
            catch (Exception ex)
            {
                if (Logger.IsEnabled(LogLevel.Error))
                {
                    Logger.LogError(ex, "Ошибка при открытии файла: {Path}", filePath);
                }

                Snackbar.Add(ex.Message, Severity.Error, config => config.RequireInteraction = true);
            }
        }

        private void OpenFileDirectory(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                if (Logger.IsEnabled(LogLevel.Warning))
                {
                    Logger.LogWarning("Попытка открыть папку файла с пустым путём");
                }

                return;
            }

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Открытие папки файла: {Path}", filePath);
            }

            try
            {
                if (TryOpenFileDirectoryByPlatform(filePath) && Logger.IsEnabled(LogLevel.Information))
                {
                    Logger.LogInformation("Папка файла успешно открыта: {Path}", filePath);
                }
            }
            catch (Exception ex)
            {
                if (Logger.IsEnabled(LogLevel.Error))
                {
                    Logger.LogError(ex, "Ошибка при открытии папки файла: {Path}", filePath);
                }

                Snackbar.Add(ex.Message, Severity.Error, config => config.RequireInteraction = true);
            }
        }

        private bool TryOpenFileDirectoryByPlatform(string filePath)
        {
#pragma warning disable S4036 // Use an absolute path for this command
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select, \"{filePath}\"")
                {
                    UseShellExecute = true
                });

                return true;
            }

            if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", $"-R \"{filePath}\"");
                return true;
            }

            if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", Path.GetDirectoryName(filePath) ?? ".");
                return true;
            }

            if (Logger.IsEnabled(LogLevel.Warning))
            {
                Logger.LogWarning("Открытие папки не поддерживается на этой платформе");
            }

            Snackbar.Add("Открытие папки не поддерживается на этой платформе.", Severity.Warning);
            return false;
#pragma warning restore S4036
        }

        #endregion

        #region Clipboard Operations

        /// <param name="number">Номер файла среди скопированных — пишется в колонку номера вместо Id.</param>
        private string GetFileInfoFormatted(ScannedFile file, int number)
        {
            StringBuilder sb = new();
            List<FileColumnConfig> configs = GetVisibleColumnsConfig();

            foreach (FileColumnConfig config in configs)
            {
                string header = L[config.HeaderKey];
                string value = GetCopiedValue(config, file, number);
                sb.Append($"{header}: {value} | ");
            }

            return sb.Length > 3 ? sb.ToString(0, sb.Length - 3) : sb.ToString();
        }

        private string GetFileInfoTsv(ScannedFile file, int number)
        {
            List<FileColumnConfig> configs = GetVisibleColumnsConfig();
            IEnumerable<string> values = configs.Select(c => GetCopiedValue(c, file, number));
            return string.Join("\t", values);
        }

        private static string GetCopiedValue(FileColumnConfig config, ScannedFile file, int number)
        {
            return config.IsRowNumber ? number.ToString() : config.ValueSelector(file);
        }

        private string GetTsvHeaders()
        {
            List<FileColumnConfig> configs = GetVisibleColumnsConfig();
            IEnumerable<string> headers = configs.Select(c => L[c.HeaderKey].ToString());
            return string.Join("\t", headers);
        }

        private async Task CopySingleInfoAsync()
        {
            ScannedFile? file = GetActiveFile();

            if (file.HasValue)
            {
                await CopySingleFileInfoAsync(file.Value, formatted: true);
            }
            else
            {
                if (Logger.IsEnabled(LogLevel.Warning))
                {
                    Logger.LogWarning("Попытка копирования без выбранного файла");
                }
            }
        }

        private async Task CopySingleInfoTsvAsync()
        {
            ScannedFile? file = GetActiveFile();

            if (file.HasValue)
            {
                await CopySingleFileInfoAsync(file.Value, formatted: false);
            }
            else
            {
                if (Logger.IsEnabled(LogLevel.Warning))
                {
                    Logger.LogWarning("Попытка копирования TSV без выбранного файла");
                }
            }
        }

        private async Task CopySingleFileInfoAsync(ScannedFile file, bool formatted)
        {
            string formatType = formatted ? "форматированно" : "TSV";

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Копирование информации о файле ({FormatType}): {Path}", formatType, file.FullPath);
            }

            string content = formatted
                ? GetFileInfoFormatted(file, number: 1)
                : BuildTsvContent([file]);

            await Clipboard.Default.SetTextAsync(content);

            if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.LogInformation("{FormatType}-информация о файле скопирована. Длина: {Length} символов",
                    formatted ? "Форматированная" : "TSV", content.Length);
            }

            Snackbar.Add(L["Common_Copied"], Severity.Info);
        }

        private async Task CopySelectedInfoAsync()
        {
            if (!ValidateSelectedFiles("копирования выбранных файлов"))
            {
                return;
            }

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Копирование информации о {Count} выбранных файлах (форматированно)", _selection.Selected.Count);
            }

            List<ScannedFile> sortedFiles = GetSortedSelectedFiles();
            StringBuilder sb = new();

            for (int i = 0; i < sortedFiles.Count; i++)
            {
                sb.AppendLine(GetFileInfoFormatted(sortedFiles[i], number: i + 1));
            }

            string content = sb.ToString();
            await Clipboard.Default.SetTextAsync(content);

            if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.LogInformation("Информация о {Count} файлах скопирована. Длина: {Length} символов",
                    sortedFiles.Count, content.Length);
            }

            Snackbar.Add(L["Common_Copied"], Severity.Info);
        }

        private async Task CopySelectedInfoTsvAsync()
        {
            if (!ValidateSelectedFiles("копирования TSV выбранных файлов"))
            {
                return;
            }

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Копирование информации о {Count} выбранных файлах (TSV)", _selection.Selected.Count);
            }

            List<ScannedFile> sortedFiles = GetSortedSelectedFiles();
            string content = BuildTsvContent(sortedFiles);

            await Clipboard.Default.SetTextAsync(content);

            if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.LogInformation("TSV-информация о {Count} файлах скопирована. Длина: {Length} символов",
                    sortedFiles.Count, content.Length);
            }

            Snackbar.Add(L["Common_Copied"], Severity.Info);
        }

        private string BuildTsvContent(List<ScannedFile> files)
        {
            StringBuilder sb = new();
            sb.AppendLine(GetTsvHeaders());

            for (int i = 0; i < files.Count; i++)
            {
                sb.AppendLine(GetFileInfoTsv(files[i], number: i + 1));
            }

            return sb.ToString();
        }

        private bool ValidateSelectedFiles(string operationName)
        {
            if (_selection.Selected.Count == 0)
            {
                if (Logger.IsEnabled(LogLevel.Warning))
                {
                    Logger.LogWarning("Попытка {Operation}, но список пуст", operationName);
                }

                return false;
            }

            return true;
        }

        private ScannedFile? GetActiveFile()
        {
            return _contextRow ?? GetKeyboardActiveFile();
        }

        /// <summary>
        /// Файл для горячих клавиш: текущая строка, если она выделена, иначе единственный выделенный.
        /// </summary>
        private ScannedFile? GetKeyboardActiveFile()
        {
            if (_selection.Cursor.HasValue && _selection.Selected.Contains(_selection.Cursor.Value))
            {
                return _selection.Cursor;
            }

            return _selection.Selected.Count == 1 ? _selection.Selected.First() : null;
        }

        /// <summary>
        /// Выделенные файлы в порядке, в котором они показаны в таблице.
        /// </summary>
        private List<ScannedFile> GetSortedSelectedFiles()
        {
            return _selection.GetSelectedInDisplayOrder(_selection.OrderedItems);
        }

        #endregion

        #region Keyboard Shortcuts

        // Работает и во время сканирования: обработчик и пополнение списка идут в одном потоке UI
        private async Task OnDataGridKeyDown(KeyboardEventArgs e)
        {
            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace("Нажата клавиша: {Code}, Ctrl: {Ctrl}, Shift: {Shift}",
                    e.Code, e.CtrlKey, e.ShiftKey);
            }

            if (_selection.HandleKey(e) || TryHandleCopyShortcut(e))
            {
                return;
            }

            await TryHandleOpenShortcutAsync(e);
        }

        private bool TryHandleCopyShortcut(KeyboardEventArgs e)
        {
            if (!e.CtrlKey)
            {
                return false;
            }

            if (e.ShiftKey && e.Code == "KeyC")
            {
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("Горячая клавиша: Ctrl+Shift+C (копирование TSV)");
                }

                _ = CopySelectedInfoTsvAsync();
                return true;
            }

            if (e.Code == "KeyC")
            {
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("Горячая клавиша: Ctrl+C (копирование)");
                }

                _ = CopySelectedInfoAsync();
                return true;
            }

            return false;
        }

        private async Task TryHandleOpenShortcutAsync(KeyboardEventArgs e)
        {
            if (!e.CtrlKey)
            {
                return;
            }

            ScannedFile? activeFile = GetKeyboardActiveFile();
            if (!activeFile.HasValue)
            {
                return;
            }

            if (e.ShiftKey && e.Code == "KeyO")
            {
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("Горячая клавиша: Ctrl+Shift+O (открытие папки файла)");
                }

                OpenFileDirectory(activeFile.Value.FullPath);
            }
            else if (e.Code == "KeyO")
            {
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("Горячая клавиша: Ctrl+O (открытие файла)");
                }

                OpenFile(activeFile.Value.FullPath);
            }
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cts?.Dispose();
                    _contextMenu?.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        // Blazor вызывает только DisposeAsync, если компонент реализует оба интерфейса
        public async ValueTask DisposeAsync()
        {
            await _selection.DisposeAsync();
            Dispose();
        }

        #endregion
    }
}