using CommunityToolkit.Maui.Storage;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace Glazecs.Shared.UI.Components
{
    public partial class PathPickerComponent : ComponentBase
    {
        #region Parameters

        [Parameter] public string Label { get; set; } = string.Empty;
        [Parameter] public string Value { get; set; } = string.Empty;
        [Parameter] public EventCallback<string> ValueChanged { get; set; }
        [Parameter] public bool Disabled { get; set; }
        [Parameter] public bool FullWidth { get; set; } = true;
        [Parameter] public string Placeholder { get; set; } = "Укажите путь...";
        [Parameter] public string TooltipText { get; set; } = "Выбрать";
        [Parameter] public string Icon { get; set; } = Icons.Material.Filled.FolderOpen;
        [Parameter] public required string IconStyle { get; set; }

        /// <summary>
        /// Если true, используется FolderPicker и проверка Directory.Exists.
        /// Если false, используется FilePicker и проверка File.Exists.
        /// </summary>
        [Parameter] public bool IsFolder { get; set; } = true;

        [Parameter] public Dictionary<DevicePlatform, IEnumerable<string>>? FileTypes { get; set; }

        #endregion

        #region State

        private bool _showManualPathDialog;
        private string _manualPathInput = string.Empty;
        private string _tooltipStyle => FullWidth ? "width: 100%;" : string.Empty;

        #endregion

        #region Logic

        private async Task SelectPathAsync()
        {
            if (Logger?.IsEnabled(LogLevel.Debug) == true)
            {
                Logger.LogDebug("Инициация выбора {PathType} через нативный Picker", IsFolder ? "папки" : "файла");
            }
            await TryPickPathAsync();
        }

        private async Task TryPickPathAsync()
        {
            try
            {
#pragma warning disable CA1416
                if (IsFolder)
                {
                    FolderPickerResult result = await FolderPicker.Default.PickAsync();
                    HandleFolderResult(result);
                }
                else
                {
                    PickOptions options = new() { PickerTitle = "Выбор файла" };
                    if (FileTypes != null)
                    {
                        options.FileTypes = new FilePickerFileType(FileTypes);
                    }

                    FileResult? result = await FilePicker.Default.PickAsync(options);
                    HandleFileResult(result);
                }
#pragma warning restore CA1416
            }
            catch (PlatformNotSupportedException ex)
            {
                await HandlePlatformNotSupportedException(ex);
            }
            catch (Exception ex)
            {
                HandlePickerException(ex);
            }
        }

        private async Task OnValueChanged(string newValue)
        {
            Value = newValue;
            await ValueChanged.InvokeAsync(newValue);
        }

        private void HandleFolderResult(FolderPickerResult result)
        {
            if (result.IsSuccessful && !string.IsNullOrEmpty(result.Folder?.Path))
            {
                _ = OnValueChanged(result.Folder.Path);

                if (Logger?.IsEnabled(LogLevel.Information) == true)
                {
                    Logger?.LogInformation("Папка успешно выбрана: {Path}", result.Folder.Path);
                }
            }
        }

        private void HandleFileResult(FileResult? result)
        {
            if (result != null && !string.IsNullOrEmpty(result.FullPath))
            {
                _ = OnValueChanged(result.FullPath);

                if (Logger?.IsEnabled(LogLevel.Information) == true)
                {
                    Logger.LogInformation("Файл успешно выбран: {Path}", result.FullPath);
                }
            }
        }

        private async Task HandlePlatformNotSupportedException(PlatformNotSupportedException ex)
        {
            Logger?.LogWarning(ex, "Picker вызван на неподдерживаемой платформе.");
            Snackbar.Add("Автоматический выбор не поддерживается. Введите путь вручную.", Severity.Warning);
            await ShowManualPathInputDialogAsync();
        }

        private void HandlePickerException(Exception ex)
        {
            Logger?.LogError(ex, "Ошибка при выборе через нативный Picker");
            Snackbar.Add($"Ошибка при выборе: {ex.Message}", Severity.Error);
        }

        private async Task ShowManualPathInputDialogAsync()
        {
            _manualPathInput = Value;
            _showManualPathDialog = true;
            await InvokeAsync(StateHasChanged);
        }

        private async Task ConfirmManualPath()
        {
            if (!string.IsNullOrWhiteSpace(_manualPathInput) && IsPathValid(_manualPathInput))
            {
                await OnValueChanged(_manualPathInput);
                _showManualPathDialog = false;

                if (Logger?.IsEnabled(LogLevel.Information) == true)
                {
                    Logger.LogInformation("Путь успешно установлен вручную: {Path}", Value);
                }

                StateHasChanged();
            }
            else
            {
                Snackbar.Add($"Указанный {(IsFolder ? "путь к папке" : "файл")} не существует или пуст.", Severity.Error);
            }
        }

        private void CancelManualPath()
        {
            _showManualPathDialog = false;
            StateHasChanged();
        }

        private bool IsPathValid(string path)
        {
            return IsFolder ? Directory.Exists(path) : File.Exists(path);
        }

        #endregion
    }
}