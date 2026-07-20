using Glazecs.Shared.UI.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Globalization;

namespace Glazecs.App.Desktop.Components.Layout
{
    public partial class MainLayout : LayoutComponentBase
    {
        private bool _drawerOpen = true;
        private bool _isDarkMode = false;
        private bool _isLoaded = false;
        private MudThemeProvider? _mudThemeProvider;

        private static readonly CultureInfo RussianCulture = new("ru-RU");
        private static readonly CultureInfo EnglishCulture = new("en-US");

        private readonly MudTheme _customTheme = new()
        {
            // Светлая тема (PaletteLight)
            PaletteLight = new PaletteLight()
            {
                // --- Фоновые и поверхностные цвета (мягкие, для снижения контрастного стресса) ---
                Background = "#f8f9fa",          // Основной фон (очень светлый серо-голубой, не режет глаз)
                Surface = "#ffffff",             // Фон карточек, панелей и таблиц (чистый белый для объема)

                // --- Акцентные цвета (адаптированы для высокого контраста на светлом фоне) ---
                Primary = "#2563eb",             // Насыщенный профессиональный синий (темнее, чем в dark-теме, для читаемости)
                PrimaryContrastText = "#ffffff", // Белый текст на акцентных кнопках/элементах
                Secondary = "#64748b",           // Нейтральный серо-синий (второстепенные элементы)
                SecondaryContrastText = "#ffffff",

                // --- Текстовые цвета (темно-серые вместо чисто черных) ---
                TextPrimary = "#1f2937",         // Основной текст (глубокий серый, комфортный для длительного чтения)
                TextSecondary = "#4b5563",       // Второстепенный текст (метаданные, подписи)
                TextDisabled = "#9ca3af",        // Неактивный текст

                // --- Навигация и панели ---
                AppbarBackground = "#ffffff",
                AppbarText = "#1f2937",
                DrawerBackground = "#ffffff",
                DrawerText = "#1f2937",
                DrawerIcon = "#2563eb",          // Акцент на иконках в боковой панели (согласуется с Primary)

                // --- Таблицы (гармонизированы со светлым Surface) ---
                TableStriped = "#f9fafb",        // Чередующиеся строки (едва заметный серый оттенок)
                TableHover = "#f1f5f9",          // Эффект наведения (четкий, но мягкий акцент)

                // --- Статусные цвета (адаптированы для светлого фона, без "кислотности") ---
                Success = "#16a34a",             // Профессиональный зеленый
                Warning = "#d97706",             // Сдержанный янтарно-оранжевый
                Error = "#dc2626",               // Четкий, но не кричащий красный
                Info = "#0284c7",                // Спокойный голубой

                // --- Границы и разделители ---
                LinesDefault = "#e5e7eb",        // Цвет разделительных линий (светло-серый)
                LinesInputs = "#d1d5db",         // Цвет границ полей ввода (чуть темнее для видимости)

                // --- Оверлеи ---
                OverlayLight = "rgba(255, 255, 255, 0.7)" // Мягкое белое затемнение фона при модальных окнах
            },

            // Темная тема (PaletteDark)
            PaletteDark = new PaletteDark()
            {
                Background = "#1e1e1e",
                Surface = "#252526",
                Primary = "#4a90e2",
                PrimaryContrastText = "#ffffff",
                Secondary = "#607d8b",
                SecondaryContrastText = "#ffffff",
                TextPrimary = "#e0e0e0",
                TextSecondary = "#a0a0a0",
                TextDisabled = "#6e6e6e",
                AppbarBackground = "#252526",
                AppbarText = "#e0e0e0",
                DrawerBackground = "#252526",
                DrawerText = "#e0e0e0",
                DrawerIcon = "#4a90e2",
                TableStriped = "#2d2d30",
                TableHover = "#38383d",
                Success = "#66bb6a",
                Warning = "#ffa726",
                Error = "#ef5350",
                Info = "#29b6f6",
                LinesDefault = "#3e3e42",
                LinesInputs = "#3e3e42",
                OverlayDark = "rgba(0, 0, 0, 0.6)"
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
