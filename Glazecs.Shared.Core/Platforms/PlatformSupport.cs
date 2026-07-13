using System;
using System.Collections.Generic;
using System.Text;

namespace Glazecs.Shared.Core.Platforms
{
    /// <summary>
    /// Предоставляет проверки поддержки platform-specific API в MAUI/Blazor Hybrid.
    /// Все методы учитывают минимальные версии ОС, требуемые соответствующими API.
    /// </summary>
    public static class PlatformSupport
    {
        /// <summary>
        /// Проверяет, поддерживается ли <c>FolderPicker.Default.PickAsync()</c> на текущей платформе.
        /// Требуемые платформы: iOS 14.0+, MacCatalyst 14.0+, Android, Tizen, Windows.
        /// </summary>
        public static bool IsFolderPickerSupported
        {
            get
            {
                // Browser (WASM) — не поддерживается в принципе
                if (OperatingSystem.IsBrowser())
                    return false;

                // iOS — требуется версия 14.0+
                if (OperatingSystem.IsIOS() && !OperatingSystem.IsIOSVersionAtLeast(14))
                    return false;

                // MacCatalyst — требуется версия 14.0+
                // ВАЖНО: IsMacCatalyst() возвращает true только для MacCatalyst,
                // а IsIOS() — только для "чистого" iOS. Они не пересекаются.
                if (OperatingSystem.IsMacCatalyst() && !OperatingSystem.IsMacCatalystVersionAtLeast(14))
                    return false;

                // Android, Windows — поддерживаются на всех версиях,
                // которые допускает сам MAUI (Android 5.0+, Tizen 6.0+, Windows 10+)
                if (OperatingSystem.IsAndroid())
                    return true;

                if (OperatingSystem.IsWindows())
                    return true;

                // Неизвестная платформа (Linux, watchOS, tvOS и т.п.) — не поддерживается
                return false;
            }
        }

        /// <summary>
        /// Возвращает человекочитаемую причину, почему <c>FolderPicker</c> не поддерживается.
        /// Если платформа поддерживается — возвращает <c>null</c>.
        /// </summary>
        public static string? GetFolderPickerUnsupportedReason()
        {
            if (IsFolderPickerSupported)
                return null;

            if (OperatingSystem.IsBrowser())
                return "Выбор папки не поддерживается в браузере (WASM). Введите путь вручную.";

            if (OperatingSystem.IsIOS() && !OperatingSystem.IsIOSVersionAtLeast(14))
                return "Выбор папки требует iOS 14.0 или новее.";

            if (OperatingSystem.IsMacCatalyst() && !OperatingSystem.IsMacCatalystVersionAtLeast(14))
                return "Выбор папки требует macOS Catalyst 14.0 или новее.";

            return "Выбор папки не поддерживается на этой платформе. Введите путь вручную.";
        }

        /// <summary>
        /// Возвращает краткое описание текущей платформы для логирования.
        /// </summary>
        public static string GetCurrentPlatformDescription()
        {
            StringBuilder sb = new();

            if (OperatingSystem.IsBrowser()) sb.Append("Browser (WASM)");
            else if (OperatingSystem.IsMacCatalyst()) sb.Append($"MacCatalyst {Environment.OSVersion.Version}");
            else if (OperatingSystem.IsIOS()) sb.Append($"iOS {Environment.OSVersion.Version}");
            else if (OperatingSystem.IsAndroid()) sb.Append($"Android {Environment.OSVersion.Version}");
            else if (OperatingSystem.IsWindows()) sb.Append($"Windows {Environment.OSVersion.Version}");
            else if (OperatingSystem.IsLinux()) sb.Append("Linux");
            else sb.Append("Unknown");

            return sb.ToString();
        }
    }
}
