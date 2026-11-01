using AppTrace.Core.Models;
using AppTrace.Core.Services;

namespace AppTrace.Tests;

public class UninstallerTests
{
    private readonly UninstallerService _service = new();

    [Fact]
    public async Task PrepareUninstallAsync_ParsesMsiCommand()
    {
        var app = new InstalledApp
        {
            DisplayName = "Test MSI Application",
            Publisher = "Test Corp",
            UninstallString = "MsiExec.exe /I{12345678-ABCD-1234-ABCD-1234567890AB}"
        };

        var plan = await _service.PrepareUninstallAsync(app);

        Assert.True(plan.CanExecute);
        Assert.Equal("Windows Installer (MSI)", plan.UninstallType);
        Assert.Contains("msiexec.exe", plan.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("/x {12345678-ABCD-1234-ABCD-1234567890AB}", plan.Arguments);
    }

    [Fact]
    public async Task PrepareUninstallAsync_BlocksProtectedComponents()
    {
        var app = new InstalledApp
        {
            DisplayName = "Microsoft Edge WebView2 Runtime",
            Publisher = "Microsoft Corporation",
            IsSystemProtected = true,
            UninstallString = "C:\\Windows\\System32\\uninstall.exe"
        };

        var plan = await _service.PrepareUninstallAsync(app);

        Assert.False(plan.CanExecute);
        Assert.NotNull(plan.SafetyWarning);
        Assert.Contains("essential Windows component", plan.SafetyWarning);
    }

    [Fact]
    public async Task PrepareUninstallAsync_ConfiguresStoreApps()
    {
        var app = new InstalledApp
        {
            DisplayName = "Spotify",
            Publisher = "Spotify AB",
            InstallSource = InstallSource.MicrosoftStore
        };

        var plan = await _service.PrepareUninstallAsync(app);

        Assert.True(plan.CanExecute);
        Assert.True(plan.IsStoreApp);
        Assert.Equal("powershell.exe", plan.ExecutablePath);
        Assert.Contains("Remove-AppxPackage", plan.Arguments);
    }
}
