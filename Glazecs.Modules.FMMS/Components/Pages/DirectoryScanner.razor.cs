using Glazecs.Modules.FMMS.Abstractions.Enums;
using Glazecs.Modules.FMMS.Abstractions.Models;
using Glazecs.Modules.FMMS.Models;
using Glazecs.Shared.Core.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Diagnostics;
using System.Text;

namespace Glazecs.Modules.FMMS.Components.Pages
{
    public partial class DirectoryScanner(ILogger<DirectoryScanner>? logger = null) : ComponentBase, IDisposable
    {
        private readonly ILogger<DirectoryScanner>? _logger = logger;

        #region State

        private string _dirPath = string.Empty;
        private bool _isScanning;
        private int _processedDirsCount;
        private CancellationTokenSource? _cts;
        private ScannedDirectory? _contextRow;
        private MudMenu _contextMenu = null!;
        private readonly List<ScannedDirectory> _scannedDirs = [];
        private HashSet<ScannedDirectory>? _selectedRows;
        private List<DirectoryColumnConfig> _visibleColumnsCache = [];
        private bool _disposed;
        private const int ThrottleIntervalMs = 200;
        private const int BatchSize = 250;

        #endregion

        #region Properties

        private FileSizeType DisplayedSizeType => SettingsService.DirectoryScanningSettings.DisplayedSizeType;

        #endregion

        #region Scanning

        private async Task StartScanningAsync()
        {
            if (!ValidateScanningPath())
            {
                return;
            }

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(
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
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning("Попытка начать сканирование без указания пути");
                }

                Snackbar.Add("Укажите путь для сканирования.", Severity.Warning);
                return false;
            }

            if (!Directory.Exists(_dirPath))
            {
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning("Попытка начать сканирование несуществующей директории: {Path}", _dirPath);
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
            _selectedRows?.Clear();
            _visibleColumnsCache.Clear();
            _cts = new CancellationTokenSource();
        }

        private async Task ProcessDirsAsync(Stopwatch sw)
        {
            List<ScannedDirectory> tempList = new(capacity: BatchSize);
            long lastUiUpdate = Environment.TickCount64;
            CancellationToken token = _cts?.Token ?? CancellationToken.None;

            await foreach (ScannedDirectory dir in Scanner.ScanDirectoryAsync(
                _dirPath,
                SettingsService.DirectoryScanningSettings,
                progress: null,
                token))
            {
                tempList.Add(dir);

                long currentTime = Environment.TickCount64;
                bool isBatchFull = tempList.Count >= BatchSize;
                bool isTimeToUpdate = (currentTime - lastUiUpdate) >= ThrottleIntervalMs && tempList.Count > 0;

                if (isBatchFull || isTimeToUpdate)
                {
                    await FlushBatchAsync(tempList);
                    lastUiUpdate = Environment.TickCount64;

                    await Task.Delay(1, token);
                }
            }

            if (tempList.Count > 0)
            {
                await FlushBatchAsync(tempList);
            }

            sw.Stop();

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(
                    "Сканирование директорий завершено успешно. Найдено директорий: {Count}, Время: {ElapsedMs} мс",
                    _scannedDirs.Count, sw.ElapsedMilliseconds);
            }

            Snackbar.Add(L["Scanner_Completed_Success"], Severity.Success);
        }

        private async Task FlushBatchAsync(List<ScannedDirectory> tempList)
        {
            _scannedDirs.AddRange(tempList);
            _processedDirsCount = _scannedDirs.Count;
            tempList.Clear();

            if (_logger?.IsEnabled(LogLevel.Trace) == true)
            {
                _logger.LogTrace("Пакетное обновление UI: добавлено {Count} директорий. Всего: {Total}",
                    tempList.Count, _scannedDirs.Count);
            }

            await InvokeAsync(StateHasChanged);
        }

        private void HandleScanningCancellation(OperationCanceledException ex, Stopwatch sw)
        {
            sw.Stop();

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(ex,
                    "Сканирование директорий отменено пользователем. Обработано директорий: {Count}, Время: {ElapsedMs} мс",
                    _scannedDirs.Count, sw.ElapsedMilliseconds);
            }

            Snackbar.Add(L["Scanner_Cancelled"], Severity.Warning);
        }

        private void HandleScanningError(Exception ex, Stopwatch sw)
        {
            sw.Stop();

            if (_logger?.IsEnabled(LogLevel.Error) == true)
            {
                _logger.LogError(ex,
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
            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Запрос отмены сканирования директорий. IsScanning: {IsScanning}", _isScanning);
            }

            _cts?.Cancel();
        }

        #endregion

        #region UI Interactions

        private async Task OpenMenuContent(DataGridRowClickEventArgs<ScannedDirectory> args)
        {
            _contextRow = args.Item;

            if (_logger?.IsEnabled(LogLevel.Trace) == true)
            {
                _logger.LogTrace("Открытие контекстного меню для директории: {Path}", args.Item.FullPath);
            }

            await _contextMenu.OpenMenuAsync(args.MouseEventArgs);
        }

        private void SelectedItemsChanged(HashSet<ScannedDirectory> items)
        {
            _selectedRows = items;

            if (_logger?.IsEnabled(LogLevel.Trace) == true)
            {
                _logger.LogTrace("Изменение выбора директорий. Выбрано: {Count}", items?.Count ?? 0);
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

            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Инициализация конфигурации видимых колонок");
            }

            List<DirectoryColumnConfig> configs = [];
            AddStandardColumns(configs);

            _visibleColumnsCache = configs;
            return configs;
        }

        private void AddStandardColumns(List<DirectoryColumnConfig> configs)
        {
            TryAddColumn(configs, "Table_Id", dir => dir.Id.ToString());
            TryAddColumn(configs, "Column_Name_Dir", dir => dir.RelativePath);
            TryAddColumn(configs, "Table_Full_Path", dir => dir.FullPath);
            TryAddColumn(configs, "Column_Size", dir => FormatSize(dir.Size));
            TryAddColumn(configs, "Column_Files_Count", dir => dir.FilesCount.ToString());
        }

        private static void TryAddColumn(
            List<DirectoryColumnConfig> configs,
            string headerKey,
            Func<ScannedDirectory, string> valueSelector)
        {
            configs.Add(new DirectoryColumnConfig(headerKey, valueSelector));
        }

        #endregion

        #region Directory Actions

        private void OpenDirectory(string? dirPath)
        {
            if (string.IsNullOrEmpty(dirPath))
            {
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning("Попытка открыть директорию с пустым путём");
                }

                return;
            }

            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Открытие директории: {Path}", dirPath);
            }

            try
            {
                Process.Start(new ProcessStartInfo(dirPath) { UseShellExecute = true });

                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("Директория успешно открыта: {Path}", dirPath);
                }
            }
            catch (Exception ex)
            {
                if (_logger?.IsEnabled(LogLevel.Error) == true)
                {
                    _logger.LogError(ex, "Ошибка при открытии директории: {Path}", dirPath);
                }

                Snackbar.Add(ex.Message, Severity.Error, config => config.RequireInteraction = true);
            }
        }

        #endregion

        #region Clipboard Operations

        private string GetDirInfoFormatted(ScannedDirectory dir)
        {
            StringBuilder sb = new();
            List<DirectoryColumnConfig> configs = GetVisibleColumnsConfig();

            foreach (DirectoryColumnConfig config in configs)
            {
                string header = L[config.HeaderKey];
                string value = config.ValueSelector(dir);
                sb.Append($"{header}: {value} | ");
            }

            return sb.Length > 3 ? sb.ToString(0, sb.Length - 3) : sb.ToString();
        }

        private string GetDirInfoTsv(ScannedDirectory dir)
        {
            List<DirectoryColumnConfig> configs = GetVisibleColumnsConfig();
            IEnumerable<string> values = configs.Select(c => c.ValueSelector(dir));
            return string.Join("\t", values);
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
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning("Попытка копирования без выбранной директории");
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
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning("Попытка копирования TSV без выбранной директории");
                }
            }
        }

        private async Task CopySingleDirInfoAsync(ScannedDirectory dir, bool formatted)
        {
            string formatType = formatted ? "форматированно" : "TSV";

            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Копирование информации о директории ({FormatType}): {Path}", formatType, dir.FullPath);
            }

            string content = formatted
                ? GetDirInfoFormatted(dir)
                : BuildTsvContent([dir]);

            await Clipboard.Default.SetTextAsync(content);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("{FormatType}-информация о директории скопирована. Длина: {Length} символов",
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

            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Копирование информации о {Count} выбранных директориях (форматированно)", _selectedRows!.Count);
            }

            List<ScannedDirectory> sortedDirs = GetSortedSelectedDirs();
            StringBuilder sb = new();

            foreach (ScannedDirectory dir in sortedDirs)
            {
                sb.AppendLine(GetDirInfoFormatted(dir));
            }

            string content = sb.ToString();
            await Clipboard.Default.SetTextAsync(content);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Информация о {Count} директориях скопирована. Длина: {Length} символов",
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

            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Копирование информации о {Count} выбранных директориях (TSV)", _selectedRows!.Count);
            }

            List<ScannedDirectory> sortedDirs = GetSortedSelectedDirs();
            string content = BuildTsvContent(sortedDirs);

            await Clipboard.Default.SetTextAsync(content);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("TSV-информация о {Count} директориях скопирована. Длина: {Length} символов",
                    sortedDirs.Count, content.Length);
            }

            Snackbar.Add(L["Common_Copied"], Severity.Info);
        }

        private string BuildTsvContent(List<ScannedDirectory> dirs)
        {
            StringBuilder sb = new();
            sb.AppendLine(GetTsvHeaders());

            foreach (ScannedDirectory dir in dirs)
            {
                sb.AppendLine(GetDirInfoTsv(dir));
            }

            return sb.ToString();
        }

        private bool ValidateSelectedDirs(string operationName)
        {
            if (_selectedRows == null || _selectedRows.Count == 0)
            {
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning("Попытка {Operation}, но список пуст", operationName);
                }

                return false;
            }

            return true;
        }

        private ScannedDirectory? GetActiveDir()
        {
            return _contextRow ?? (_selectedRows?.Count == 1 ? _selectedRows.FirstOrDefault() : null);
        }

        private List<ScannedDirectory> GetSortedSelectedDirs()
        {
            if (_selectedRows == null || _selectedRows.Count == 0)
            {
                return [];
            }

            // Сортировка по индексу в исходном списке для сохранения порядка, как в оригинальной логике
            return [.. _selectedRows.OrderBy(_scannedDirs.IndexOf)];
        }

        #endregion

        #region Keyboard Shortcuts

        private async Task OnDataGridKeyDown(KeyboardEventArgs e)
        {
            if (_isScanning)
            {
                return;
            }

            if (_logger?.IsEnabled(LogLevel.Trace) == true)
            {
                _logger.LogTrace("Нажата клавиша: {Code}, Ctrl: {Ctrl}, Shift: {Shift}",
                    e.Code, e.CtrlKey, e.ShiftKey);
            }

            if (TryHandleCopyShortcut(e))
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
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug("Горячая клавиша: Ctrl+Shift+C (копирование TSV)");
                }

                _ = CopySingleInfoTsvAsync();
                return true;
            }

            if (e.Code == "KeyC")
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug("Горячая клавиша: Ctrl+C (копирование)");
                }

                _ = CopySingleInfoAsync();
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

            ScannedDirectory? activeDir = GetActiveDir();
            if (!activeDir.HasValue)
            {
                return;
            }

            if (e.ShiftKey && e.Code == "KeyO")
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug("Горячая клавиша: Ctrl+Shift+O (открытие директории)");
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

        #endregion
    }
}