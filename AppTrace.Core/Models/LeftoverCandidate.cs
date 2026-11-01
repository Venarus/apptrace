namespace AppTrace.Core.Models;

public class LeftoverCandidate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Path { get; set; } = string.Empty;
    public CleanupItemType ItemType { get; set; } = CleanupItemType.Directory;
    public long EstimatedSizeBytes { get; set; }
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.Possible;
    public string DetectionReason { get; set; } = string.Empty;
    public DateTime? LastModified { get; set; }
    public string AssociatedAppName { get; set; } = string.Empty;
    public string AssociatedPublisher { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public bool IsSelected { get; set; } = false;
    public string? RegistryRoot { get; set; }
    public string? RegistrySubKey { get; set; }

    public string FormattedSize
    {
        get
        {
            if (ItemType == CleanupItemType.RegistryKey)
                return "Registry Entry";

            if (EstimatedSizeBytes <= 0)
                return "< 1 KB";

            const double KB = 1024.0;
            const double MB = KB * 1024.0;
            const double GB = MB * 1024.0;

            return EstimatedSizeBytes switch
            {
                >= 1073741824 => $"{EstimatedSizeBytes / GB:0.##} GB",
                >= 1048576 => $"{EstimatedSizeBytes / MB:0.##} MB",
                >= 1024 => $"{EstimatedSizeBytes / KB:0.##} KB",
                _ => $"{EstimatedSizeBytes} B"
            };
        }
    }

    public string ConfidenceBadge => Confidence switch
    {
        ConfidenceLevel.High => "High Confidence",
        ConfidenceLevel.Possible => "Possible Match",
        ConfidenceLevel.Uncertain => "Uncertain / Review",
        _ => "Unknown"
    };
}
