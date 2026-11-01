using AppTrace.Core.Models;
using AppTrace.Core.Services;

namespace AppTrace.Tests;

public class DetectionTests
{
    [Fact]
    public void CategorizeApp_IdentifiesDeveloperTools()
    {
        var category = AppDetectionService.CategorizeApp("Visual Studio Code", "Microsoft Corporation");
        Assert.Equal(AppCategory.Development, category);

        category = AppDetectionService.CategorizeApp("Git version 2.40", "The Git Development Community");
        Assert.Equal(AppCategory.Development, category);

        category = AppDetectionService.CategorizeApp("Python 3.11 (64-bit)", "Python Software Foundation");
        Assert.Equal(AppCategory.Development, category);
    }

    [Fact]
    public void CategorizeApp_IdentifiesProductivitySoftware()
    {
        var category = AppDetectionService.CategorizeApp("Microsoft Word", "Microsoft Corporation");
        Assert.Equal(AppCategory.Productivity, category);

        category = AppDetectionService.CategorizeApp("Adobe Acrobat Reader DC", "Adobe Inc.");
        Assert.Equal(AppCategory.Productivity, category);

        category = AppDetectionService.CategorizeApp("Slack", "Slack Technologies");
        Assert.Equal(AppCategory.Productivity, category);
    }

    [Fact]
    public void CategorizeApp_IdentifiesBrowsersAndInternet()
    {
        var category = AppDetectionService.CategorizeApp("Google Chrome", "Google LLC");
        Assert.Equal(AppCategory.Internet, category);

        category = AppDetectionService.CategorizeApp("Mozilla Firefox (x64 en-US)", "Mozilla");
        Assert.Equal(AppCategory.Internet, category);

        category = AppDetectionService.CategorizeApp("Discord", "Discord Inc.");
        Assert.Equal(AppCategory.Internet, category);
    }

    [Fact]
    public void CategorizeApp_IdentifiesGamesAndLaunchers()
    {
        var category = AppDetectionService.CategorizeApp("Steam", "Valve Corporation");
        Assert.Equal(AppCategory.Games, category);

        category = AppDetectionService.CategorizeApp("Epic Games Launcher", "Epic Games, Inc.");
        Assert.Equal(AppCategory.Games, category);
    }

    [Fact]
    public void FormattedSize_DistinguishesVerifiedEstimatedAndUnknown()
    {
        var appUnknown = new InstalledApp
        {
            DisplayName = "TestApp",
            EstimatedSizeBytes = 0,
            SizeConfidence = SizeConfidence.Unknown
        };
        Assert.Equal("Unknown size", appUnknown.FormattedSize);

        var appEstimated = new InstalledApp
        {
            DisplayName = "TestApp",
            EstimatedSizeBytes = 1048576 * 250, // 250 MB
            SizeConfidence = SizeConfidence.Estimated
        };
        Assert.Contains("~", appEstimated.FormattedSize);
        Assert.Contains("(est.)", appEstimated.FormattedSize);
        Assert.Contains("250 MB", appEstimated.FormattedSize);

        var appVerified = new InstalledApp
        {
            DisplayName = "TestApp",
            EstimatedSizeBytes = 1073741824L * 2, // 2 GB
            SizeConfidence = SizeConfidence.Verified
        };
        Assert.DoesNotContain("~", appVerified.FormattedSize);
        Assert.DoesNotContain("(est.)", appVerified.FormattedSize);
        Assert.Equal("2 GB", appVerified.FormattedSize);
    }

    [Fact]
    public void HasUninstallCommand_ReflectsAvailableOptions()
    {
        var app1 = new InstalledApp { UninstallString = "C:\\app\\unins000.exe" };
        Assert.True(app1.HasUninstallCommand);

        var app2 = new InstalledApp { QuietUninstallString = "msiexec /qn /x {12345}" };
        Assert.True(app2.HasUninstallCommand);

        var app3 = new InstalledApp { InstallSource = InstallSource.MicrosoftStore };
        Assert.True(app3.HasUninstallCommand);

        var app4 = new InstalledApp { UninstallString = null, QuietUninstallString = null, InstallSource = InstallSource.Registry64 };
        Assert.False(app4.HasUninstallCommand);
    }
}
