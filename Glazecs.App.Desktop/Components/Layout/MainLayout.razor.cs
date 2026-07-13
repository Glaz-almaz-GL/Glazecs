using Glazecs.Shared.UI.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Globalization;

namespace Glazecs.App.Desktop.Components.Layout
{
    public sealed partial class MainLayout : LayoutComponentBase
    {
        private bool _drawerOpen = false;
        private bool _isDarkMode = false;
        private bool _isLoaded = false;
        private MudThemeProvider? _mudThemeProvider;

        private static readonly CultureInfo RussianCulture = new("ru-RU");
        private static readonly CultureInfo EnglishCulture = new("en-US");

        private readonly MudTheme _customTheme = new()
        {
            PaletteDark = new PaletteDark()
            {
                TableStriped = MudBlazor.Utilities.MudColor.Parse("32323b"),
                TableHover = MudBlazor.Utilities.MudColor.Parse("2c2c33")
            }
        };

        private CultureInfo CurrentCulture
        {
            get => CultureInfo.CurrentCulture;
            set
            {
                if (CultureInfo.CurrentCulture != value)
                {
                    ChangeCultureInfo(value);

                    AppSettings.Settings.Culture = value;
                    _ = SaveSettingsAsync();

                    if (_isLoaded)
                    {
                        NavManager.NavigateTo(NavManager.Uri, forceLoad: true);
                    }
                    else
                    {
                        StateHasChanged();
                    }
                }
            }
        }

        public void DrawerToggle()
        {
            _drawerOpen = !_drawerOpen;
        }

        private async Task ToggleThemeAsync()
        {
            await ToggleThemeAsync(AppSettings.Settings.ThemeMode);
        }

        private async Task ToggleThemeAsync(ThemeMode themeMode)
        {
            if (_mudThemeProvider == null)
            {
                return;
            }

            _isDarkMode = themeMode switch
            {
                ThemeMode.Dark => true,
                ThemeMode.Light => false,
                ThemeMode.System => await _mudThemeProvider.GetSystemDarkModeAsync(),
                _ => await _mudThemeProvider.GetSystemDarkModeAsync(),
            };

            await SaveSettingsAsync();
        }

        private async Task ToggleCultureAsync(CultureInfo cultureInfo)
        {
            CurrentCulture = cultureInfo;
            await SaveSettingsAsync();
        }

        protected override async Task OnInitializedAsync()
        {
            await AppSettings.LoadAsync();

            // Применяем сохраненный язык до первой отрисовки
            if (AppSettings.Settings.Culture != CultureInfo.CurrentCulture)
            {
                ChangeCultureInfo(AppSettings.Settings.Culture);
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender && _mudThemeProvider != null)
            {
                if (AppSettings.Settings.ThemeMode == ThemeMode.System)
                {
                    _isDarkMode = await _mudThemeProvider.GetSystemDarkModeAsync();
                }
                else
                {
                    await ToggleThemeAsync();
                }

                _isLoaded = true;
                StateHasChanged();
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                await AppSettings.SaveAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] Error saving settings: {ex.Message}");
            }
        }

        private static void ChangeCultureInfo(CultureInfo cultureInfo)
        {
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
        }
    }
}
