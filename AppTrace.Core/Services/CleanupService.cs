using System.IO;
using AppTrace.Core.Data;
using AppTrace.Core.Interop;
using AppTrace.Core.Models;
using Microsoft.Win32;

namespace AppTrace.Core.Services;

public class CleanupReport
{
    public int ItemsProcessed { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public long TotalFreedBytes { get; set; }
    public List<string> ErrorMessages { get; set; } = new();
    public List<string> RemovedPaths { get; set; } = new();

    public string FormattedFreedSize
    {
        get
        {
            if (TotalFreedBytes <= 0) return "0 bytes";
            const double KB = 1024.0;
            const double MB = KB * 1024.0;
            const double GB = MB * 1024.0;
            return TotalFreedBytes switch
            {
                >= 1073741824 => $"{TotalFreedBytes / GB:0.##} GB",
                >= 1048576 => $"{TotalFreedBytes / MB:0.##} MB",
                >= 1024 => $"{TotalFreedBytes / KB:0.##} KB",
                _ => $"{TotalFreedBytes} B"
            };
        }
    }
}

public interface ICleanupService
{
    Task<CleanupReport> CleanSelectedItemsAsync(
        IEnumerable<LeftoverCandidate> itemsToClean,
        bool createQuarantineBackup,
        bool sendToRecycleBin,
        CancellationToken cancellationToken = default);
}

public class CleanupService : ICleanupService
{
    private readonly ILeftoverScannerService _scannerService;
    private readonly IQuarantineService _quarantineService;
    private readonly IDatabaseService _databaseService;

    public CleanupService(
        ILeftoverScannerService scannerService,
        IQuarantineService quarantineService,
        IDatabaseService databaseService)
    {
        _scannerService = scannerService;
        _quarantineService = quarantineService;
        _databaseService = databaseService;
    }

    public async Task<CleanupReport> CleanSelectedItemsAsync(
        IEnumerable<LeftoverCandidate> itemsToClean,
        bool createQuarantineBackup,
        bool sendToRecycleBin,
        CancellationToken cancellationToken = default)
    {
        var report = new CleanupReport();
        var selectedList = itemsToClean.Where(i => i.IsSelected).ToList();
        report.ItemsProcessed = selectedList.Count;

        foreach (var item in selectedList)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                // Safety Guard: never delete protected paths
                if (_scannerService.IsProtectedPath(item.Path))
                {
                    report.FailedCount++;
                    report.ErrorMessages.Add($"Safety Block: Path '{item.Path}' is a protected Windows system path.");
                    continue;
                }

                if (item.ItemType == CleanupItemType.RegistryKey)
                {
                    // Registry key cleanup with mandatory backup
                    if (string.IsNullOrEmpty(item.RegistryRoot) || string.IsNullOrEmpty(item.RegistrySubKey))
                    {
                        report.FailedCount++;
                        report.ErrorMessages.Add($"Cannot clean registry key without root/subkey: {item.Path}");
                        continue;
                    }

                    // 1. Mandatory quarantine backup for registry
                    await _quarantineService.QuarantineRegistryKeyAsync(
                        item.RegistryRoot,
                        item.RegistrySubKey,
                        item.AssociatedAppName,
                        cancellationToken);

                    // 2. Remove registry subkey
                    bool removed = RemoveRegistryKey(item.RegistryRoot, item.RegistrySubKey);
                    if (removed)
                    {
                        report.SucceededCount++;
                        report.RemovedPaths.Add(item.Path);
                        await _databaseService.LogActivityAsync(new ActivityLogEntry
                        {
                            ActionType = "Cleanup",
                            ApplicationName = item.AssociatedAppName,
                            Description = $"Removed leftover registry key: {item.Path}",
                            Details = $"Registry key safely backed up to Quarantine and deleted.",
                            Success = true
                        });
                    }
                    else
                    {
                        report.FailedCount++;
                        report.ErrorMessages.Add($"Could not delete registry key: {item.Path}");
                    }
                    continue;
                }

                // File or Directory cleanup
                bool exists = Directory.Exists(item.Path) || File.Exists(item.Path);
                if (!exists)
                {
                    report.FailedCount++;
                    report.ErrorMessages.Add($"Item no longer exists on disk: {item.Path}");
                    continue;
                }

                // 1. Optional Quarantine Backup
                if (createQuarantineBackup)
                {
                    await _quarantineService.QuarantineFileOrDirectoryAsync(
                        item.Path,
                        item.AssociatedAppName,
                        cancellationToken);
                }

                // 2. Delete via Recycle Bin or Direct
                bool deleteSuccess = false;
                if (sendToRecycleBin)
                {
                    deleteSuccess = ShellNativeMethods.SendToRecycleBin(item.Path);
                }

                // Fallback to Directory.Delete if Recycle Bin was disabled or failed
                if (!deleteSuccess && !sendToRecycleBin)
                {
                    if (Directory.Exists(item.Path))
                    {
                        Directory.Delete(item.Path, true);
                        deleteSuccess = !Directory.Exists(item.Path);
                    }
                    else if (File.Exists(item.Path))
                    {
                        File.Delete(item.Path);
                        deleteSuccess = !File.Exists(item.Path);
                    }
                }

                if (deleteSuccess || (!Directory.Exists(item.Path) && !File.Exists(item.Path)))
                {
                    report.SucceededCount++;
                    report.TotalFreedBytes += item.EstimatedSizeBytes;
                    report.RemovedPaths.Add(item.Path);

                    await _databaseService.LogActivityAsync(new ActivityLogEntry
                    {
                        ActionType = "Cleanup",
                        ApplicationName = item.AssociatedAppName,
                        Description = $"Cleaned leftover: {Path.GetFileName(item.Path)} ({item.FormattedSize})",
                        Details = $"Path: {item.Path}, Method: {(sendToRecycleBin ? "Recycle Bin" : "Quarantine Permanent")}",
                        Success = true
                    });
                }
                else
                {
                    report.FailedCount++;
                    report.ErrorMessages.Add($"Could not remove item: {item.Path}. File may be locked by another process.");
                }
            }
            catch (Exception ex)
            {
                report.FailedCount++;
                report.ErrorMessages.Add($"Error cleaning '{item.Path}': {ex.Message}");
            }
        }

        return report;
    }

    private static bool RemoveRegistryKey(string hive, string subKey)
    {
        try
        {
            var baseKey = hive.Equals("HKCU", StringComparison.OrdinalIgnoreCase)
                ? Registry.CurrentUser
                : Registry.LocalMachine;

            int lastSlash = subKey.LastIndexOf('\\');
            if (lastSlash > 0)
            {
                string parentPath = subKey.Substring(0, lastSlash);
                string keyToDelete = subKey.Substring(lastSlash + 1);

                using var parent = baseKey.OpenSubKey(parentPath, writable: true);
                if (parent != null)
                {
                    parent.DeleteSubKeyTree(keyToDelete, throwOnMissingSubKey: false);
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
