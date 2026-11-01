using System.Collections.ObjectModel;
using System.Diagnostics;
using AppTrace.Core.Models;

namespace AppTrace.UI.ViewModels;

public class AppDetailsViewModel : ViewModelBase
{
    private readonly MainViewModel _mainViewModel;
    public ILocalizationService Loc => _mainViewModel.Loc;
    private InstalledApp _app = new();
    private bool _hasInstallFolder;
    private bool _hasWebsite;
    private bool _hasUninstallCommand;
    private bool _isScanningLeftovers;
    private string _leftoverSummary = string.Empty;

    public InstalledApp App
    {
        get => _app;
        set
        {
            if (SetProperty(ref _app, value))
            {
                HasInstallFolder = !string.IsNullOrWhiteSpace(_app.InstallLocation) && Directory.Exists(_app.InstallLocation);
                HasWebsite = !string.IsNullOrWhiteSpace(_app.HelpLink) || !string.IsNullOrWhiteSpace(_app.UrlInfoAbout);
                HasUninstallCommand = _app.HasUninstallCommand;

                RelatedFolders.Clear();
                foreach (var folder in _app.RelatedDirectories)
                {
                    RelatedFolders.Add(folder);
                }
            }
        }
    }

    public bool HasInstallFolder
    {
        get => _hasInstallFolder;
        set => SetProperty(ref _hasInstallFolder, value);
    }

    public bool HasWebsite
    {
        get => _hasWebsite;
        set => SetProperty(ref _hasWebsite, value);
    }

    public bool HasUninstallCommand
    {
        get => _hasUninstallCommand;
        set => SetProperty(ref _hasUninstallCommand, value);
    }

    public bool IsScanningLeftovers
    {
        get => _isScanningLeftovers;
        set => SetProperty(ref _isScanningLeftovers, value);
    }

    public string LeftoverSummary
    {
        get => _leftoverSummary;
        set => SetProperty(ref _leftoverSummary, value);
    }

    public ObservableCollection<string> RelatedFolders { get; } = new();

    public RelayCommand BackCommand { get; }
    public RelayCommand OpenInstallFolderCommand { get; }
    public RelayCommand LaunchAppCommand { get; }
    public RelayCommand OpenWebsiteCommand { get; }
    public AsyncRelayCommand StartUninstallCommand { get; }
    public RelayCommand ScanLeftoversCommand { get; }
    public RelayCommand OpenFolderByPathCommand { get; }

    public AppDetailsViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;

        BackCommand = new RelayCommand(() => _mainViewModel.NavigateTo("InstalledApps"));
        OpenInstallFolderCommand = new RelayCommand(ExecuteOpenInstallFolder);
        LaunchAppCommand = new RelayCommand(ExecuteLaunchApp);
        OpenWebsiteCommand = new RelayCommand(ExecuteOpenWebsite);
        StartUninstallCommand = new AsyncRelayCommand(async () => await _mainViewModel.PromptUninstallAsync(App));
        ScanLeftoversCommand = new RelayCommand(() => _mainViewModel.ScanLeftoversForApp(App));
        OpenFolderByPathCommand = new RelayCommand(ExecuteOpenSpecificFolder);
    }

    private void ExecuteOpenInstallFolder()
    {
        if (HasInstallFolder && !string.IsNullOrWhiteSpace(App.InstallLocation))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{App.InstallLocation}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _mainViewModel.ShowToast($"Error opening folder: {ex.Message}", "Error");
            }
        }
    }

    private void ExecuteOpenSpecificFolder(object? parameter)
    {
        if (parameter is string folder && Directory.Exists(folder))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{folder}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _mainViewModel.ShowToast($"Error opening folder: {ex.Message}", "Error");
            }
        }
    }

    private void ExecuteLaunchApp()
    {
        try
        {
            if (HasInstallFolder && !string.IsNullOrWhiteSpace(App.InstallLocation))
            {
                var exe = Directory.EnumerateFiles(App.InstallLocation, "*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (exe != null)
                {
                    Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
                    _mainViewModel.ShowToast($"Launched {App.DisplayName}", "Success");
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(App.IconPath) && File.Exists(App.IconPath) && App.IconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo { FileName = App.IconPath, UseShellExecute = true });
                _mainViewModel.ShowToast($"Launched {App.DisplayName}", "Success");
                return;
            }

            _mainViewModel.ShowToast("No launchable executable found in application directory.", "Warning");
        }
        catch (Exception ex)
        {
            _mainViewModel.ShowToast($"Failed to launch application: {ex.Message}", "Error");
        }
    }

    private void ExecuteOpenWebsite()
    {
        string? url = !string.IsNullOrWhiteSpace(App.UrlInfoAbout) ? App.UrlInfoAbout : App.HelpLink;
        if (!string.IsNullOrWhiteSpace(url))
        {
            try
            {
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _mainViewModel.ShowToast($"Failed to open website: {ex.Message}", "Error");
            }
        }
    }
}
