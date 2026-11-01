using System.Collections.ObjectModel;
using System.IO;
using AppTrace.Core.Models;
using AppTrace.Core.Services;

namespace AppTrace.UI.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly MainViewModel _mainViewModel;
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _loc;
    public ILocalizationService Loc => _loc;

    private string _selectedTheme = "Dark";
    private string _selectedLanguage = "tr";
    private string _selectedBackdrop = "Mica";
    private double _windowOpacity = 0.96;
    private string _selectedAccentColor = "#3B82F6";
    private string _scanMode = "Conservative";
    private bool _sendToRecycleBin = true;
    private bool _createQuarantineBackup = true;
    private string _newExcludedPath = string.Empty;
    private string _databaseSizeFormatted = "Calculating...";
    private string _appVersion = "1.0.0 (Windows 11 Edition)";

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
                SaveSettings();
        }
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetProperty(ref _selectedLanguage, value))
            {
                _loc.SetLanguage(value);
                SaveSettings();
            }
        }
    }

    public string SelectedBackdrop
    {
        get => _selectedBackdrop;
        set
        {
            if (SetProperty(ref _selectedBackdrop, value))
                SaveSettings();
        }
    }

    public double WindowOpacity
    {
        get => _windowOpacity;
        set
        {
            if (SetProperty(ref _windowOpacity, value))
                SaveSettings();
        }
    }

    public string SelectedAccentColor
    {
        get => _selectedAccentColor;
        set
        {
            if (SetProperty(ref _selectedAccentColor, value))
                SaveSettings();
        }
    }

    public string ScanMode
    {
        get => _scanMode;
        set
        {
            if (SetProperty(ref _scanMode, value))
                SaveSettings();
        }
    }

    public bool SendToRecycleBin
    {
        get => _sendToRecycleBin;
        set
        {
            if (SetProperty(ref _sendToRecycleBin, value))
                SaveSettings();
        }
    }

    public bool CreateQuarantineBackup
    {
        get => _createQuarantineBackup;
        set
        {
            if (SetProperty(ref _createQuarantineBackup, value))
                SaveSettings();
        }
    }

    public string NewExcludedPath
    {
        get => _newExcludedPath;
        set => SetProperty(ref _newExcludedPath, value);
    }

    public string DatabaseSizeFormatted
    {
        get => _databaseSizeFormatted;
        set => SetProperty(ref _databaseSizeFormatted, value);
    }

    public string AppVersion
    {
        get => _appVersion;
        set => SetProperty(ref _appVersion, value);
    }

    public ObservableCollection<string> ExcludedPaths { get; } = new();

    public RelayCommand AddExcludedPathCommand { get; }
    public RelayCommand RemoveExcludedPathCommand { get; }
    public RelayCommand ResetSettingsCommand { get; }
    public RelayCommand SelectAccentColorCommand { get; }

    public SettingsViewModel(
        MainViewModel mainViewModel,
        ISettingsService settingsService,
        ILocalizationService localizationService)
    {
        _mainViewModel = mainViewModel;
        _settingsService = settingsService;
        _loc = localizationService;

        AddExcludedPathCommand = new RelayCommand(ExecuteAddExcludedPath);
        RemoveExcludedPathCommand = new RelayCommand(ExecuteRemoveExcludedPath);
        ResetSettingsCommand = new RelayCommand(ExecuteResetSettings);
        SelectAccentColorCommand = new RelayCommand(color =>
        {
            if (color is string c) SelectedAccentColor = c;
        });

        LoadSettings();
    }

    public void LoadSettings()
    {
        var s = _settingsService.CurrentSettings;
        _selectedTheme = s.Theme;
        _selectedLanguage = string.IsNullOrWhiteSpace(s.Language) ? "tr" : s.Language;
        _selectedBackdrop = s.BackdropType;
        _windowOpacity = s.WindowOpacity;
        _selectedAccentColor = s.AccentColorHex;
        _scanMode = s.ScanMode;
        _sendToRecycleBin = s.SendToRecycleBin;
        _createQuarantineBackup = s.CreateQuarantineBackup;

        ExcludedPaths.Clear();
        foreach (var p in s.ExcludedPaths)
        {
            ExcludedPaths.Add(p);
        }

        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(SelectedBackdrop));
        OnPropertyChanged(nameof(WindowOpacity));
        OnPropertyChanged(nameof(SelectedAccentColor));
        OnPropertyChanged(nameof(ScanMode));
        OnPropertyChanged(nameof(SendToRecycleBin));
        OnPropertyChanged(nameof(CreateQuarantineBackup));

        UpdateDatabaseSize();
    }

    private void SaveSettings()
    {
        var s = new AppSettings
        {
            Theme = SelectedTheme,
            Language = SelectedLanguage,
            BackdropType = SelectedBackdrop,
            WindowOpacity = WindowOpacity,
            AccentColorHex = SelectedAccentColor,
            ScanMode = ScanMode,
            SendToRecycleBin = SendToRecycleBin,
            CreateQuarantineBackup = CreateQuarantineBackup,
            ExcludedPaths = ExcludedPaths.ToList()
        };

        _ = _settingsService.SaveSettingsAsync(s);
        _mainViewModel.ApplyVisualSettings(s);
    }

    private void ExecuteAddExcludedPath()
    {
        if (!string.IsNullOrWhiteSpace(NewExcludedPath))
        {
            string clean = NewExcludedPath.Trim();
            if (!ExcludedPaths.Contains(clean, StringComparer.OrdinalIgnoreCase))
            {
                ExcludedPaths.Add(clean);
                NewExcludedPath = string.Empty;
                SaveSettings();
                _mainViewModel.ShowToast($"Added '{clean}' to exclusion list", "Success");
            }
        }
    }

    private void ExecuteRemoveExcludedPath(object? parameter)
    {
        if (parameter is string path && ExcludedPaths.Contains(path))
        {
            ExcludedPaths.Remove(path);
            SaveSettings();
            _mainViewModel.ShowToast($"Removed '{path}' from exclusions", "Success");
        }
    }

    private void ExecuteResetSettings()
    {
        var defaultSettings = new AppSettings();
        _ = _settingsService.SaveSettingsAsync(defaultSettings);
        LoadSettings();
        _mainViewModel.ApplyVisualSettings(defaultSettings);
        _mainViewModel.ShowToast("Settings reset to defaults.", "Success");
    }

    private void UpdateDatabaseSize()
    {
        try
        {
            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AppTrace",
                "apptrace.db");

            if (File.Exists(dbPath))
            {
                var len = new FileInfo(dbPath).Length;
                DatabaseSizeFormatted = FormatBytes(len);
            }
            else
            {
                DatabaseSizeFormatted = "< 1 MB (New Database)";
            }
        }
        catch
        {
            DatabaseSizeFormatted = "Local SQLite";
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 MB";
        const double KB = 1024.0;
        const double MB = KB * 1024.0;
        return bytes switch
        {
            >= 1048576 => $"{bytes / MB:0.##} MB",
            >= 1024 => $"{bytes / KB:0.##} KB",
            _ => $"{bytes} B"
        };
    }
}
