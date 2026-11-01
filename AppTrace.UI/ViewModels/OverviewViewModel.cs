using System.Collections.ObjectModel;
using AppTrace.Core.Models;

namespace AppTrace.UI.ViewModels;

public class OverviewViewModel : ViewModelBase
{
    private readonly MainViewModel _mainViewModel;
    public ILocalizationService Loc => _mainViewModel.Loc;
    private int _totalInstalledApps;
    private string _totalDetectedStorage = "0 MB";
    private int _totalStartupApps;
    private int _pendingCleanupCount;
    private string _potentialCleanupSize = "0 MB";
    private int _highConfidenceRemnantsCount;
    private double _storageUsedPercent = 50.0;
    private string _driveInfoText = "Calculating drive space...";

    public int TotalInstalledApps
    {
        get => _totalInstalledApps;
        set => SetProperty(ref _totalInstalledApps, value);
    }

    public string TotalDetectedStorage
    {
        get => _totalDetectedStorage;
        set => SetProperty(ref _totalDetectedStorage, value);
    }

    public int TotalStartupApps
    {
        get => _totalStartupApps;
        set => SetProperty(ref _totalStartupApps, value);
    }

    public int PendingCleanupCount
    {
        get => _pendingCleanupCount;
        set => SetProperty(ref _pendingCleanupCount, value);
    }

    public string PotentialCleanupSize
    {
        get => _potentialCleanupSize;
        set => SetProperty(ref _potentialCleanupSize, value);
    }

    public int HighConfidenceRemnantsCount
    {
        get => _highConfidenceRemnantsCount;
        set => SetProperty(ref _highConfidenceRemnantsCount, value);
    }

    public double StorageUsedPercent
    {
        get => _storageUsedPercent;
        set => SetProperty(ref _storageUsedPercent, value);
    }

    public string DriveInfoText
    {
        get => _driveInfoText;
        set => SetProperty(ref _driveInfoText, value);
    }

    public ObservableCollection<InstalledApp> RecentApps { get; } = new();

    public RelayCommand NavigateToAppsCommand { get; }
    public RelayCommand NavigateToScannerCommand { get; }
    public RelayCommand NavigateToStartupCommand { get; }
    public RelayCommand NavigateToStorageCommand { get; }
    public AsyncRelayCommand RefreshDashboardCommand { get; }

    public OverviewViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;

        NavigateToAppsCommand = new RelayCommand(() => _mainViewModel.NavigateTo("InstalledApps"));
        NavigateToScannerCommand = new RelayCommand(() => _mainViewModel.NavigateTo("LeftoverScanner"));
        NavigateToStartupCommand = new RelayCommand(() => _mainViewModel.NavigateTo("StartupManager"));
        NavigateToStorageCommand = new RelayCommand(() => _mainViewModel.NavigateTo("StorageAnalysis"));
        RefreshDashboardCommand = new AsyncRelayCommand(async () => await _mainViewModel.RefreshAllDataAsync());
    }

    public void UpdateData(
        List<InstalledApp> apps,
        List<StartupEntry> startupEntries,
        List<LeftoverCandidate> leftovers)
    {
        TotalInstalledApps = apps.Count;
        long totalStorage = apps.Sum(a => a.EstimatedSizeBytes);
        TotalDetectedStorage = FormatBytes(totalStorage);

        TotalStartupApps = startupEntries.Count;
        PendingCleanupCount = leftovers.Count;
        HighConfidenceRemnantsCount = leftovers.Count(l => l.Confidence == ConfidenceLevel.High);
        long leftoverSize = leftovers.Sum(l => l.EstimatedSizeBytes);
        PotentialCleanupSize = FormatBytes(leftoverSize);

        // Update Recent Apps
        RecentApps.Clear();
        var recents = apps
            .OrderByDescending(a => a.InstallDate ?? DateTime.MinValue)
            .Take(5);
        foreach (var app in recents)
        {
            RecentApps.Add(app);
        }

        // Drive space
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
            if (drive.IsReady)
            {
                long used = drive.TotalSize - drive.TotalFreeSpace;
                StorageUsedPercent = (double)used / drive.TotalSize * 100.0;
                DriveInfoText = $"{FormatBytes(used)} used of {FormatBytes(drive.TotalSize)} ({StorageUsedPercent:0.0}%)";
            }
        }
        catch
        {
            DriveInfoText = "System Drive C:\\";
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 MB";
        const double KB = 1024.0;
        const double MB = KB * 1024.0;
        const double GB = MB * 1024.0;
        return bytes switch
        {
            >= 1073741824 => $"{bytes / GB:0.##} GB",
            >= 1048576 => $"{bytes / MB:0.##} MB",
            >= 1024 => $"{bytes / KB:0.##} KB",
            _ => $"{bytes} B"
        };
    }
}
