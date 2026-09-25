using Glazecs.Modules.FMMS.Abstractions.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glazecs.Modules.FMMS.Services
{
    internal sealed class FmmsSettingsService
    {
        private readonly ILogger<FmmsSettingsService>? _logger;
        private readonly string _filePath;
        private readonly object _fileLock = new();

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        public event Action? OnSettingsSaved;
        public event Action? OnSettingsLoaded;

        public FilesScanningSettings FilesScanningSettings { get; private set; }
        public DirectoryScanningSettings DirectoryScanningSettings { get; private set; }

        public FmmsSettingsService(ILogger<FmmsSettingsService>? logger = null)
        {
            _logger = logger;

            // Cross-platform path for MAUI
            string appDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _filePath = Path.Combine(appDataDirectory, "fmms_settings.json");

            // Initialize with default values
            FilesScanningSettings = new FilesScanningSettings();
            DirectoryScanningSettings = new DirectoryScanningSettings();

            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("FMMS settings service initialized. File path: {FilePath}", _filePath);
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
                        if (_logger?.IsEnabled(LogLevel.Information) == true)
                        {
                            _logger.LogInformation("FMMS settings file not found at {FilePath}. Using defaults.", _filePath);
                        }
                        return;
                    }

                    string json = File.ReadAllText(_filePath);

                    SavedSettingsContainer? savedSettings = JsonSerializer.Deserialize<SavedSettingsContainer>(json, _jsonOptions);

                    if (savedSettings is null ||
                        savedSettings.FilesScanningSettings is null ||
                        savedSettings.DirectoryScanningSettings is null)
                    {
                        _logger?.LogWarning("FMMS settings file is empty or corrupted (null values). Resetting to defaults.");
                        HandleCorruptedFile();
                        return;
                    }

                    ApplySettings(savedSettings);
                }
                catch (JsonException jsonEx)
                {
                    if (_logger?.IsEnabled(LogLevel.Error) == true)
                    {
                        _logger.LogError(jsonEx, "FMMS settings deserialization error. File is corrupted.");
                    }

                    HandleCorruptedFile();
                }
                catch (Exception ex)
                {
                    if (_logger?.IsEnabled(LogLevel.Error) == true)
                    {
                        _logger.LogError(ex, "Unexpected error loading FMMS settings. Default settings will be used for this session; the file is left untouched.");
                    }

                    // Only in memory: the failure may be temporary (e.g. file locked), so don't overwrite saved settings
                    ResetToDefaultValues();
                }
            }
        }

        private void ApplySettings(SavedSettingsContainer? savedSettings)
        {
            ArgumentNullException.ThrowIfNull(savedSettings);
            ArgumentNullException.ThrowIfNull(savedSettings.FilesScanningSettings);
            ArgumentNullException.ThrowIfNull(savedSettings.DirectoryScanningSettings);

            FilesScanningSettings = savedSettings.FilesScanningSettings;
            DirectoryScanningSettings = savedSettings.DirectoryScanningSettings;

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("FMMS settings loaded successfully from {FilePath}.", _filePath);
            }

            OnSettingsLoaded?.Invoke();
        }

        public void SaveCurrent()
        {
            Save(FilesScanningSettings, DirectoryScanningSettings);
        }

        public void Save(FilesScanningSettings filesScanningSettings, DirectoryScanningSettings directoryScanningSettings)
        {
            lock (_fileLock)
            {
                try
                {
                    string? directory = Path.GetDirectoryName(_filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    SavedSettingsContainer container = new()
                    {
                        FilesScanningSettings = filesScanningSettings,
                        DirectoryScanningSettings = directoryScanningSettings
                    };

                    string json = JsonSerializer.Serialize(container, _jsonOptions);
                    File.WriteAllText(_filePath, json);

                    if (_logger?.IsEnabled(LogLevel.Information) == true)
                    {
                        _logger.LogInformation("FMMS settings saved successfully to {FilePath}.", _filePath);
                    }

                    OnSettingsSaved?.Invoke();
                }
                catch (Exception ex)
                {
                    if (_logger?.IsEnabled(LogLevel.Error) == true)
                    {
                        _logger.LogError(ex, "Critical error saving FMMS settings.");
                    }
                }
            }
        }

        public void ResetToDefaults()
        {
            ResetToDefaultValues();
            SaveCurrent();

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("FMMS settings have been reset to default values.");
            }
        }

        private void ResetToDefaultValues()
        {
            FilesScanningSettings = new FilesScanningSettings();
            DirectoryScanningSettings = new DirectoryScanningSettings();
        }

        private void HandleCorruptedFile()
        {
            try
            {
                string backupPath = $"{_filePath}.bak_{DateTime.Now:yyyyMMdd_HHmmss}";
                File.Move(_filePath, backupPath);

                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning("Corrupted FMMS settings file renamed to {BackupPath}. Default settings created.", backupPath);
                }
            }
            catch (Exception ex)
            {
                if (_logger?.IsEnabled(LogLevel.Error) == true)
                {
                    _logger.LogError(ex, "Failed to create backup of corrupted FMMS settings file.");
                }
            }

            ResetToDefaults();
            OnSettingsLoaded?.Invoke();
        }

        internal sealed record SavedSettingsContainer
        {
            public FilesScanningSettings? FilesScanningSettings { get; set; }
            public DirectoryScanningSettings? DirectoryScanningSettings { get; set; }
        }
    }
}
