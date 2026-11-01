using System.Collections.ObjectModel;
using AppTrace.Core.Models;
using AppTrace.Core.Services;

namespace AppTrace.UI.ViewModels;

public class QuarantineViewModel : ViewModelBase
{
    private readonly MainViewModel _mainViewModel;
    public ILocalizationService Loc => _mainViewModel.Loc;
    private readonly IQuarantineService _quarantineService;

    private int _totalItemsCount;
    private string _totalQuarantinedSize = "0 MB";
    private QuarantineItem? _selectedItem;
    private bool _isLoading;

    public ObservableCollection<QuarantineItem> QuarantineItems { get; } = new();

    public int TotalItemsCount
    {
        get => _totalItemsCount;
        set => SetProperty(ref _totalItemsCount, value);
    }

    public string TotalQuarantinedSize
    {
        get => _totalQuarantinedSize;
        set => SetProperty(ref _totalQuarantinedSize, value);
    }

    public QuarantineItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public AsyncRelayCommand RefreshItemsCommand { get; }
    public AsyncRelayCommand RestoreItemCommand { get; }
    public AsyncRelayCommand PermanentlyDeleteItemCommand { get; }

    public QuarantineViewModel(
        MainViewModel mainViewModel,
        IQuarantineService quarantineService)
    {
        _mainViewModel = mainViewModel;
        _quarantineService = quarantineService;

        RefreshItemsCommand = new AsyncRelayCommand(LoadItemsAsync);
        RestoreItemCommand = new AsyncRelayCommand(ExecuteRestoreItemAsync);
        PermanentlyDeleteItemCommand = new AsyncRelayCommand(ExecutePermanentlyDeleteAsync);
    }

    public async Task LoadItemsAsync()
    {
        IsLoading = true;
        try
        {
            var items = await _quarantineService.GetQuarantinedItemsAsync();
            QuarantineItems.Clear();
            foreach (var item in items)
            {
                QuarantineItems.Add(item);
            }

            TotalItemsCount = QuarantineItems.Count;
            long totalBytes = QuarantineItems.Sum(i => i.SizeBytes);
            TotalQuarantinedSize = FormatBytes(totalBytes);
        }
        catch (Exception ex)
        {
            _mainViewModel.ShowToast($"Failed to load quarantine items: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExecuteRestoreItemAsync(object? parameter)
    {
        var item = parameter as QuarantineItem ?? SelectedItem;
        if (item == null) return;

        bool confirmed = await _mainViewModel.ShowConfirmAsync(
            title: "Restore Quarantined Item",
            message: $"Are you sure you want to restore this item back to its original location?\n\nOriginal Path: {item.OriginalPath}",
            actionButtonText: "Restore Item");

        if (!confirmed) return;

        IsLoading = true;
        try
        {
            bool success = await _quarantineService.RestoreItemAsync(item.Id);
            if (success)
            {
                _mainViewModel.ShowToast($"Successfully restored '{item.RelatedAppName}' to {item.OriginalPath}", "Success");
                await LoadItemsAsync();
            }
            else
            {
                _mainViewModel.ShowToast("Restoration failed. Target destination may be locked or unwritable.", "Error");
            }
        }
        catch (Exception ex)
        {
            _mainViewModel.ShowToast($"Restoration error: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExecutePermanentlyDeleteAsync(object? parameter)
    {
        var item = parameter as QuarantineItem ?? SelectedItem;
        if (item == null) return;

        bool confirmed = await _mainViewModel.ShowConfirmAsync(
            title: "Permanently Delete Item",
            message: $"This will permanently delete the quarantine backup for '{item.RelatedAppName}'. This operation cannot be undone.\n\nAre you sure?",
            actionButtonText: "Delete Permanently",
            isDestructive: true);

        if (!confirmed) return;

        IsLoading = true;
        try
        {
            bool success = await _quarantineService.PermanentlyDeleteItemAsync(item.Id);
            if (success)
            {
                _mainViewModel.ShowToast("Item permanently deleted from quarantine.", "Success");
                await LoadItemsAsync();
            }
            else
            {
                _mainViewModel.ShowToast("Failed to delete quarantine file.", "Error");
            }
        }
        catch (Exception ex)
        {
            _mainViewModel.ShowToast($"Error deleting item: {ex.Message}", "Error");
        }
        finally
        {
            IsLoading = false;
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
