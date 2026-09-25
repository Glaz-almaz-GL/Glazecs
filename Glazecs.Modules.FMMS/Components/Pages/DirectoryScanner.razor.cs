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
    public partial class DirectoryScanner : ComponentBase, IDisposable, IAsyncDisposable
    {
        #region Injection

        [Inject] private ILogger<DirectoryScanner> Logger { get; set; } = default!;
        [Inject] private IDirectoryScannerService Scanner { get; set; } = default!;
        [Inject] private FmmsSettingsService SettingsService { get; set; } = default!;
        [Inject] private IStringLocalizer<FmmsResources> L { get; set; } = default!;
        [Inject] private ISnackbar Snackbar { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

        #endregion

        #region State

        private string _dirPath = string.Empty;
        private bool _isScanning;
        private int _processedDirsCount;
        private CancellationTokenSource? _cts;
        private ScannedDirectory? _contextRow;
        private MudMenu _contextMenu = null!;
        private MudDataGrid<ScannedDirectory> _grid = null!;
        private ElementReference _gridContainer;
        private readonly List<ScannedDirectory> _scannedDirs = [];
        private readonly GridSelectionController<ScannedDirectory> _selection;
        private List<DirectoryColumnConfig> _visibleColumnsCache = [];
        private bool _disposed;
        private const int ThrottleIntervalMs = 200;
        private const int BatchSize = 250;

        #endregion

        public DirectoryScanner()
        {
            _selection = new GridSelectionController<ScannedDirectory>(dir => dir.Id, () => InvokeAsync(StateHasChanged));
        }

        #region Properties

        private FileSizeType DisplayedSizeType => SettingsService.DirectoryScanningSettings.DisplayedSizeType;

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
                    "Начало сканирования директорий: {Path}, SizeType: {SizeType}",
                    _dirPath,
                    DisplayedSizeType);
            }

            Stopwatch sw = Stopwatch.StartNew();
            InitializeScanningState();

            try
            {
                await ProcessDirsAsync(sw);
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
            _processedDirsCount = 0;
            _scannedDirs.Clear();
            _selection.Clear();
            _visibleColumnsCache.Clear();
            _cts = new CancellationTokenSource();
        }

        /// <remarks>
        /// Поток директорий читается вне потока UI (ConfigureAwait(false)): иначе каждая директория — отдельная
        /// задача в очереди UI, и нажатия клавиш ждали бы секундами. В UI уходят только пачки.
        /// </remarks>
        private async Task ProcessDirsAsync(Stopwatch sw)
        {
            List<ScannedDirectory> tempList = new(capacity: BatchSize);
            long lastUiUpdate = Environment.TickCount64;
            CancellationToken token = _cts?.Token ?? CancellationToken.None;
            int totalCount = 0;

            await foreach (ScannedDirectory dir in Scanner.ScanDirectoryAsync(
                _dirPath,
                SettingsService.DirectoryScanningSettings,
                progress: null,
                token).ConfigureAwait(false))
            {
                tempList.Add(dir);

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
                    "Сканирование директорий завершено успешно. Найдено директорий: {Count}, Время: {ElapsedMs} мс",
                    totalCount, sw.ElapsedMilliseconds);
            }

            await InvokeAsync(() => Snackbar.Add(L["Scanner_Completed_Success"], Severity.Success));
        }

        /// <summary>
        /// Передаёт накопленную пачку в UI. Список таблицы меняется только в потоке UI.
        /// </summary>
        private async Task FlushBatchAsync(List<ScannedDirectory> tempList)
        {
            ScannedDirectory[] batch = [.. tempList];
            tempList.Clear();

            await InvokeAsync(() =>
            {
                _scannedDirs.AddRange(batch);
                _processedDirsCount = _scannedDirs.Count;

                if (Logger.IsEnabled(LogLevel.Trace))
                {
                    Logger.LogTrace("Пакетное обновление UI: добавлено {Count} директорий. Всего: {Total}",
                        batch.Length, _scannedDirs.Count);
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
                    "Сканирование директорий отменено пользователем. Обработано директорий: {Count}, Время: {ElapsedMs} мс",
                    _scannedDirs.Count, sw.ElapsedMilliseconds);
            }

            Snackbar.Add(L["Scanner_Cancelled"], Severity.Warning);
        }

        private void HandleScanningError(Exception ex, Stopwatch sw)
        {
            sw.Stop();

            if (Logger.IsEnabled(LogLevel.Error))
            {
                Logger.LogError(ex,
                    "Ошибка при сканировании директорий: {Path}. Обработано директорий: {Count}, Время: {ElapsedMs} мс",
                    _dirPath, _scannedDirs.Count, sw.ElapsedMilliseconds);
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
                Logger.LogInformation("Запрос отмены сканирования директорий. IsScanning: {IsScanning}", _isScanning);
            }

            _cts?.Cancel();
        }

        #endregion

        #region UI Interactions

        private async Task OpenMenuContent(DataGridRowClickEventArgs<ScannedDirectory> args)
        {
            _contextRow = args.Item;
            _selection.EnsureSelected(args.Item);

            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace("Открытие контекстного меню для директории: {Path}", args.Item.FullPath);
            }

            await _contextMenu.OpenMenuAsync(args.MouseEventArgs);
        }

        private void SelectedItemsChanged(HashSet<ScannedDirectory> items)
        {
            _selection.SyncFromGrid(items);

            if (Logger.IsEnabled(LogLevel.Trace))
            {
                Logger.LogTrace("Изменение выбора директорий. Выбрано: {Count}", items?.Count ?? 0);
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

        #endregion

        #region Column Configuration

        private List<DirectoryColumnConfig> GetVisibleColumnsConfig()
        {
            if (_visibleColumnsCache.Count > 0)
            {
                return _visibleColumnsCache;
            }

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Инициализация конфигурации видимых колонок");
            }

            List<DirectoryColumnConfig> configs = [];
            AddStandardColumns(configs);

            _visibleColumnsCache = configs;
            return configs;
        }

        private void AddStandardColumns(List<DirectoryColumnConfig> configs)
        {
            TryAddColumn(configs, "Table_Id", dir => dir.Id.ToString(), isRowNumber: true);
            TryAddColumn(configs, "Column_Name_Dir", dir => dir.RelativePath);
            TryAddColumn(configs, "Table_Full_Path", dir => dir.FullPath);
            TryAddColumn(configs, "Column_Size", dir => FormatSize(dir.Size));
            TryAddColumn(configs, "Column_Files_Count", dir => dir.FilesCount.ToString());
        }

        private static void TryAddColumn(
            List<DirectoryColumnConfig> configs,
            string headerKey,
            Func<ScannedDirectory, string> valueSelector,
            bool isRowNumber = false)
        {
            configs.Add(new DirectoryColumnConfig(headerKey, valueSelector) { IsRowNumber = isRowNumber });
        }

        #endregion

        #region Directory Actions

        private void OpenDirectory(string? dirPath)
        {
            if (string.IsNullOrEmpty(dirPath))
            {
                if (Logger.IsEnabled(LogLevel.Warning))
                {
                    Logger.LogWarning("Попытка открыть директорию с пустым путём");
                }

                return;
            }

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Открытие директории: {Path}", dirPath);
            }

            try
            {
                Process.Start(new ProcessStartInfo(dirPath) { UseShellExecute = true });

                if (Logger.IsEnabled(LogLevel.Information))
                {
                    Logger.LogInformation("Директория успешно открыта: {Path}", dirPath);
                }
            }
            catch (Exception ex)
            {
                if (Logger.IsEnabled(LogLevel.Error))
                {
                    Logger.LogError(ex, "Ошибка при открытии директории: {Path}", dirPath);
                }

                Snackbar.Add(ex.Message, Severity.Error, config => config.RequireInteraction = true);
            }
        }

        #endregion

        #region Clipboard Operations

        /// <param name="number">Номер директории среди скопированных — пишется в колонку номера вместо Id.</param>
        private string GetDirInfoFormatted(ScannedDirectory dir, int number)
        {
            StringBuilder sb = new();
            List<DirectoryColumnConfig> configs = GetVisibleColumnsConfig();

            foreach (DirectoryColumnConfig config in configs)
            {
                string header = L[config.HeaderKey];
                string value = GetCopiedValue(config, dir, number);
                sb.Append($"{header}: {value} | ");
            }

            return sb.Length > 3 ? sb.ToString(0, sb.Length - 3) : sb.ToString();
        }

        private string GetDirInfoTsv(ScannedDirectory dir, int number)
        {
            List<DirectoryColumnConfig> configs = GetVisibleColumnsConfig();
            IEnumerable<string> values = configs.Select(c => GetCopiedValue(c, dir, number));
            return string.Join("\t", values);
        }

        private static string GetCopiedValue(DirectoryColumnConfig config, ScannedDirectory dir, int number)
        {
            return config.IsRowNumber ? number.ToString() : config.ValueSelector(dir);
        }

        private string GetTsvHeaders()
        {
            List<DirectoryColumnConfig> configs = GetVisibleColumnsConfig();
            IEnumerable<string> headers = configs.Select(c => L[c.HeaderKey].ToString());
            return string.Join("\t", headers);
        }

        private async Task CopySingleInfoAsync()
        {
            ScannedDirectory? dir = GetActiveDir();

            if (dir.HasValue)
            {
                await CopySingleDirInfoAsync(dir.Value, formatted: true);
            }
            else
            {
                if (Logger.IsEnabled(LogLevel.Warning))
                {
                    Logger.LogWarning("Попытка копирования без выбранной директории");
                }
            }
        }

        private async Task CopySingleInfoTsvAsync()
        {
            ScannedDirectory? dir = GetActiveDir();

            if (dir.HasValue)
            {
                await CopySingleDirInfoAsync(dir.Value, formatted: false);
            }
            else
            {
                if (Logger.IsEnabled(LogLevel.Warning))
                {
                    Logger.LogWarning("Попытка копирования TSV без выбранной директории");
                }
            }
        }

        private async Task CopySingleDirInfoAsync(ScannedDirectory dir, bool formatted)
        {
            string formatType = formatted ? "форматированно" : "TSV";

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Копирование информации о директории ({FormatType}): {Path}", formatType, dir.FullPath);
            }

            string content = formatted
                ? GetDirInfoFormatted(dir, number: 1)
                : BuildTsvContent([dir]);

            await Clipboard.Default.SetTextAsync(content);

            if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.LogInformation("{FormatType}-информация о директории скопирована. Длина: {Length} символов",
                    formatted ? "Форматированная" : "TSV", content.Length);
            }

            Snackbar.Add(L["Common_Copied"], Severity.Info);
        }

        private async Task CopySelectedInfoAsync()
        {
            if (!ValidateSelectedDirs("копирования выбранных директорий"))
            {
                return;
            }

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Копирование информации о {Count} выбранных директориях (форматированно)", _selection.Selected.Count);
            }

            List<ScannedDirectory> sortedDirs = GetSortedSelectedDirs();
            StringBuilder sb = new();

            for (int i = 0; i < sortedDirs.Count; i++)
            {
                sb.AppendLine(GetDirInfoFormatted(sortedDirs[i], number: i + 1));
            }

            string content = sb.ToString();
            await Clipboard.Default.SetTextAsync(content);

            if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.LogInformation("Информация о {Count} директориях скопирована. Длина: {Length} символов",
                    sortedDirs.Count, content.Length);
            }

            Snackbar.Add(L["Common_Copied"], Severity.Info);
        }

        private async Task CopySelectedInfoTsvAsync()
        {
            if (!ValidateSelectedDirs("копирования TSV выбранных директорий"))
            {
                return;
            }

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Копирование информации о {Count} выбранных директориях (TSV)", _selection.Selected.Count);
            }

            List<ScannedDirectory> sortedDirs = GetSortedSelectedDirs();
            string content = BuildTsvContent(sortedDirs);

            await Clipboard.Default.SetTextAsync(content);

            if (Logger.IsEnabled(LogLevel.Information))
            {
                Logger.LogInformation("TSV-информация о {Count} директориях скопирована. Длина: {Length} символов",
                    sortedDirs.Count, content.Length);
            }

            Snackbar.Add(L["Common_Copied"], Severity.Info);
        }

        private string BuildTsvContent(List<ScannedDirectory> dirs)
        {
            StringBuilder sb = new();
            sb.AppendLine(GetTsvHeaders());

            for (int i = 0; i < dirs.Count; i++)
            {
                sb.AppendLine(GetDirInfoTsv(dirs[i], number: i + 1));
            }

            return sb.ToString();
        }

        private bool ValidateSelectedDirs(string operationName)
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

        private ScannedDirectory? GetActiveDir()
        {
            return _contextRow ?? GetKeyboardActiveDir();
        }

        /// <summary>
        /// Директория для горячих клавиш: текущая строка, если она выделена, иначе единственная выделенная.
        /// </summary>
        private ScannedDirectory? GetKeyboardActiveDir()
        {
            if (_selection.Cursor.HasValue && _selection.Selected.Contains(_selection.Cursor.Value))
            {
                return _selection.Cursor;
            }

            return _selection.Selected.Count == 1 ? _selection.Selected.First() : null;
        }

        /// <summary>
        /// Выделенные директории в порядке, в котором они показаны в таблице.
        /// </summary>
        private List<ScannedDirectory> GetSortedSelectedDirs()
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

            ScannedDirectory? activeDir = GetKeyboardActiveDir();
            if (!activeDir.HasValue)
            {
                return;
            }

            if (e.ShiftKey && e.Code == "KeyO")
            {
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("Горячая клавиша: Ctrl+Shift+O (открытие директории)");
                }

                OpenDirectory(activeDir.Value.FullPath);
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