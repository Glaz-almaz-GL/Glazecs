using Glazecs.Modules.FMMS.Abstractions.Enums;
using Glazecs.Modules.FMMS.Abstractions.Models;
using Glazecs.Shared.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace Glazecs.Modules.FMMS.Components.Pages
{
    public partial class FmmsSettingsView : ComponentBase, IModuleSettingsProvider
    {
        private string _newArchiveExt = "";
        private string _newRuleExt = "";
        private int _newRulePages = 1;
        private List<string> _availableAlgorithms = [];

        public string ModuleName => "FMMS";

        public string Icon => Icons.Material.Filled.PermMedia;

        public Type SettingsComponentType => typeof(FmmsSettingsView);

        protected override void OnInitialized()
        {
            _availableAlgorithms = [.. HashProviderFactory.GetAvailableAlgorithms()];
            base.OnInitialized();
        }

        private string GetLocalizedColumnName(AnalyzeField column)
        {
            return L[$"Column_{column}"]?.Value ?? column.ToString();
        }

        #region Hashing Logic

        private async Task OnAlgorithmToggled(string algorithm, bool isChecked)
        {
            HashingSettings hashSettings = SettingsService.FilesScanningSettings.Hashing;

            if (isChecked)
            {
                hashSettings.AlgorithmsToCalculate.Add(algorithm);
            }
            else
            {
                hashSettings.AlgorithmsToCalculate.Remove(algorithm);
            }

            await SaveExplicitly();
        }

        #endregion

        #region Columns Logic

        private async Task OnStandardColumnToggled(AnalyzeField column, bool isVisible)
        {
            SettingsService.FilesScanningSettings.AnalyzeSettings.FieldsToAnalyze[column] = isVisible;
            await SaveExplicitly();
        }

        #endregion

        #region Archive Extensions Logic

        private async Task AddArchiveExtension()
        {
            if (TryNormalizeExtension(_newArchiveExt, out string? ext) && SettingsService.FilesScanningSettings.CustomArchiveExtensions.Add(ext))
            {
                _newArchiveExt = "";
                await SaveExplicitly();
            }
        }

        private async Task RemoveArchiveExtension(string ext)
        {
            SettingsService.FilesScanningSettings.CustomArchiveExtensions.Remove(ext);
            await SaveExplicitly();
        }

        #endregion

        #region Page Rules Logic

        private async Task AddPageRule()
        {
            if (TryNormalizeExtension(_newRuleExt, out string? ext))
            {
                SettingsService.FilesScanningSettings.PagesCountCustomRules[ext] = _newRulePages;
                _newRuleExt = "";
                _newRulePages = 1;
                await SaveExplicitly();
            }
        }

        private async Task RemovePageRule(string key)
        {
            SettingsService.FilesScanningSettings.PagesCountCustomRules.Remove(key);
            await SaveExplicitly();
        }

        #endregion

        #region Helpers & Validation

        /// <summary>
        /// Нормализует и валидирует расширение файла.
        /// </summary>
        private bool TryNormalizeExtension(string input, out string normalizedExt)
        {
            normalizedExt = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string ext = input.Trim().ToLowerInvariant();
            if (!ext.StartsWith('.'))
            {
                ext = "." + ext;
            }

            if (!string.IsNullOrEmpty(Path.GetExtension(ext)))
            {
                Snackbar.Add(L["Settings_InvalidExtension"], Severity.Warning);
                return false;
            }

            normalizedExt = ext;
            return true;
        }

        private async Task OnIncludeHiddenSettingsChanged(bool value)
        {
            SettingsService.DirectoryScanningSettings.IncludeHidden = value;
            await SaveExplicitly();
        }

        private async Task OnParallelismSettingsChanged(int value)
        {
            SettingsService.FilesScanningSettings.Hashing.MaxDegreeOfParallelism = value;
            await SaveExplicitly();
        }

        private async Task OnCalculateSettingsChanged(bool value)
        {
            SettingsService.FilesScanningSettings.Hashing.CalculateInParallel = value;
            await SaveExplicitly();
        }

        private async Task OnMaxSizeSettingsChanged(long value)
        {
            SettingsService.FilesScanningSettings.Hashing.MaxFileSizeBytes = value;
            await SaveExplicitly();
        }

        private async Task OnSizeTypeSettingChanged(FileSizeType value)
        {
            SettingsService.FilesScanningSettings.DisplayedSizeType = value;
            await SaveExplicitly();
        }

        private async Task OnArchiveSettingsChanged(bool value)
        {
            SettingsService.FilesScanningSettings.ScanArchives = value;
            await SaveExplicitly();
        }

        private async Task SaveExplicitly()
        {
            await SettingsService.SaveCurrentAsync();
            Snackbar.Add(L["Settings_Saved_Success"], Severity.Success);
        }

        private async Task HandleEnterKey(KeyboardEventArgs args)
        {
            if (args.Key == "Enter")
            {
                await AddArchiveExtension();
            }
        }

        private async Task HandleEnterKeyPages(KeyboardEventArgs args)
        {
            if (args.Key == "Enter")
            {
                await AddPageRule();
            }
        }

        public async Task ResetToDefaultsAsync()
        {
            await SettingsService.ResetToDefaultsAsync();
            await InvokeAsync(StateHasChanged);
        }

        #endregion
    }
}
