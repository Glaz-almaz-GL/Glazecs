using Glazecs.Shared.UI.Interfaces;
using Glazecs.Shared.UI.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace Glazecs.App.Desktop.Services
{
    public sealed class AppSettingsService : IAppSettingsService
    {
        private readonly string _filePath;
        private readonly ILogger<AppSettingsService>? _logger;
        private readonly object _fileLock = new();

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new CultureInfoJsonConverter() }
        };

        public event Action<AppSettings>? OnSettingsChanged;
        public AppSettings Settings { get; private set; } = new();

        public AppSettingsService(ILogger<AppSettingsService>? logger = null)
        {
            _logger = logger;
            string appDataDirectory = FileSystem.AppDataDirectory;
            _filePath = Path.Combine(appDataDirectory, "app_settings.json");

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Settings service initialized. File path: {FilePath}", _filePath);
            }

            Load();
        }

        public void Load()
        {
            lock (_fileLock)
            {
                try
                {
                    if (!File.Exists(_filePath))
                    {
                        if (_logger?.IsEnabled(LogLevel.Warning) == true)
                        {
                            _logger.LogWarning("Settings file not found at {FilePath}. Default settings will be used.", _filePath);
                        }
                        return;
                    }

                    string json = File.ReadAllText(_filePath);
                    AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);

                    if (loaded != null)
                    {
                        Settings = loaded;
                        ValidateSettings();
                        ApplyCultureInfo();

                        if (_logger?.IsEnabled(LogLevel.Information) == true)
                        {
                            _logger.LogInformation("Settings loaded successfully. Language: {Language}", Settings.Culture.Name);
                        }
                    }
                }
                catch (JsonException jsonEx)
                {
                    if (_logger?.IsEnabled(LogLevel.Error) == true)
                    {
                        _logger.LogError(jsonEx, "Settings deserialization error. File is corrupted.");
                    }
                    HandleCorruptedFile();
                }
                catch (Exception ex)
                {
                    if (_logger?.IsEnabled(LogLevel.Error) == true)
                    {
                        _logger.LogError(ex, "Unexpected error during settings loading.");
                    }
                    Settings = new AppSettings();
                }
            }
        }

        public void Save()
        {
            lock (_fileLock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(Settings, _jsonOptions);

                    // Create directory if it doesn't exist
                    string? directory = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(_filePath, json);

                    if (_logger?.IsEnabled(LogLevel.Information) == true)
                    {
                        _logger.LogInformation("Settings saved successfully. Language: {Language}", Settings.Culture.Name);
                    }

                    // Notify subscribers about changes
                    ApplyCultureInfo();
                    OnSettingsChanged?.Invoke(Settings);
                }
                catch (Exception ex)
                {
                    if (_logger?.IsEnabled(LogLevel.Error) == true)
                    {
                        _logger.LogError(ex, "Critical error during settings saving.");
                    }
                }
            }
        }

        private void ApplyCultureInfo()
        {
            CultureInfo.CurrentCulture = Settings.Culture;
            CultureInfo.CurrentUICulture = Settings.Culture;
            CultureInfo.DefaultThreadCurrentCulture = Settings.Culture;
            CultureInfo.DefaultThreadCurrentUICulture = Settings.Culture;
        }

        /// <summary>
        /// Method to handle corrupted settings file.
        /// </summary>
        private void HandleCorruptedFile()
        {
            try
            {
                string backupPath = $"{_filePath}.bak_{DateTime.Now:yyyyMMdd_HHmmss}";
                File.Move(_filePath, backupPath);
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning("Corrupted settings file renamed to {BackupPath}. Default settings created.", backupPath);
                }
            }
            catch (Exception ex)
            {
                if (_logger?.IsEnabled(LogLevel.Error) == true)
                {
                    _logger.LogError(ex, "Failed to create backup of corrupted settings file.");
                }
            }

            Settings = new AppSettings();
        }

        private void ValidateSettings()
        {
            if (Settings.Culture == default)
            {
                Settings.Culture = CultureInfo.CurrentCulture;
                _logger?.LogWarning("Invalid language setting. Using current culture: {Language}", Settings.Culture.Name);
            }
        }

        public void ResetToDefaults()
        {
            Settings = new AppSettings();
            Save();

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Settings reset to defaults.");
            }
        }
    }
}
