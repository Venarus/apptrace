using System.Collections.ObjectModel;
using System.Diagnostics;
using AppTrace.Core.Models;
using AppTrace.Core.Services;

namespace AppTrace.UI.ViewModels;

public class StartupManagerViewModel : ViewModelBase
{
    private readonly MainViewModel _mainViewModel;
    public ILocalizationService Loc => _mainViewModel.Loc;
    private readonly IStartupManagerService _startupService;

    private List<StartupEntry> _allEntries = new();
    private string _searchQuery = string.Empty;
    private int _totalEntriesCount;
    private int _enabledCount;
    private int _disabledCount;
    private bool _isLoading;

    public ObservableCollection<StartupEntry> FilteredEntries { get; } = new();

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
                ApplyFilter();
        }
    }

    public int TotalEntriesCount
    {
        get => _totalEntriesCount;
        set => SetProperty(ref _totalEntriesCount, value);
    }

    public int EnabledCount
    {
        get => _enabledCount;
        set => SetProperty(ref _enabledCount, value);
    }

    public int DisabledCount
    {
        get => _disabledCount;
        set => SetProperty(ref _disabledCount, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public AsyncRelayCommand RefreshEntriesCommand { get; }
    public AsyncRelayCommand ToggleEntryCommand { get; }
    public RelayCommand OpenExecutableLocationCommand { get; }

    public StartupManagerViewModel(
        MainViewModel mainViewModel,
        IStartupManagerService startupService)
    {
        _mainViewModel = mainViewModel;
        _startupService = startupService;

        RefreshEntriesCommand = new AsyncRelayCommand(LoadEntriesAsync);
        ToggleEntryCommand = new AsyncRelayCommand(ExecuteToggleEntryAsync);
        OpenExecutableLocationCommand = new RelayCommand(ExecuteOpenLocation);
    }

    public async Task LoadEntriesAsync()
    {
        IsLoading = true;
        try
        {
            var entries = await _startupService.GetStartupEntriesAsync();
            _allEntries = entries;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _mainViewModel.ShowToast($"Failed to load startup entries: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void SetEntries(List<StartupEntry> entries)
    {
        _allEntries = entries ?? new List<StartupEntry>();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = _allEntries.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(_searchQuery))
        {
            string q = _searchQuery.Trim().ToLowerInvariant();
            query = query.Where(e =>
                e.Name.ToLowerInvariant().Contains(q) ||
                e.Publisher.ToLowerInvariant().Contains(q) ||
                e.ExecutablePath.ToLowerInvariant().Contains(q));
        }

        FilteredEntries.Clear();
        foreach (var entry in query.OrderBy(e => e.Name))
        {
            FilteredEntries.Add(entry);
        }

        TotalEntriesCount = _allEntries.Count;
        EnabledCount = _allEntries.Count(e => e.IsEnabled);
        DisabledCount = _allEntries.Count(e => !e.IsEnabled);
    }

    private async Task ExecuteToggleEntryAsync(object? parameter)
    {
        if (parameter is StartupEntry entry)
        {
            bool newState = !entry.IsEnabled;
            string actionWord = newState ? "enable" : "disable";

            bool confirmed = await _mainViewModel.ShowConfirmAsync(
                title: $"{char.ToUpper(actionWord[0]) + actionWord.Substring(1)} Startup Program",
                message: $"Are you sure you want to {actionWord} '{entry.Name}' from automatically starting with Windows?\n\nThis safe operation does not delete the program registration.",
                actionButtonText: $"{char.ToUpper(actionWord[0]) + actionWord.Substring(1)}");

            if (!confirmed) return;

            bool success = await _startupService.ToggleStartupEntryStateAsync(entry, newState);
            if (success)
            {
                entry.IsEnabled = newState;
                entry.StateExplanation = newState ? "Enabled in Windows Startup" : "Disabled by User";
                _mainViewModel.ShowToast($"Startup entry '{entry.Name}' was {actionWord}d.", "Success");
                ApplyFilter();
            }
            else
            {
                _mainViewModel.ShowToast($"Failed to change startup status for '{entry.Name}'. Administrator permissions may be required.", "Error");
            }
        }
    }

    private void ExecuteOpenLocation(object? parameter)
    {
        if (parameter is StartupEntry entry && !string.IsNullOrWhiteSpace(entry.ExecutablePath))
        {
            try
            {
                string path = entry.ExecutablePath;
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{path}\"",
                        UseShellExecute = true
                    });
                }
                else
                {
                    _mainViewModel.ShowToast("Executable file does not exist on disk.", "Warning");
                }
            }
            catch (Exception ex)
            {
                _mainViewModel.ShowToast($"Failed to open location: {ex.Message}", "Error");
            }
        }
    }
}
