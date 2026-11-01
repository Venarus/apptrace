namespace AppTrace.Core.Models;

public class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string Language { get; set; } = "tr";
    public string BackdropType { get; set; } = "Mica";
    public double WindowOpacity { get; set; } = 0.96;
    public string AccentColorHex { get; set; } = "#0078D4";
    public string ScanMode { get; set; } = "Conservative";
    public bool SendToRecycleBin { get; set; } = true;
    public bool CreateQuarantineBackup { get; set; } = true;
    public bool ExcludeWindowsApps { get; set; } = false;
    public List<string> ExcludedPaths { get; set; } = new()
    {
        @"C:\Windows",
        @"C:\Windows\System32",
        @"C:\Program Files\Common Files",
        @"C:\Program Files (x86)\Common Files"
    };
}
