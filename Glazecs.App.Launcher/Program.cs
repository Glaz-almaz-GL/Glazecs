using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Glazecs.App.Launcher
{
    /// <summary>
    /// Запускает приложение из подпапки app\ с той же командной строкой и сразу завершается.
    /// </summary>
    internal static class Program
    {
        private const string AppFolder = "app";
        private const string AppExecutable = "Glazecs.exe";
        private const uint MbIconError = 0x10;

        private static int Main()
        {
            string appPath = Path.Combine(AppContext.BaseDirectory, AppFolder, AppExecutable);

            if (!File.Exists(appPath))
            {
                ShowError($"Не найден файл приложения:\n{appPath}\n\nПереустановите Glazecs.");
                return 1;
            }

            ProcessStartInfo startInfo = new(appPath, GetArgumentsTail(Environment.CommandLine))
            {
                UseShellExecute = false,
                WorkingDirectory = Environment.CurrentDirectory
            };

            try
            {
                using Process? process = Process.Start(startInfo);
                return 0;
            }
            catch (Win32Exception ex)
            {
                ShowError($"Не удалось запустить приложение:\n{appPath}\n\n{ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Командная строка без имени самого лаунчера — передаётся приложению как есть, без перекавычивания.
        /// </summary>
        internal static string GetArgumentsTail(string commandLine)
        {
            string line = commandLine.TrimStart();
            int end;

            if (line.StartsWith("\"", StringComparison.Ordinal))
            {
                int closingQuote = line.IndexOf('"', 1);
                end = closingQuote < 0 ? line.Length : closingQuote + 1;
            }
            else
            {
                int space = line.IndexOfAny([' ', '\t']);
                end = space < 0 ? line.Length : space;
            }

            return line.Substring(end).TrimStart();
        }

        private static void ShowError(string text)
        {
            _ = MessageBoxW(IntPtr.Zero, text, "Glazecs", MbIconError);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
    }
}
