namespace AppTrace.Core.Models;

public class ActivityLogEntry
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string ActionType { get; set; } = "Scan";
    public string ApplicationName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public bool Success { get; set; } = true;

    public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
}
