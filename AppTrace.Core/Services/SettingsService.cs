using System.Text.Json;
using AppTrace.Core.Models;

namespace AppTrace.Core.Services;

public interface ISettingsService
{
    AppSettings CurrentSettings { get; }
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    event EventHandler<AppSettings>? SettingsChanged;
}

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private AppSettings _currentSettings;

    public event EventHandler<AppSettings>? SettingsChanged;

    public AppSettings CurrentSettings => _currentSettings;

    public SettingsService(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath))
        {
            _settingsFilePath = customPath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appTraceDir = Path.Combine(appData, "AppTrace");
            _settingsFilePath = Path.Combine(appTraceDir, "settings.json");
        }

        _currentSettings = new AppSettings();
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                string json = await File.ReadAllTextAsync(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    _currentSettings = loaded;
                    return _currentSettings;
                }
            }
        }
        catch
        {
            // Fall back to defaults
        }

        _currentSettings = new AppSettings();
        await SaveSettingsAsync(_currentSettings);
        return _currentSettings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        _currentSettings = settings ?? new AppSettings();
        try
        {
            string dir = Path.GetDirectoryName(_settingsFilePath) ?? string.Empty;
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
            await File.ReadAllTextAsync(_settingsFilePath).ConfigureAwait(false); // test read
        }
        catch
        {
            // ignore if not existing
        }

        try
        {
            string dir = Path.GetDirectoryName(_settingsFilePath) ?? string.Empty;
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsFilePath, json);
            SettingsChanged?.Invoke(this, _currentSettings);
        }
        catch
        {
            // Failed to save settings
        }
    }
}
