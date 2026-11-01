namespace AppTrace.Core.Models;

public class StartupEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string RawCommand { get; set; } = string.Empty;
    public StartupSource Source { get; set; } = StartupSource.HKCU_Run;
    public string SourceDescription => Source switch
    {
        StartupSource.HKCU_Run => "Registry (Current User Run)",
        StartupSource.HKLM_Run => "Registry (All Users Run)",
        StartupSource.HKLM_WOW64_Run => "Registry (32-bit All Users Run)",
        StartupSource.UserStartupFolder => "Startup Folder (Current User)",
        StartupSource.CommonStartupFolder => "Startup Folder (Common)",
        _ => "Startup"
    };
    public bool IsEnabled { get; set; } = true;
    public string StateExplanation { get; set; } = "Enabled via Windows Startup";
    public bool IsFileExisting { get; set; } = true;
    public long FileSizeBytes { get; set; }

    public string FormattedSize
    {
        get
        {
            if (!IsFileExisting || FileSizeBytes <= 0)
                return "Unknown";

            const double KB = 1024.0;
            const double MB = KB * 1024.0;
            return FileSizeBytes switch
            {
                >= 1048576 => $"{FileSizeBytes / MB:0.##} MB",
                >= 1024 => $"{FileSizeBytes / KB:0.##} KB",
                _ => $"{FileSizeBytes} B"
            };
        }
    }
}
