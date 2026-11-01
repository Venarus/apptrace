using System.Collections.ObjectModel;
using System.Diagnostics;
using AppTrace.Core.Models;

namespace AppTrace.UI.ViewModels;

public class InstalledAppsViewModel : ViewModelBase
{
    private readonly MainViewModel _mainViewModel;
    public ILocalizationService Loc => _mainViewModel.Loc;
    private List<InstalledApp> _allApps = new();
    private string _searchQuery = string.Empty;
    private string _selectedFilter = "All";
    private string _selectedCategory = "All";
    private string _sortBy = "Name";
    private bool _isGridView = false;
    private InstalledApp? _selectedApp;
    private bool _isLoading;
    private string _statusText = "Ready";

    public ObservableCollection<InstalledApp> FilteredApps { get; } = new();

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
                ApplyFilters();
        }
    }

    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (SetProperty(ref _selectedFilter, value))
                ApplyFilters();
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
                ApplyFilters();
        }
    }

    public string SortBy
    {
        get => _sortBy;
        set
        {
            if (SetProperty(ref _sortBy, value))
                ApplyFilters();
        }
    }

    public bool IsGridView
    {
        get => _isGridView;
        set => SetProperty(ref _isGridView, value);
    }

    public InstalledApp? SelectedApp
    {
        get => _selectedApp;
        set => SetProperty(ref _selectedApp, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public RelayCommand ToggleViewModeCommand { get; }
    public RelayCommand ViewDetailsCommand { get; }
    public RelayCommand LaunchAppCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public AsyncRelayCommand StartUninstallCommand { get; }
    public RelayCommand ScanLeftoversCommand { get; }
    public AsyncRelayCommand RefreshAppsCommand { get; }

    public InstalledAppsViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;

        ToggleViewModeCommand = new RelayCommand(() => IsGridView = !IsGridView);
        ViewDetailsCommand = new RelayCommand(ExecuteViewDetails);
        LaunchAppCommand = new RelayCommand(ExecuteLaunchApp);
        OpenFolderCommand = new RelayCommand(ExecuteOpenFolder);
        StartUninstallCommand = new AsyncRelayCommand(ExecuteStartUninstallAsync);
        ScanLeftoversCommand = new RelayCommand(ExecuteScanLeftovers);
        RefreshAppsCommand = new AsyncRelayCommand(async () => await _mainViewModel.RefreshAppsAsync());
    }

    public void SetApps(List<InstalledApp> apps)
    {
        _allApps = apps ?? new List<InstalledApp>();
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var query = _allApps.AsEnumerable();

        // 1. Text Search
        if (!string.IsNullOrWhiteSpace(_searchQuery))
        {
            string q = _searchQuery.Trim().ToLowerInvariant();
            query = query.Where(a =>
                a.DisplayName.ToLowerInvariant().Contains(q) ||
                a.Publisher.ToLowerInvariant().Contains(q) ||
                (a.InstallLocation != null && a.InstallLocation.ToLowerInvariant().Contains(q)));
        }

        // 2. Filter Tab
        query = _selectedFilter switch
        {
            "Desktop" => query.Where(a => a.InstallSource != InstallSource.MicrosoftStore),
            "Store" => query.Where(a => a.InstallSource == InstallSource.MicrosoftStore),
            "Large (>1GB)" => query.Where(a => a.EstimatedSizeBytes >= 1073741824L),
            "Recent" => query.Where(a => a.InstallDate.HasValue && a.InstallDate.Value >= DateTime.Now.AddDays(-60)),
            _ => query
        };

        // 3. Category Filter
        if (!string.IsNullOrWhiteSpace(_selectedCategory) && 
            !_selectedCategory.Equals("All", StringComparison.OrdinalIgnoreCase) && 
            !_selectedCategory.Equals("All Categories", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(a => a.Category.ToString().Equals(_selectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        // 4. Sorting
        query = _sortBy switch
        {
            "SizeDesc" or "Size (Largest)" => query.OrderByDescending(a => a.EstimatedSizeBytes),
            "SizeAsc" or "Size (Smallest)" => query.OrderBy(a => a.EstimatedSizeBytes > 0 ? a.EstimatedSizeBytes : long.MaxValue),
            "DateDesc" or "Install Date (Newest)" => query.OrderByDescending(a => a.InstallDate ?? DateTime.MinValue),
            "Publisher" => query.OrderBy(a => a.Publisher).ThenBy(a => a.DisplayName),
            _ => query.OrderBy(a => a.DisplayName)
        };

        FilteredApps.Clear();
        foreach (var app in query)
        {
            FilteredApps.Add(app);
        }

        StatusText = _mainViewModel.Loc.CurrentLanguage == "tr"
            ? $"{_allApps.Count} uygulamadan {FilteredApps.Count} tanesi listeleniyor"
            : $"Showing {FilteredApps.Count} of {_allApps.Count} applications";
    }

    private void ExecuteViewDetails(object? parameter)
    {
        var app = parameter as InstalledApp ?? SelectedApp;
        if (app != null)
        {
            _mainViewModel.ShowAppDetails(app);
        }
    }

    private void ExecuteLaunchApp(object? parameter)
    {
        var app = parameter as InstalledApp ?? SelectedApp;
        if (app == null) return;

        try
        {
            if (!string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation))
            {
                var exe = Directory.EnumerateFiles(app.InstallLocation, "*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (exe != null)
                {
                    Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
                    _mainViewModel.ShowToast($"Launched {app.DisplayName}", "Success");
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(app.IconPath) && File.Exists(app.IconPath) && app.IconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo { FileName = app.IconPath, UseShellExecute = true });
                _mainViewModel.ShowToast($"Launched {app.DisplayName}", "Success");
                return;
            }

            _mainViewModel.ShowToast($"Cannot find primary executable for {app.DisplayName}", "Warning");
        }
        catch (Exception ex)
        {
            _mainViewModel.ShowToast($"Failed to launch application: {ex.Message}", "Error");
        }
    }

    private void ExecuteOpenFolder(object? parameter)
    {
        var app = parameter as InstalledApp ?? SelectedApp;
        if (app == null) return;

        string? folder = app.InstallLocation;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            if (app.RelatedDirectories.Count > 0 && Directory.Exists(app.RelatedDirectories[0]))
            {
                folder = app.RelatedDirectories[0];
            }
        }

        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
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
                _mainViewModel.ShowToast($"Failed to open folder: {ex.Message}", "Error");
            }
        }
        else
        {
            _mainViewModel.ShowToast("Installation folder does not exist or cannot be located.", "Warning");
        }
    }

    private async Task ExecuteStartUninstallAsync(object? parameter)
    {
        var app = parameter as InstalledApp ?? SelectedApp;
        if (app != null)
        {
            await _mainViewModel.PromptUninstallAsync(app);
        }
    }

    private void ExecuteScanLeftovers(object? parameter)
    {
        var app = parameter as InstalledApp ?? SelectedApp;
        if (app != null)
        {
            _mainViewModel.ScanLeftoversForApp(app);
        }
    }
}
