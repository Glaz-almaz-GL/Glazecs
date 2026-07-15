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
        private readonly SemaphoreSlim _saveLock = new(1, 1);

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        public event Action? OnSettingsChanged;

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
        }

        public FilesScanningSettings FilesScanningSettings { get; set; }
        public DirectoryScanningSettings DirectoryScanningSettings { get; set; }

        public async Task LoadAsync(CancellationToken cancellationToken = default)
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

                string json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
                ApplySettings(JsonSerializer.Deserialize<SavedSettingsContainer>(json, _jsonOptions));
            }
            catch (JsonException jsonEx)
            {
                if (_logger?.IsEnabled(LogLevel.Error) == true)
                {
                    _logger.LogError(jsonEx, "FMMS settings deserialization error. File is corrupted.");
                }
                HandleCorruptedFile();
            }
            catch (OperationCanceledException ex)
            {
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning(ex, "FMMS settings loading was canceled.");
                }
            }
            catch (Exception ex)
            {
                if (_logger?.IsEnabled(LogLevel.Error) == true)
                {
                    _logger.LogError(ex, "Unexpected error loading FMMS settings. Default settings will be used.");
                }
                ResetToDefaultValues();
            }
        }

        private void ApplySettings(SavedSettingsContainer? savedSettings)
        {
            if (savedSettings != null)
            {
                if (savedSettings.FilesScanningSettings != null)
                {
                    FilesScanningSettings = savedSettings.FilesScanningSettings;
                }

                if (savedSettings.DirectoryScanningSettings != null)
                {
                    DirectoryScanningSettings = savedSettings.DirectoryScanningSettings;
                }

                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("FMMS settings loaded successfully from {FilePath}.", _filePath);
                }
            }
        }

        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string? directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                SavedSettingsContainer container = new()
                {
                    FilesScanningSettings = FilesScanningSettings,
                    DirectoryScanningSettings = DirectoryScanningSettings
                };

                string json = JsonSerializer.Serialize(container, _jsonOptions);
                await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);

                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("FMMS settings saved successfully to {FilePath}.", _filePath);
                }

                OnSettingsChanged?.Invoke();
            }
            catch (OperationCanceledException ex)
            {
                if (_logger?.IsEnabled(LogLevel.Warning) == true)
                {
                    _logger.LogWarning(ex, "FMMS settings saving was canceled.");
                }
            }
            catch (Exception ex)
            {
                if (_logger?.IsEnabled(LogLevel.Error) == true)
                {
                    _logger.LogError(ex, "Critical error saving FMMS settings.");
                }
            }
            finally
            {
                _saveLock.Release();
            }
        }

        public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
        {
            ResetToDefaultValues();
            await SaveAsync(cancellationToken).ConfigureAwait(false);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("FMMS settings reset to defaults.");
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

            ResetToDefaultValues();
        }

        internal sealed class SavedSettingsContainer
        {
            public FilesScanningSettings? FilesScanningSettings { get; set; }
            public DirectoryScanningSettings? DirectoryScanningSettings { get; set; }
        }
    }
}