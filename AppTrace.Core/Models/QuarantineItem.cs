namespace AppTrace.Core.Models;

public class QuarantineItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OriginalPath { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
    public DateTime OperationDate { get; set; } = DateTime.Now;
    public string RelatedAppName { get; set; } = string.Empty;
    public string ItemType { get; set; } = "Directory";
    public long SizeBytes { get; set; }
    public string ManifestJson { get; set; } = string.Empty;
    public bool IsRestored { get; set; } = false;

    public string FormattedSize
    {
        get
        {
            if (SizeBytes <= 0) return "< 1 KB";
            const double KB = 1024.0;
            const double MB = KB * 1024.0;
            const double GB = MB * 1024.0;
            return SizeBytes switch
            {
                >= 1073741824 => $"{SizeBytes / GB:0.##} GB",
                >= 1048576 => $"{SizeBytes / MB:0.##} MB",
                >= 1024 => $"{SizeBytes / KB:0.##} KB",
                _ => $"{SizeBytes} B"
            };
        }
    }
}
