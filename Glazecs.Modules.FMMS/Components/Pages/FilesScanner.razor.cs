using CommunityToolkit.Maui.Storage;
using Glazecs.Modules.FMMS.Abstractions.Enums;
using Glazecs.Modules.FMMS.Abstractions.Models;
using Glazecs.Modules.FMMS.Models;
using Glazecs.Shared.Core.Extensions;
using Glazecs.Shared.Core.Platforms;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Diagnostics;
using System.Text;

namespace Glazecs.Modules.FMMS.Components.Pages
{
    public partial class FilesScanner(ILogger<FilesScanner>? logger = null) : ComponentBase, IDisposable
    {
        private readonly ILogger<FilesScanner>? _logger = logger;

        #region State

        private string _dirPath = string.Empty;
        private bool _isScanning;
        private int _processedFilesCount;
        private CancellationTokenSource? _cts;
        private ScannedFile? _contextRow;
        private MudMenu _contextMenu = null!;
        private readonly List<ScannedFile> _scannedFiles = [];
        private HashSet<ScannedFile>? _selectedRows;
        private List<FileColumnConfig> _visibleColumnsCache = [];
        private bool _showManualPathDialog;
        private string _manualPathInput = string.Empty;
        private bool _disposed;
        private const int ThrottleIntervalMs = 200;
        private const int BatchSize = 250;

        #endregion

        #region Properties

        private FileSizeType DisplayedSizeType => SettingsService.FilesScanningSettings.DisplayedSizeType;

        #endregion

        #region Folder Selection

        private async Task SelectFolderAsync()
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Начало выбора папки через FolderPicker");
            }

            if (!PlatformSupport.IsFolderPickerSupported)
            {
                await HandleUnsupportedPlatformAsync();
                return;
            }

            await TryPickFolderAsync();
        }

        private async Task HandleUnsupportedPlatformAsync()
        {
            string reason = PlatformSupport.GetFolderPickerUnsupportedReason()
                ?? "Платформа не поддерживается.";

            if (_logger?.IsEnabled(LogLevel.Warning) == true)
            {
                _logger.LogWarning(
                    "FolderPicker вызван на неподдерживаемой платформе: {Platform}. Причина: {Reason}",
                    PlatformSupport.GetCurrentPlatformDescription(),
                    reason);
            }

            Snackbar.Add(reason, Severity.Warning);
            await ShowManualPathInputDialogAsync();
        }

        private async Task TryPickFolderAsync()
        {
            try
            {
#pragma warning disable CA1416
                FolderPickerResult result = await FolderPicker.Default.PickAsync();
#pragma warning restore CA1416

                HandleFolderPickerResult(result);
            }
            catch (PlatformNotSupportedException ex)
            {
                await HandlePlatformNotSupportedException(ex);
            }
            catch (Exception ex)
            {
                HandleFolderPickerException(ex);
            }
        }

        private void HandleFolderPickerResult(FolderPickerResult result)
        {
            if (result.IsSuccessful && !string.IsNullOrEmpty(result.Folder?.Path))
            {
                _dirPath = result.Folder.Path;

                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("Папка успешно выбрана: {Path}", _dirPath);
                }

                StateHasChanged();
            }
            else
            {
                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("Пользователь отменил выбор папки");
                }
            }
        }

        private async Task HandlePlatformNotSupportedException(PlatformNotSupportedException ex)
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
            {
                _logger.LogError(ex,
                    "PlatformNotSupportedException при вызове FolderPicker. Платформа: {Platform}",
                    PlatformSupport.GetCurrentPlatformDescription());
            }

            Snackbar.Add("Выбор папки не поддерживается на этой платформе.", Severity.Error);
            await ShowManualPathInputDialogAsync();
        }

        private void HandleFolderPickerException(Exception ex)
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
            {
                _logger.LogError(ex, "Ошибка при выборе папки через FolderPicker");
            }

            Snackbar.Add($"Ошибка при выборе папки: {ex.Message}", Severity.Error);
        }

        private async Task ShowManualPathInputDialogAsync()
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Открытие диалога ручного ввода пути");
            }

            _manualPathInput = _dirPath ?? string.Empty;
            _showManualPathDialog = true;
            StateHasChanged();
        }

        private void ConfirmManualPath()
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Попытка подтверждения ручного пути: {Path}", _manualPathInput);
            }

            if (!string.IsNullOrWhiteSpace(_manualPathInput) && Directory.Exists(_manualPathInput))
            {
                _dirPath = _manualPathInput;
                _showManualPathDialog = false;

                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("Путь успешно установлен вручную: {Path}", _dirPath);
                }

                StateHasChanged();
            }
            else
            {
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning("Ручной ввод пути невалиден: {Path}", _manualPathInput);
                }

                Snackbar.Add("Указанный путь не существует или пуст.", Severity.Error);
            }
        }

        private void CancelManualPath()
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Пользователь отменил ручной ввод пути");
            }

            _showManualPathDialog = false;
            StateHasChanged();
        }

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
            _processedFilesCount = 0;
            _scannedFiles.Clear();
            _selectedRows?.Clear();
            _visibleColumnsCache.Clear();
            _cts = new CancellationTokenSource();
        }

        private async Task ProcessFilesAsync(Stopwatch sw)
        {
            List<ScannedFile> tempList = new(capacity: BatchSize);
            long lastUiUpdate = Environment.TickCount64;
            CancellationToken token = _cts?.Token ?? CancellationToken.None;

            await foreach (ScannedFile file in ScannerService.ScanDirectoryAsync(
                _dirPath,
                SettingsService.FilesScanningSettings,
                progress: null,
                token))
            {
                tempList.Add(file);

                long currentTime = Environment.TickCount64;
                bool isBatchFull = tempList.Count >= BatchSize;
                bool isTimeToUpdate = (currentTime - lastUiUpdate) >= ThrottleIntervalMs && tempList.Count > 0;

                if (isBatchFull || isTimeToUpdate)
                {
                    await FlushBatchAsync(tempList);
                    lastUiUpdate = Environment.TickCount64;

                    // Отдаём управление UI потоку
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
                    "Сканирование файлов завершено успешно. Найдено файлов: {Count}, Время: {ElapsedMs} мс",
                    _scannedFiles.Count, sw.ElapsedMilliseconds);
            }

            Snackbar.Add(L["Scanner_Completed_Success"], Severity.Success);
        }

        private async Task FlushBatchAsync(List<ScannedFile> tempList)
        {
            _scannedFiles.AddRange(tempList);
            _processedFilesCount = _scannedFiles.Count;
            tempList.Clear();

            if (_logger?.IsEnabled(LogLevel.Trace) == true)
            {
                _logger.LogTrace("Пакетное обновление UI: добавлено {BatchSize} файлов. Всего: {Total}",
                    BatchSize, _scannedFiles.Count);
            }

            await InvokeAsync(StateHasChanged);
        }

        private void HandleScanningCancellation(OperationCanceledException ex, Stopwatch sw)
        {
            sw.Stop();

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(ex,
                    "Сканирование файлов отменено пользователем. Обработано файлов: {Count}, Время: {ElapsedMs} мс",
                    _scannedFiles.Count, sw.ElapsedMilliseconds);
            }

            Snackbar.Add(L["Scanner_Cancelled"], Severity.Warning);
        }

        private void HandleScanningError(Exception ex, Stopwatch sw)
        {
            sw.Stop();

            if (_logger?.IsEnabled(LogLevel.Error) == true)
            {
                _logger.LogError(ex,
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
            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Запрос отмены сканирования файлов. IsScanning: {IsScanning}", _isScanning);
            }

            _cts?.Cancel();
        }

        #endregion

        #region UI Interactions

        private async Task OpenMenuContent(DataGridRowClickEventArgs<ScannedFile> args)
        {
            _contextRow = args.Item;

            if (_logger?.IsEnabled(LogLevel.Trace) == true)
            {
                _logger.LogTrace("Открытие контекстного меню для файла: {Path}", args.Item.FullPath);
            }

            await _contextMenu.OpenMenuAsync(args.MouseEventArgs);
        }

        private void SelectedItemsChanged(HashSet<ScannedFile> items)
        {
            _selectedRows = items;

            if (_logger?.IsEnabled(LogLevel.Trace) == true)
            {
                _logger.LogTrace("Изменение выбора файлов. Выбрано: {Count}", items?.Count ?? 0);
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

            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Инициализация конфигурации видимых колонок");
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
            ColumnVisibilitySettings visibleSettings = settings.Columns;

            TryAddColumn(configs, visibleSettings, FileColumn.Id, true, "Table_Id",
                f => f.Id.ToString());

            TryAddColumn(configs, visibleSettings, FileColumn.Name, true, "Table_Name",
                f => f.Name);

            TryAddColumn(configs, visibleSettings, FileColumn.Extension, true, "Table_Extension",
                f => f.Extension);

            TryAddColumn(configs, visibleSettings, FileColumn.Size, true, "Table_Size",
                f => FormatSize(f.Size));

            TryAddColumn(configs, visibleSettings, FileColumn.PagesCount, true, "Table_Pages_Count",
                f => f.PagesCount > 0 ? f.PagesCount.ToString() : "-");

            TryAddColumn(configs, visibleSettings, FileColumn.IsArchive, false, "Table_Is_Archive",
                f => f.IsArchive ? "Yes" : "No");

            TryAddColumn(configs, visibleSettings, FileColumn.IsArchiveEntry, false, "Table_Is_Archive_Entry",
                f => f.IsArchiveEntry ? "Yes" : "No");

            TryAddColumn(configs, visibleSettings, FileColumn.FullPath, false, "Table_Full_Path",
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
            ColumnVisibilitySettings visibleSettings,
            FileColumn column,
            bool defaultValue,
            string headerKey,
            Func<ScannedFile, string> valueSelector)
        {
            if (visibleSettings.StandardColumns.GetValueOrDefault(column, defaultValue))
            {
                configs.Add(new FileColumnConfig(headerKey, valueSelector));
            }
        }

        #endregion

        #region File Actions

        private void OpenFile(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning("Попытка открыть файл с пустым путём");
                }

                return;
            }

            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Открытие файла: {Path}", filePath);
            }

            try
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });

                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("Файл успешно открыт: {Path}", filePath);
                }
            }
            catch (Exception ex)
            {
                if (_logger?.IsEnabled(LogLevel.Error) == true)
                {
                    _logger.LogError(ex, "Ошибка при открытии файла: {Path}", filePath);
                }

                Snackbar.Add(ex.Message, Severity.Error, config => config.RequireInteraction = true);
            }
        }

        private void OpenFileDirectory(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning("Попытка открыть папку файла с пустым путём");
                }

                return;
            }

            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Открытие папки файла: {Path}", filePath);
            }

            try
            {
                if (TryOpenFileDirectoryByPlatform(filePath) && _logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("Папка файла успешно открыта: {Path}", filePath);
                }
            }
            catch (Exception ex)
            {
                if (_logger?.IsEnabled(LogLevel.Error) == true)
                {
                    _logger.LogError(ex, "Ошибка при открытии папки файла: {Path}", filePath);
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

            if (_logger?.IsEnabled(LogLevel.Warning) == true)
            {
                _logger.LogWarning("Открытие папки не поддерживается на этой платформе");
            }

            Snackbar.Add("Открытие папки не поддерживается на этой платформе.", Severity.Warning);
            return false;
#pragma warning restore S4036
        }

        #endregion

        #region Clipboard Operations

        private string GetFileInfoFormatted(ScannedFile file)
        {
            StringBuilder sb = new();
            List<FileColumnConfig> configs = GetVisibleColumnsConfig();

            foreach (FileColumnConfig config in configs)
            {
                string header = L[config.HeaderKey];
                string value = config.ValueSelector(file);
                sb.Append($"{header}: {value} | ");
            }

            return sb.Length > 3 ? sb.ToString(0, sb.Length - 3) : sb.ToString();
        }

        private string GetFileInfoTsv(ScannedFile file)
        {
            List<FileColumnConfig> configs = GetVisibleColumnsConfig();
            IEnumerable<string> values = configs.Select(c => c.ValueSelector(file));
            return string.Join("\t", values);
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
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning("Попытка копирования без выбранного файла");
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
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning("Попытка копирования TSV без выбранного файла");
                }
            }
        }

        private async Task CopySingleFileInfoAsync(ScannedFile file, bool formatted)
        {
            string formatType = formatted ? "форматированно" : "TSV";

            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Копирование информации о файле ({FormatType}): {Path}", formatType, file.FullPath);
            }

            string content = formatted
                ? GetFileInfoFormatted(file)
                : BuildTsvContent([file]);

            await Clipboard.Default.SetTextAsync(content);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("{FormatType}-информация о файле скопирована. Длина: {Length} символов",
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

            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Копирование информации о {Count} выбранных файлах (форматированно)", _selectedRows!.Count);
            }

            List<ScannedFile> sortedFiles = GetSortedSelectedFiles();
            StringBuilder sb = new();

            foreach (ScannedFile file in sortedFiles)
            {
                sb.AppendLine(GetFileInfoFormatted(file));
            }

            string content = sb.ToString();
            await Clipboard.Default.SetTextAsync(content);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Информация о {Count} файлах скопирована. Длина: {Length} символов",
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

            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Копирование информации о {Count} выбранных файлах (TSV)", _selectedRows!.Count);
            }

            List<ScannedFile> sortedFiles = GetSortedSelectedFiles();
            string content = BuildTsvContent(sortedFiles);

            await Clipboard.Default.SetTextAsync(content);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("TSV-информация о {Count} файлах скопирована. Длина: {Length} символов",
                    sortedFiles.Count, content.Length);
            }

            Snackbar.Add(L["Common_Copied"], Severity.Info);
        }

        private string BuildTsvContent(List<ScannedFile> files)
        {
            StringBuilder sb = new();
            sb.AppendLine(GetTsvHeaders());

            foreach (ScannedFile file in files)
            {
                sb.AppendLine(GetFileInfoTsv(file));
            }

            return sb.ToString();
        }

        private bool ValidateSelectedFiles(string operationName)
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

        private ScannedFile? GetActiveFile()
        {
            return _contextRow ?? (_selectedRows?.Count == 1 ? _selectedRows.FirstOrDefault() : null);
        }

        private List<ScannedFile> GetSortedSelectedFiles()
        {
            return _selectedRows == null || _selectedRows.Count == 0 ? [] : [.. _selectedRows.OrderBy(f => f.Id)];
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

            ScannedFile? activeFile = GetActiveFile();
            if (!activeFile.HasValue)
            {
                return;
            }

            if (e.ShiftKey && e.Code == "KeyO")
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug("Горячая клавиша: Ctrl+Shift+O (открытие папки файла)");
                }

                OpenFileDirectory(activeFile.Value.FullPath);
            }
            else if (e.Code == "KeyO")
            {
                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug("Горячая клавиша: Ctrl+O (открытие файла)");
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

        #endregion
    }
}