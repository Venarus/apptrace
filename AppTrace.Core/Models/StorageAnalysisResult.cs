namespace AppTrace.Core.Models;

public class StorageCategoryBreakdown
{
    public AppCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public long TotalSizeBytes { get; set; }
    public int AppCount { get; set; }
    public double Percentage { get; set; }
    public string ColorHex { get; set; } = "#3B82F6";

    public string FormattedSize
    {
        get
        {
            const double KB = 1024.0;
            const double MB = KB * 1024.0;
            const double GB = MB * 1024.0;
            return TotalSizeBytes switch
            {
                >= 1073741824 => $"{TotalSizeBytes / GB:0.##} GB",
                >= 1048576 => $"{TotalSizeBytes / MB:0.##} MB",
                >= 1024 => $"{TotalSizeBytes / KB:0.##} KB",
                _ => $"{TotalSizeBytes} B"
            };
        }
    }
}

public class SizeRangeBreakdown
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public long TotalSizeBytes { get; set; }
    public string ColorHex { get; set; } = "#0078D4";
}

public class StorageAnalysisResult
{
    public long TotalDriveCapacityBytes { get; set; }
    public long TotalDriveFreeBytes { get; set; }
    public long TotalDriveUsedBytes => TotalDriveCapacityBytes - TotalDriveFreeBytes;
    public long TotalAppStorageBytes { get; set; }
    public long RemovableCandidateBytes { get; set; }
    public List<StorageCategoryBreakdown> Categories { get; set; } = new();
    public List<SizeRangeBreakdown> SizeRanges { get; set; } = new();
    public List<InstalledApp> LargestApps { get; set; } = new();
}
