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
        }

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

                SavedSettingsContainer? savedSettings = JsonSerializer.Deserialize<SavedSettingsContainer>(json, _jsonOptions);

                if (savedSettings is null ||
                    savedSettings.FilesScanningSettings is null ||
                    savedSettings.DirectoryScanningSettings is null)
                {
                    _logger?.LogWarning("FMMS settings file is empty or corrupted (null values). Resetting to defaults.");
                    await ResetToDefaultsAsync(cancellationToken).ConfigureAwait(false);
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

                await HandleCorruptedFileAsync();
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

                await ResetToDefaultsAsync(cancellationToken).ConfigureAwait(false);
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

        public async Task SaveCurrentAsync(CancellationToken cancellationToken = default)
        {
            await SaveAsync(FilesScanningSettings, DirectoryScanningSettings, cancellationToken).ConfigureAwait(false);
        }

        public async Task SaveAsync(FilesScanningSettings filesScanningSettings, DirectoryScanningSettings directoryScanningSettings, CancellationToken cancellationToken = default)
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
                    FilesScanningSettings = filesScanningSettings,
                    DirectoryScanningSettings = directoryScanningSettings
                };

                string json = JsonSerializer.Serialize(container, _jsonOptions);
                await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);

                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("FMMS settings saved successfully to {FilePath}.", _filePath);
                }

                OnSettingsSaved?.Invoke();
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
            await SaveCurrentAsync(cancellationToken).ConfigureAwait(false);

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

        private async Task HandleCorruptedFileAsync()
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

            await ResetToDefaultsAsync();
            OnSettingsLoaded?.Invoke();
        }

        internal sealed record SavedSettingsContainer
        {
            public FilesScanningSettings? FilesScanningSettings { get; set; }
            public DirectoryScanningSettings? DirectoryScanningSettings { get; set; }
        }
    }
}