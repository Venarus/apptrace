using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using AppTrace.Core.Data;
using AppTrace.Core.Models;
using Microsoft.Win32;

namespace AppTrace.UI.ViewModels;

public class ActivityHistoryViewModel : ViewModelBase
{
    private readonly MainViewModel _mainViewModel;
    public ILocalizationService Loc => _mainViewModel.Loc;
    private readonly IDatabaseService _databaseService;

    private string _selectedActionFilter = "All";
    private string _searchQuery = string.Empty;
    private bool _isLoading;

    public ObservableCollection<ActivityLogEntry> Activities { get; } = new();

    public string SelectedActionFilter
    {
        get => _selectedActionFilter;
        set
        {
            if (SetProperty(ref _selectedActionFilter, value))
                _ = LoadActivitiesAsync();
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
                _ = LoadActivitiesAsync();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public AsyncRelayCommand RefreshActivitiesCommand { get; }
    public AsyncRelayCommand ClearActivitiesCommand { get; }
    public AsyncRelayCommand ExportCsvCommand { get; }

    public ActivityHistoryViewModel(
        MainViewModel mainViewModel,
        IDatabaseService databaseService)
    {
        _mainViewModel = mainViewModel;
        _databaseService = databaseService;

        RefreshActivitiesCommand = new AsyncRelayCommand(LoadActivitiesAsync);
        ClearActivitiesCommand = new AsyncRelayCommand(ExecuteClearActivitiesAsync);
        ExportCsvCommand = new AsyncRelayCommand(ExecuteExportCsvAsync);
    }

    public async Task LoadActivitiesAsync()
    {
        IsLoading = true;
        try
        {
            var list = await _databaseService.GetActivitiesAsync(
                limit: 250,
                actionType: _selectedActionFilter == "All" ? null : _selectedActionFilter,
                search: string.IsNullOrWhiteSpace(_searchQuery) ? null : _searchQuery.Trim());

            Activities.Clear();
            foreach (var item in list)
            {
                Activities.Add(item);
            }
        }
        catch (Exception ex)
        {
            _mainViewModel.ShowToast($"Failed to load activity logs: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExecuteClearActivitiesAsync()
    {
        bool confirmed = await _mainViewModel.ShowConfirmAsync(
            title: "Clear Activity History",
            message: "Are you sure you want to permanently clear the activity history log?",
            actionButtonText: "Clear History",
            isDestructive: true);

        if (!confirmed) return;

        try
        {
            await _databaseService.ClearActivitiesAsync();
            Activities.Clear();
            _mainViewModel.ShowToast("Activity history cleared.", "Success");
        }
        catch (Exception ex)
        {
            _mainViewModel.ShowToast($"Failed to clear activity history: {ex.Message}", "Error");
        }
    }

    private async Task ExecuteExportCsvAsync()
    {
        try
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "Export Activity History to CSV",
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                FileName = $"AppTrace_History_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Id,Timestamp,ActionType,ApplicationName,Description,Details,Success");

                var allLogs = await _databaseService.GetActivitiesAsync(limit: 5000);
                foreach (var log in allLogs)
                {
                    string safeDesc = EscapeCsv(log.Description);
                    string safeDetails = EscapeCsv(log.Details);
                    string safeApp = EscapeCsv(log.ApplicationName);
                    sb.AppendLine($"{log.Id},{log.FormattedTimestamp},{log.ActionType},{safeApp},{safeDesc},{safeDetails},{log.Success}");
                }

                await File.WriteAllTextAsync(saveDialog.FileName, sb.ToString(), Encoding.UTF8);
                _mainViewModel.ShowToast($"Exported history to {Path.GetFileName(saveDialog.FileName)}", "Success");
            }
        }
        catch (Exception ex)
        {
            _mainViewModel.ShowToast($"Failed to export history: {ex.Message}", "Error");
        }
    }

    private static string EscapeCsv(string text)
    {
        if (string.IsNullOrEmpty(text)) return "\"\"";
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}
