using System.Text.Json.Serialization;
using System.Windows.Media;

namespace AppTrace.Core.Models;

public class InstalledApp
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DisplayName { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string DisplayVersion { get; set; } = string.Empty;
    public DateTime? InstallDate { get; set; }
    public string? InstallLocation { get; set; }
    public long EstimatedSizeBytes { get; set; }
    public SizeConfidence SizeConfidence { get; set; } = SizeConfidence.Unknown;
    public string? UninstallString { get; set; }
    public string? QuietUninstallString { get; set; }
    public InstallSource InstallSource { get; set; } = InstallSource.Registry64;
    public string? IconPath { get; set; }

    [JsonIgnore]
    public ImageSource? IconSource { get; set; }

    public string? HelpLink { get; set; }
    public string? UrlInfoAbout { get; set; }
    public string? Comments { get; set; }
    public string? RegistryKeyPath { get; set; }
    public bool IsWindowsComponent { get; set; }
    public bool IsSystemProtected { get; set; }
    public AppCategory Category { get; set; } = AppCategory.Other;
    public List<string> RelatedDirectories { get; set; } = new();
    public string StartupStatus { get; set; } = "Not Registered";

    public bool HasUninstallCommand =>
        !string.IsNullOrWhiteSpace(UninstallString) ||
        !string.IsNullOrWhiteSpace(QuietUninstallString) ||
        InstallSource == InstallSource.MicrosoftStore;

    public string FormattedSize
    {
        get
        {
            if (SizeConfidence == SizeConfidence.Unknown || EstimatedSizeBytes <= 0)
                return "Unknown size";

            const double KB = 1024.0;
            const double MB = KB * 1024.0;
            const double GB = MB * 1024.0;

            string sizeStr = EstimatedSizeBytes switch
            {
                >= 1073741824 => $"{EstimatedSizeBytes / GB:0.##} GB",
                >= 1048576 => $"{EstimatedSizeBytes / MB:0.##} MB",
                >= 1024 => $"{EstimatedSizeBytes / KB:0.##} KB",
                _ => $"{EstimatedSizeBytes} bytes"
            };

            return SizeConfidence == SizeConfidence.Estimated
                ? $"~{sizeStr} (est.)"
                : sizeStr;
        }
    }

    public string FormattedInstallDate =>
        InstallDate.HasValue ? InstallDate.Value.ToString("MMM dd, yyyy") : "Unknown date";
}
