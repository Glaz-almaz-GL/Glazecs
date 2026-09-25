using Glazecs.Modules.FMMS.Abstractions.Enums;
using Glazecs.Modules.FMMS.Abstractions.Models;
using Glazecs.Modules.FMMS.Resources.Languages;
using Glazecs.Modules.FMMS.Services;
using Glazecs.Modules.Hash.Abstractions.Interfaces;
using Glazecs.Shared.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace Glazecs.Modules.FMMS.Components.Pages
{
    public partial class FmmsSettingsView : ComponentBase, IModuleSettingsProvider
    {
        #region Injection

        [Inject] private IStringLocalizer<FmmsResources> L { get; set; } = default!;
        [Inject] private FmmsSettingsService SettingsService { get; set; } = default!;
        [Inject] private ISnackbar Snackbar { get; set; } = default!;
        [Inject] private IHashProviderFactory HashProviderFactory { get; set; } = default!;

        #endregion

        private string _newArchiveExt = "";
        private string _newRuleExt = "";
        private int _newRulePages = 1;
        private List<string> _availableAlgorithms = [];

        public string ModuleName => "FMMS";

        public string Icon => Icons.Material.Filled.FilePresent;

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

        private void OnAlgorithmToggled(string algorithm, bool isChecked)
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

            SaveExplicitly();
        }

        #endregion

        #region Columns Logic

        private void OnStandardColumnToggled(AnalyzeField column, bool isVisible)
        {
            SettingsService.FilesScanningSettings.AnalyzeSettings.FieldsToAnalyze[column] = isVisible;
            SaveExplicitly();
        }

        #endregion

        #region Archive Extensions Logic

        private void AddArchiveExtension()
        {
            if (TryNormalizeExtension(_newArchiveExt, out string? ext) && SettingsService.FilesScanningSettings.CustomArchiveExtensions.Add(ext))
            {
                _newArchiveExt = "";
                SaveExplicitly();
            }
        }

        private void RemoveArchiveExtension(string ext)
        {
            SettingsService.FilesScanningSettings.CustomArchiveExtensions.Remove(ext);
            SaveExplicitly();
        }

        #endregion

        #region Page Rules Logic

        private void AddPageRule()
        {
            if (TryNormalizeExtension(_newRuleExt, out string? ext))
            {
                SettingsService.FilesScanningSettings.PagesCountCustomRules[ext] = _newRulePages;
                _newRuleExt = "";
                _newRulePages = 1;
                SaveExplicitly();
            }
        }

        private void RemovePageRule(string key)
        {
            SettingsService.FilesScanningSettings.PagesCountCustomRules.Remove(key);
            SaveExplicitly();
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

            if (string.IsNullOrEmpty(Path.GetExtension(ext)))
            {
                Snackbar.Add(L["Settings_InvalidExtension"], Severity.Warning);
                return false;
            }

            normalizedExt = ext;
            return true;
        }

        private void OnIncludeHiddenSettingsChanged(bool value)
        {
            SettingsService.DirectoryScanningSettings.IncludeHidden = value;
            SaveExplicitly();
        }

        private void OnParallelismSettingsChanged(int value)
        {
            SettingsService.FilesScanningSettings.Hashing.MaxDegreeOfParallelism = value;
            SaveExplicitly();
        }

        private void OnCalculateSettingsChanged(bool value)
        {
            SettingsService.FilesScanningSettings.Hashing.CalculateInParallel = value;
            SaveExplicitly();
        }

        private void OnMaxSizeSettingsChanged(long value)
        {
            SettingsService.FilesScanningSettings.Hashing.MaxFileSizeBytes = value;
            SaveExplicitly();
        }

        private void OnSizeTypeSettingChanged(FileSizeType value)
        {
            SettingsService.FilesScanningSettings.DisplayedSizeType = value;
            SaveExplicitly();
        }

        private void OnArchiveSettingsChanged(bool value)
        {
            SettingsService.FilesScanningSettings.ScanArchives = value;
            SaveExplicitly();
        }

        private void SaveExplicitly()
        {
            SettingsService.SaveCurrent();
            Snackbar.Add(L["Settings_Saved_Success"], Severity.Success);
        }

        private void HandleEnterKey(KeyboardEventArgs args)
        {
            if (args.Key == "Enter")
            {
                AddArchiveExtension();
            }
        }

        private void HandleEnterKeyPages(KeyboardEventArgs args)
        {
            if (args.Key == "Enter")
            {
                AddPageRule();
            }
        }

        public async Task ResetToDefaultsAsync()
        {
            SettingsService.ResetToDefaults();
            await InvokeAsync(StateHasChanged);
        }

        #endregion
    }
}
