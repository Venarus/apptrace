using System.Collections.ObjectModel;
using System.Diagnostics;
using AppTrace.Core.Models;
using AppTrace.Core.Services;

namespace AppTrace.UI.ViewModels;

public class LeftoverScannerViewModel : ViewModelBase
{
    private readonly MainViewModel _mainViewModel;
    public ILocalizationService Loc => _mainViewModel.Loc;
    private readonly ILeftoverScannerService _scannerService;
    private readonly ICleanupService _cleanupService;

    private InstalledApp? _targetApp;
    private bool _isScanning;
    private string _progressText = "Ready to scan for leftover files and remnants";
    private string _confidenceFilter = "All";
    private string _totalRemovableSpace = "0 MB";
    private int _highConfidenceCount;
    private int _selectedCount;
    private string _selectedSizeFormatted = "0 MB";
    private bool _sendToRecycleBin = true;
    private bool _createQuarantineBackup = true;
    private CancellationTokenSource? _scanCts;

    private readonly List<LeftoverCandidate> _allCandidates = new();
    public ObservableCollection<LeftoverCandidate> FilteredCandidates { get; } = new();

    public InstalledApp? TargetApp
    {
        get => _targetApp;
        set => SetProperty(ref _targetApp, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    public string ConfidenceFilter
    {
        get => _confidenceFilter;
        set
        {
            if (SetProperty(ref _confidenceFilter, value))
                ApplyFilter();
        }
    }

    public string TotalRemovableSpace
    {
        get => _totalRemovableSpace;
        set => SetProperty(ref _totalRemovableSpace, value);
    }

    public int HighConfidenceCount
    {
        get => _highConfidenceCount;
        set => SetProperty(ref _highConfidenceCount, value);
    }

    public int SelectedCount
    {
        get => _selectedCount;
        set => SetProperty(ref _selectedCount, value);
    }

    public string SelectedSizeFormatted
    {
        get => _selectedSizeFormatted;
        set => SetProperty(ref _selectedSizeFormatted, value);
    }

    public bool SendToRecycleBin
    {
        get => _sendToRecycleBin;
        set => SetProperty(ref _sendToRecycleBin, value);
    }

    public bool CreateQuarantineBackup
    {
        get => _createQuarantineBackup;
        set => SetProperty(ref _createQuarantineBackup, value);
    }

    public AsyncRelayCommand StartScanCommand { get; }
    public RelayCommand CancelScanCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectHighConfidenceCommand { get; }
    public RelayCommand DeselectAllCommand { get; }
    public RelayCommand ToggleItemSelectionCommand { get; }
    public RelayCommand OpenItemFolderCommand { get; }
    public AsyncRelayCommand StartCleanupWorkflowCommand { get; }
    public RelayCommand ClearTargetAppCommand { get; }

    public LeftoverScannerViewModel(
        MainViewModel mainViewModel,
        ILeftoverScannerService scannerService,
        ICleanupService cleanupService)
    {
        _mainViewModel = mainViewModel;
        _scannerService = scannerService;
        _cleanupService = cleanupService;

        StartScanCommand = new AsyncRelayCommand(ExecuteScanAsync);
        CancelScanCommand = new RelayCommand(ExecuteCancelScan);
        SelectAllCommand = new RelayCommand(ExecuteSelectAll);
        SelectHighConfidenceCommand = new RelayCommand(ExecuteSelectHighConfidence);
        DeselectAllCommand = new RelayCommand(ExecuteDeselectAll);
        ToggleItemSelectionCommand = new RelayCommand(ExecuteToggleSelection);
        OpenItemFolderCommand = new RelayCommand(ExecuteOpenItemFolder);
        StartCleanupWorkflowCommand = new AsyncRelayCommand(ExecuteCleanupWorkflowAsync);
        ClearTargetAppCommand = new RelayCommand(() => TargetApp = null);
    }

    public void SetTargetApp(InstalledApp app)
    {
        TargetApp = app;
        ProgressText = $"Target application selected: {app.DisplayName}";
    }

    public async Task ExecuteScanAsync()
    {
        if (IsScanning) return;

        IsScanning = true;
        _scanCts = new CancellationTokenSource();
        _allCandidates.Clear();
        FilteredCandidates.Clear();

        try
        {
            if (TargetApp != null)
            {
                ProgressText = $"Scanning residual directories for '{TargetApp.DisplayName}'...";
                var results = await _scannerService.ScanForAppLeftoversAsync(TargetApp, _scanCts.Token);
                _allCandidates.AddRange(results);
            }
            else
            {
                ProgressText = "Scanning standard locations for orphaned leftover files...";
                var currentApps = _mainViewModel.AllInstalledApps;
                var results = await _scannerService.ScanAllOrphanLeftoversAsync(
                    currentApps,
                    _mainViewModel.RecentlyUninstalledApps,
                    _scanCts.Token);
                _allCandidates.AddRange(results);
            }

            ProgressText = $"Scan complete. Detected {_allCandidates.Count} leftover candidates.";
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Scan was cancelled by user.";
        }
        catch (Exception ex)
        {
            ProgressText = $"Error during scan: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            ApplyFilter();
            UpdateStats();
        }
    }

    private void ExecuteCancelScan()
    {
        if (_scanCts != null && !_scanCts.IsCancellationRequested)
        {
            _scanCts.Cancel();
        }
    }

    private void ApplyFilter()
    {
        var query = _allCandidates.AsEnumerable();

        if (_confidenceFilter == "High")
            query = query.Where(c => c.Confidence == ConfidenceLevel.High);
        else if (_confidenceFilter == "Possible")
            query = query.Where(c => c.Confidence == ConfidenceLevel.Possible);
        else if (_confidenceFilter == "Uncertain")
            query = query.Where(c => c.Confidence == ConfidenceLevel.Uncertain);

        FilteredCandidates.Clear();
        foreach (var c in query)
        {
            FilteredCandidates.Add(c);
        }

        UpdateStats();
    }

    public void UpdateStats()
    {
        long totalBytes = _allCandidates.Sum(c => c.EstimatedSizeBytes);
        TotalRemovableSpace = FormatBytes(totalBytes);
        HighConfidenceCount = _allCandidates.Count(c => c.Confidence == ConfidenceLevel.High);

        var selected = _allCandidates.Where(c => c.IsSelected).ToList();
        SelectedCount = selected.Count;
        SelectedSizeFormatted = FormatBytes(selected.Sum(c => c.EstimatedSizeBytes));
    }

    private void ExecuteSelectAll()
    {
        foreach (var c in _allCandidates) c.IsSelected = true;
        // Trigger UI refresh
        ApplyFilter();
    }

    private void ExecuteSelectHighConfidence()
    {
        foreach (var c in _allCandidates)
        {
            c.IsSelected = c.Confidence == ConfidenceLevel.High;
        }
        ApplyFilter();
    }

    private void ExecuteDeselectAll()
    {
        foreach (var c in _allCandidates) c.IsSelected = false;
        ApplyFilter();
    }

    private void ExecuteToggleSelection(object? parameter)
    {
        if (parameter is LeftoverCandidate candidate)
        {
            candidate.IsSelected = !candidate.IsSelected;
            UpdateStats();
        }
    }

    private void ExecuteOpenItemFolder(object? parameter)
    {
        if (parameter is LeftoverCandidate candidate)
        {
            string path = candidate.Path;
            if (candidate.ItemType == CleanupItemType.RegistryKey)
            {
                _mainViewModel.ShowToast($"Registry key: {path}", "Info");
                return;
            }

            if (File.Exists(path))
            {
                path = Path.GetDirectoryName(path) ?? path;
            }

            if (Directory.Exists(path))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    _mainViewModel.ShowToast($"Failed to open directory: {ex.Message}", "Error");
                }
            }
        }
    }

    private async Task ExecuteCleanupWorkflowAsync()
    {
        var selected = _allCandidates.Where(c => c.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _mainViewModel.ShowToast("No items selected for cleanup. Please select at least one item.", "Warning");
            return;
        }

        // Show confirmation dialog via MainViewModel
        bool confirmed = await _mainViewModel.ConfirmCleanupAsync(
            selected.Count,
            SelectedSizeFormatted,
            SendToRecycleBin,
            CreateQuarantineBackup);

        if (!confirmed) return;

        IsScanning = true;
        ProgressText = $"Cleaning {selected.Count} selected items...";

        try
        {
            var report = await _cleanupService.CleanSelectedItemsAsync(
                selected,
                CreateQuarantineBackup,
                SendToRecycleBin);

            // Remove successfully cleaned items from list
            foreach (var path in report.RemovedPaths)
            {
                _allCandidates.RemoveAll(c => c.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            }

            ApplyFilter();

            _mainViewModel.ShowToast(
                $"Cleanup finished: {report.SucceededCount} items cleaned, {report.FormattedFreedSize} freed.",
                report.FailedCount == 0 ? "Success" : "Warning");

            ProgressText = $"Cleanup completed. Freed {report.FormattedFreedSize} of disk space.";
        }
        catch (Exception ex)
        {
            _mainViewModel.ShowToast($"Cleanup encountered an error: {ex.Message}", "Error");
            ProgressText = "Cleanup stopped due to error.";
        }
        finally
        {
            IsScanning = false;
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
