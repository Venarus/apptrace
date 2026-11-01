using System.Collections.ObjectModel;
using AppTrace.Core.Models;
using AppTrace.Core.Services;

namespace AppTrace.UI.ViewModels;

public class StorageAnalysisViewModel : ViewModelBase
{
    private readonly MainViewModel _mainViewModel;
    public ILocalizationService Loc => _mainViewModel.Loc;
    private readonly IStorageAnalysisService _analysisService;

    private string _driveUsedFormatted = "0 GB";
    private string _driveFreeFormatted = "0 GB";
    private string _driveCapacityFormatted = "0 GB";
    private double _driveUsedPercentage = 0.0;
    private string _totalAppsStorageFormatted = "0 GB";
    private string _removableSpaceFormatted = "0 MB";

    public string DriveUsedFormatted
    {
        get => _driveUsedFormatted;
        set => SetProperty(ref _driveUsedFormatted, value);
    }

    public string DriveFreeFormatted
    {
        get => _driveFreeFormatted;
        set => SetProperty(ref _driveFreeFormatted, value);
    }

    public string DriveCapacityFormatted
    {
        get => _driveCapacityFormatted;
        set => SetProperty(ref _driveCapacityFormatted, value);
    }

    public double DriveUsedPercentage
    {
        get => _driveUsedPercentage;
        set => SetProperty(ref _driveUsedPercentage, value);
    }

    public string TotalAppsStorageFormatted
    {
        get => _totalAppsStorageFormatted;
        set => SetProperty(ref _totalAppsStorageFormatted, value);
    }

    public string RemovableSpaceFormatted
    {
        get => _removableSpaceFormatted;
        set => SetProperty(ref _removableSpaceFormatted, value);
    }

    public ObservableCollection<StorageCategoryBreakdown> Categories { get; } = new();
    public ObservableCollection<SizeRangeBreakdown> SizeRanges { get; } = new();
    public ObservableCollection<InstalledApp> LargestApps { get; } = new();

    public RelayCommand NavigateToScannerCommand { get; }
    public RelayCommand SelectAppCommand { get; }

    public StorageAnalysisViewModel(
        MainViewModel mainViewModel,
        IStorageAnalysisService analysisService)
    {
        _mainViewModel = mainViewModel;
        _analysisService = analysisService;

        NavigateToScannerCommand = new RelayCommand(() => _mainViewModel.NavigateTo("LeftoverScanner"));
        SelectAppCommand = new RelayCommand(ExecuteSelectApp);
    }

    public void UpdateAnalysis(List<InstalledApp> apps, List<LeftoverCandidate>? leftovers = null)
    {
        var result = _analysisService.AnalyzeStorage(apps, leftovers);

        DriveUsedFormatted = FormatBytes(result.TotalDriveUsedBytes);
        DriveFreeFormatted = FormatBytes(result.TotalDriveFreeBytes);
        DriveCapacityFormatted = FormatBytes(result.TotalDriveCapacityBytes);
        DriveUsedPercentage = result.TotalDriveCapacityBytes > 0
            ? (double)result.TotalDriveUsedBytes / result.TotalDriveCapacityBytes * 100.0
            : 0;

        TotalAppsStorageFormatted = FormatBytes(result.TotalAppStorageBytes);
        RemovableSpaceFormatted = FormatBytes(result.RemovableCandidateBytes);

        Categories.Clear();
        foreach (var cat in result.Categories) Categories.Add(cat);

        SizeRanges.Clear();
        foreach (var sr in result.SizeRanges) SizeRanges.Add(sr);

        LargestApps.Clear();
        foreach (var app in result.LargestApps) LargestApps.Add(app);
    }

    private void ExecuteSelectApp(object? parameter)
    {
        if (parameter is InstalledApp app)
        {
            _mainViewModel.ShowAppDetails(app);
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
