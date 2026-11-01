using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using AppTrace.Core.Data;
using AppTrace.Core.Models;
using AppTrace.Core.Services;

namespace AppTrace.UI.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IAppDetectionService _detectionService;
    private readonly IUninstallerService _uninstallerService;
    private readonly ILeftoverScannerService _scannerService;
    private readonly IStartupManagerService _startupService;
    private readonly IStorageAnalysisService _storageService;
    private readonly IQuarantineService _quarantineService;
    private readonly IDatabaseService _databaseService;
    private readonly ISettingsService _settingsService;

    private string _currentView = "Overview";
    private bool _isGlobalLoading = false;
    private string _globalStatusText = "AppTrace Hazır";

    // Toast Notifications
    private string _toastMessage = string.Empty;
    private string _toastType = "Info";
    private bool _isToastVisible = false;

    // Generic Confirmation Modal
    private bool _isConfirmDialogOpen = false;
    private string _confirmDialogTitle = string.Empty;
    private string _confirmDialogMessage = string.Empty;
    private string _confirmDialogActionButtonText = "Onayla";
    private bool _confirmDialogIsDestructive = false;
    private TaskCompletionSource<bool>? _confirmDialogTcs;

    // Uninstall Confirmation Modal
    private bool _isUninstallDialogOpen = false;
    private UninstallPlan? _currentUninstallPlan;
    private InstalledApp? _currentUninstallApp;
    private TaskCompletionSource<bool>? _uninstallDialogTcs;

    // Cleanup Confirmation Modal
    private bool _isCleanupDialogOpen = false;
    private int _cleanupItemsCount;
    private string _cleanupTotalSize = "0 MB";
    private bool _cleanupToRecycleBin = true;
    private bool _cleanupQuarantineBackup = true;
    private TaskCompletionSource<bool>? _cleanupDialogTcs;

    public ILocalizationService Loc { get; }

    public string CurrentView
    {
        get => _currentView;
        set
        {
            if (SetProperty(ref _currentView, value))
            {
                OnPropertyChanged(nameof(CurrentViewModel));
            }
        }
    }

    public object CurrentViewModel => CurrentView switch
    {
        "Overview" => Overview,
        "InstalledApps" => InstalledApps,
        "AppDetails" => AppDetails,
        "LeftoverScanner" => LeftoverScanner,
        "StartupManager" => StartupManager,
        "StorageAnalysis" => StorageAnalysis,
        "Quarantine" => Quarantine,
        "ActivityHistory" => ActivityHistory,
        "Settings" => Settings,
        _ => Overview
    };

    public bool IsGlobalLoading
    {
        get => _isGlobalLoading;
        set => SetProperty(ref _isGlobalLoading, value);
    }

    public string GlobalStatusText
    {
        get => _globalStatusText;
        set => SetProperty(ref _globalStatusText, value);
    }

    public string ToastMessage
    {
        get => _toastMessage;
        set => SetProperty(ref _toastMessage, value);
    }

    public string ToastType
    {
        get => _toastType;
        set => SetProperty(ref _toastType, value);
    }

    public bool IsToastVisible
    {
        get => _isToastVisible;
        set => SetProperty(ref _isToastVisible, value);
    }

    public bool IsConfirmDialogOpen
    {
        get => _isConfirmDialogOpen;
        set => SetProperty(ref _isConfirmDialogOpen, value);
    }

    public string ConfirmDialogTitle
    {
        get => _confirmDialogTitle;
        set => SetProperty(ref _confirmDialogTitle, value);
    }

    public string ConfirmDialogMessage
    {
        get => _confirmDialogMessage;
        set => SetProperty(ref _confirmDialogMessage, value);
    }

    public string ConfirmDialogActionButtonText
    {
        get => _confirmDialogActionButtonText;
        set => SetProperty(ref _confirmDialogActionButtonText, value);
    }

    public bool ConfirmDialogIsDestructive
    {
        get => _confirmDialogIsDestructive;
        set => SetProperty(ref _confirmDialogIsDestructive, value);
    }

    public bool IsUninstallDialogOpen
    {
        get => _isUninstallDialogOpen;
        set => SetProperty(ref _isUninstallDialogOpen, value);
    }

    public UninstallPlan? CurrentUninstallPlan
    {
        get => _currentUninstallPlan;
        set => SetProperty(ref _currentUninstallPlan, value);
    }

    public InstalledApp? CurrentUninstallApp
    {
        get => _currentUninstallApp;
        set => SetProperty(ref _currentUninstallApp, value);
    }

    public bool IsCleanupDialogOpen
    {
        get => _isCleanupDialogOpen;
        set => SetProperty(ref _isCleanupDialogOpen, value);
    }

    public int CleanupItemsCount
    {
        get => _cleanupItemsCount;
        set => SetProperty(ref _cleanupItemsCount, value);
    }

    public string CleanupTotalSize
    {
        get => _cleanupTotalSize;
        set => SetProperty(ref _cleanupTotalSize, value);
    }

    public bool CleanupToRecycleBin
    {
        get => _cleanupToRecycleBin;
        set => SetProperty(ref _cleanupToRecycleBin, value);
    }

    public bool CleanupQuarantineBackup
    {
        get => _cleanupQuarantineBackup;
        set => SetProperty(ref _cleanupQuarantineBackup, value);
    }

    // ViewModels
    public OverviewViewModel Overview { get; }
    public InstalledAppsViewModel InstalledApps { get; }
    public AppDetailsViewModel AppDetails { get; }
    public LeftoverScannerViewModel LeftoverScanner { get; }
    public StartupManagerViewModel StartupManager { get; }
    public StorageAnalysisViewModel StorageAnalysis { get; }
    public QuarantineViewModel Quarantine { get; }
    public ActivityHistoryViewModel ActivityHistory { get; }
    public SettingsViewModel Settings { get; }

    public List<InstalledApp> AllInstalledApps { get; private set; } = new();
    public List<string> RecentlyUninstalledApps { get; } = new();

    // Navigation Commands
    public RelayCommand NavigateCommand { get; }
    public RelayCommand ConfirmDialogApproveCommand { get; }
    public RelayCommand ConfirmDialogCancelCommand { get; }
    public RelayCommand UninstallDialogApproveCommand { get; }
    public RelayCommand UninstallDialogCancelCommand { get; }
    public RelayCommand CleanupDialogApproveCommand { get; }
    public RelayCommand CleanupDialogCancelCommand { get; }
    public RelayCommand DismissToastCommand { get; }

    public event Action<AppSettings>? VisualSettingsChanged;

    public MainViewModel(
        IAppDetectionService detectionService,
        IUninstallerService uninstallerService,
        ILeftoverScannerService scannerService,
        IStartupManagerService startupService,
        IStorageAnalysisService storageService,
        IQuarantineService quarantineService,
        IDatabaseService databaseService,
        ISettingsService settingsService,
        ICleanupService cleanupService,
        ILocalizationService localizationService)
    {
        _detectionService = detectionService;
        _uninstallerService = uninstallerService;
        _scannerService = scannerService;
        _startupService = startupService;
        _storageService = storageService;
        _quarantineService = quarantineService;
        _databaseService = databaseService;
        _settingsService = settingsService;
        Loc = localizationService;

        // Initialize child ViewModels
        Overview = new OverviewViewModel(this);
        InstalledApps = new InstalledAppsViewModel(this);
        AppDetails = new AppDetailsViewModel(this);
        LeftoverScanner = new LeftoverScannerViewModel(this, _scannerService, cleanupService);
        StartupManager = new StartupManagerViewModel(this, _startupService);
        StorageAnalysis = new StorageAnalysisViewModel(this, _storageService);
        Quarantine = new QuarantineViewModel(this, _quarantineService);
        ActivityHistory = new ActivityHistoryViewModel(this, _databaseService);
        Settings = new SettingsViewModel(this, _settingsService, Loc);

        NavigateCommand = new RelayCommand(param =>
        {
            if (param is string viewName) NavigateTo(viewName);
        });

        ConfirmDialogApproveCommand = new RelayCommand(() =>
        {
            IsConfirmDialogOpen = false;
            _confirmDialogTcs?.TrySetResult(true);
        });

        ConfirmDialogCancelCommand = new RelayCommand(() =>
        {
            IsConfirmDialogOpen = false;
            _confirmDialogTcs?.TrySetResult(false);
        });

        UninstallDialogApproveCommand = new RelayCommand(() =>
        {
            IsUninstallDialogOpen = false;
            _uninstallDialogTcs?.TrySetResult(true);
        });

        UninstallDialogCancelCommand = new RelayCommand(() =>
        {
            IsUninstallDialogOpen = false;
            _uninstallDialogTcs?.TrySetResult(false);
        });

        CleanupDialogApproveCommand = new RelayCommand(() =>
        {
            IsCleanupDialogOpen = false;
            _cleanupDialogTcs?.TrySetResult(true);
        });

        CleanupDialogCancelCommand = new RelayCommand(() =>
        {
            IsCleanupDialogOpen = false;
            _cleanupDialogTcs?.TrySetResult(false);
        });

        DismissToastCommand = new RelayCommand(() => IsToastVisible = false);
    }

    public void NavigateTo(string viewName)
    {
        CurrentView = viewName;
        switch (viewName)
        {
            case "Quarantine":
                _ = Quarantine.LoadItemsAsync();
                break;
            case "ActivityHistory":
                _ = ActivityHistory.LoadActivitiesAsync();
                break;
            case "StartupManager":
                _ = StartupManager.LoadEntriesAsync();
                break;
            case "StorageAnalysis":
                StorageAnalysis.UpdateAnalysis(AllInstalledApps);
                break;
        }
    }

    public void ShowAppDetails(InstalledApp app)
    {
        AppDetails.App = app;
        CurrentView = "AppDetails";
    }

    public void ScanLeftoversForApp(InstalledApp app)
    {
        LeftoverScanner.SetTargetApp(app);
        CurrentView = "LeftoverScanner";
        _ = LeftoverScanner.ExecuteScanAsync();
    }

    public async Task RefreshAllDataAsync()
    {
        IsGlobalLoading = true;
        GlobalStatusText = Loc["Overview_Subtitle"];

        try
        {
            await _databaseService.InitializeAsync();
            var settings = await _settingsService.LoadSettingsAsync();
            if (!string.IsNullOrEmpty(settings.Language))
            {
                Loc.SetLanguage(settings.Language);
            }

            // High performance parallel scan
            var appsTask = _detectionService.DetectInstalledApplicationsAsync();
            var startupTask = _startupService.GetStartupEntriesAsync();

            await Task.WhenAll(appsTask, startupTask);

            var apps = appsTask.Result;
            var startupEntries = startupTask.Result;

            AllInstalledApps = apps;
            InstalledApps.SetApps(apps);
            StartupManager.SetEntries(startupEntries);

            Overview.UpdateData(apps, startupEntries, new List<LeftoverCandidate>());
            StorageAnalysis.UpdateAnalysis(apps);

            GlobalStatusText = Loc.CurrentLanguage == "tr"
                ? $"{apps.Count} uygulama ve {startupEntries.Count} başlangıç programı yüklendi"
                : $"Loaded {apps.Count} applications and {startupEntries.Count} startup programs";

            // Run orphan scan in background without blocking UI
            _ = Task.Run(async () =>
            {
                try
                {
                    var leftovers = await _scannerService.ScanAllOrphanLeftoversAsync(apps, RecentlyUninstalledApps);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Overview.UpdateData(apps, startupEntries, leftovers);
                        StorageAnalysis.UpdateAnalysis(apps, leftovers);
                    });
                }
                catch { }
            });

            await _databaseService.LogActivityAsync(new ActivityLogEntry
            {
                ActionType = "Scan",
                ApplicationName = "System",
                Description = $"Discovered {apps.Count} applications and {startupEntries.Count} startup programs",
                Success = true
            });
        }
        catch (Exception ex)
        {
            ShowToast($"Failed to refresh system inventory: {ex.Message}", "Error");
            GlobalStatusText = "Error scanning applications";
        }
        finally
        {
            IsGlobalLoading = false;
        }
    }

    public async Task RefreshAppsAsync()
    {
        IsGlobalLoading = true;
        GlobalStatusText = Loc.CurrentLanguage == "tr" ? "Uygulamalar yenileniyor..." : "Refreshing installed applications...";
        try
        {
            var apps = await _detectionService.DetectInstalledApplicationsAsync();
            AllInstalledApps = apps;
            InstalledApps.SetApps(apps);
            StorageAnalysis.UpdateAnalysis(apps);
            GlobalStatusText = Loc.CurrentLanguage == "tr" ? $"{apps.Count} uygulama yüklendi" : $"Loaded {apps.Count} applications";
        }
        catch (Exception ex)
        {
            ShowToast($"Error refreshing apps: {ex.Message}", "Error");
        }
        finally
        {
            IsGlobalLoading = false;
        }
    }

    public async Task PromptUninstallAsync(InstalledApp app)
    {
        CurrentUninstallApp = app;
        CurrentUninstallPlan = await _uninstallerService.PrepareUninstallAsync(app);

        if (!CurrentUninstallPlan.CanExecute)
        {
            ShowToast(CurrentUninstallPlan.SafetyWarning ?? "Cannot uninstall this application.", "Warning");
            return;
        }

        _uninstallDialogTcs = new TaskCompletionSource<bool>();
        IsUninstallDialogOpen = true;

        bool approved = await _uninstallDialogTcs.Task;
        if (!approved) return;

        IsGlobalLoading = true;
        GlobalStatusText = $"{app.DisplayName} kaldırılıyor...";

        await _databaseService.LogActivityAsync(new ActivityLogEntry
        {
            ActionType = "Uninstall",
            ApplicationName = app.DisplayName,
            Description = $"User started uninstaller: {CurrentUninstallPlan.UninstallType}",
            Details = $"Command: {CurrentUninstallPlan.ExecutablePath} {CurrentUninstallPlan.Arguments}",
            Success = true
        });

        var result = await _uninstallerService.ExecuteUninstallAsync(CurrentUninstallPlan);
        IsGlobalLoading = false;

        if (result.Success)
        {
            ShowToast($"'{app.DisplayName}' başarıyla kaldırıldı", "Success");
            RecentlyUninstalledApps.Add(app.DisplayName);

            await _databaseService.LogActivityAsync(new ActivityLogEntry
            {
                ActionType = "Uninstall",
                ApplicationName = app.DisplayName,
                Description = $"Uninstallation completed ({result.Duration.TotalSeconds:0.#}s)",
                Details = result.Message,
                Success = true
            });

            // Offer leftover scan
            string promptTitle = Loc.CurrentLanguage == "tr" ? "Artık Dosyaları Tara?" : "Scan for Leftover Files?";
            string promptMsg = Loc.CurrentLanguage == "tr"
                ? $"'{app.DisplayName}' kaldırıldı.\n\nAppTrace'in kalan artık dosyaları, önbellek klasörlerini ve kayıt defteri girdilerini aramasını ister misiniz?"
                : $"'{app.DisplayName}' was uninstalled.\n\nWould you like AppTrace to scan for remaining residual files, cache folders, and registry entries?";
            string btnText = Loc.CurrentLanguage == "tr" ? "Artıkları Tara" : "Scan Leftovers";

            bool scanPrompt = await ShowConfirmAsync(promptTitle, promptMsg, btnText);

            await RefreshAppsAsync();

            if (scanPrompt)
            {
                ScanLeftoversForApp(app);
            }
        }
        else
        {
            ShowToast(result.Message, "Error");
            await _databaseService.LogActivityAsync(new ActivityLogEntry
            {
                ActionType = "Error",
                ApplicationName = app.DisplayName,
                Description = $"Uninstall failed or cancelled: {result.Message}",
                Success = false
            });
        }
    }

    public Task<bool> ConfirmCleanupAsync(int count, string totalSize, bool toRecycleBin, bool quarantineBackup)
    {
        CleanupItemsCount = count;
        CleanupTotalSize = totalSize;
        CleanupToRecycleBin = toRecycleBin;
        CleanupQuarantineBackup = quarantineBackup;

        _cleanupDialogTcs = new TaskCompletionSource<bool>();
        IsCleanupDialogOpen = true;
        return _cleanupDialogTcs.Task;
    }

    public Task<bool> ShowConfirmAsync(string title, string message, string actionButtonText = "Confirm", bool isDestructive = false)
    {
        ConfirmDialogTitle = title;
        ConfirmDialogMessage = message;
        ConfirmDialogActionButtonText = actionButtonText;
        ConfirmDialogIsDestructive = isDestructive;

        _confirmDialogTcs = new TaskCompletionSource<bool>();
        IsConfirmDialogOpen = true;
        return _confirmDialogTcs.Task;
    }

    public void ShowToast(string message, string type = "Info")
    {
        ToastMessage = message;
        ToastType = type;
        IsToastVisible = true;

        _ = Task.Delay(4000).ContinueWith(_ =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (ToastMessage == message)
                {
                    IsToastVisible = false;
                }
            });
        });
    }

    public void ApplyVisualSettings(AppSettings settings)
    {
        VisualSettingsChanged?.Invoke(settings);
    }
}
