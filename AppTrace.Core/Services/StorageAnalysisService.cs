using System.IO;
using AppTrace.Core.Models;

namespace AppTrace.Core.Services;

public interface IStorageAnalysisService
{
    StorageAnalysisResult AnalyzeStorage(
        IEnumerable<InstalledApp> installedApps,
        IEnumerable<LeftoverCandidate>? detectedLeftovers = null);
}

public class StorageAnalysisService : IStorageAnalysisService
{
    public StorageAnalysisResult AnalyzeStorage(
        IEnumerable<InstalledApp> installedApps,
        IEnumerable<LeftoverCandidate>? detectedLeftovers = null)
    {
        var appList = installedApps.ToList();
        var result = new StorageAnalysisResult();

        // 1. Get primary drive capacity and free space
        try
        {
            var systemDrive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
            if (systemDrive.IsReady)
            {
                result.TotalDriveCapacityBytes = systemDrive.TotalSize;
                result.TotalDriveFreeBytes = systemDrive.TotalFreeSpace;
            }
        }
        catch
        {
            result.TotalDriveCapacityBytes = 512L * 1024 * 1024 * 1024;
            result.TotalDriveFreeBytes = 256L * 1024 * 1024 * 1024;
        }

        // 2. Total App Storage
        result.TotalAppStorageBytes = appList.Sum(a => a.EstimatedSizeBytes);

        // 3. Removable leftovers
        if (detectedLeftovers != null)
        {
            result.RemovableCandidateBytes = detectedLeftovers.Sum(l => l.EstimatedSizeBytes);
        }

        // 4. Largest applications (Top 10)
        result.LargestApps = appList
            .Where(a => a.EstimatedSizeBytes > 0)
            .OrderByDescending(a => a.EstimatedSizeBytes)
            .Take(10)
            .ToList();

        // 5. Category breakdown
        var categoryColors = new Dictionary<AppCategory, string>
        {
            [AppCategory.Development] = "#6366F1", // Indigo
            [AppCategory.Productivity] = "#10B981", // Emerald
            [AppCategory.Internet] = "#06B6D4", // Cyan
            [AppCategory.Multimedia] = "#EC4899", // Pink
            [AppCategory.Utilities] = "#F59E0B", // Amber
            [AppCategory.Games] = "#8B5CF6", // Purple
            [AppCategory.System] = "#64748B", // Slate
            [AppCategory.Other] = "#94A3B8" // Gray
        };

        var catGroups = appList
            .GroupBy(a => a.Category)
            .Select(g => new StorageCategoryBreakdown
            {
                Category = g.Key,
                AppCount = g.Count(),
                TotalSizeBytes = g.Sum(a => a.EstimatedSizeBytes),
                ColorHex = categoryColors.GetValueOrDefault(g.Key, "#3B82F6"),
                Percentage = result.TotalAppStorageBytes > 0
                    ? Math.Round((double)g.Sum(a => a.EstimatedSizeBytes) / result.TotalAppStorageBytes * 100.0, 1)
                    : 0
            })
            .OrderByDescending(c => c.TotalSizeBytes)
            .ToList();

        result.Categories = catGroups;

        // 6. Size ranges
        long mb100 = 100L * 1024 * 1024;
        long gb1 = 1024L * 1024 * 1024;
        long gb5 = 5L * 1024 * 1024 * 1024;

        result.SizeRanges = new List<SizeRangeBreakdown>
        {
            new()
            {
                Label = "> 5 GB (Massive)",
                Count = appList.Count(a => a.EstimatedSizeBytes >= gb5),
                TotalSizeBytes = appList.Where(a => a.EstimatedSizeBytes >= gb5).Sum(a => a.EstimatedSizeBytes),
                ColorHex = "#EF4444" // Red
            },
            new()
            {
                Label = "1 GB - 5 GB (Large)",
                Count = appList.Count(a => a.EstimatedSizeBytes >= gb1 && a.EstimatedSizeBytes < gb5),
                TotalSizeBytes = appList.Where(a => a.EstimatedSizeBytes >= gb1 && a.EstimatedSizeBytes < gb5).Sum(a => a.EstimatedSizeBytes),
                ColorHex = "#F59E0B" // Amber
            },
            new()
            {
                Label = "100 MB - 1 GB (Medium)",
                Count = appList.Count(a => a.EstimatedSizeBytes >= mb100 && a.EstimatedSizeBytes < gb1),
                TotalSizeBytes = appList.Where(a => a.EstimatedSizeBytes >= mb100 && a.EstimatedSizeBytes < gb1).Sum(a => a.EstimatedSizeBytes),
                ColorHex = "#3B82F6" // Blue
            },
            new()
            {
                Label = "< 100 MB (Light)",
                Count = appList.Count(a => a.EstimatedSizeBytes > 0 && a.EstimatedSizeBytes < mb100),
                TotalSizeBytes = appList.Where(a => a.EstimatedSizeBytes > 0 && a.EstimatedSizeBytes < mb100).Sum(a => a.EstimatedSizeBytes),
                ColorHex = "#10B981" // Emerald
            },
            new()
            {
                Label = "Unknown Size",
                Count = appList.Count(a => a.EstimatedSizeBytes <= 0),
                TotalSizeBytes = 0,
                ColorHex = "#64748B" // Slate
            }
        };

        return result;
    }
}
