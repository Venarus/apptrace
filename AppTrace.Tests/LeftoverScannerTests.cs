using AppTrace.Core.Models;
using AppTrace.Core.Services;

namespace AppTrace.Tests;

public class LeftoverScannerTests
{
    private readonly LeftoverScannerService _scanner = new();

    [Theory]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"C:\Program Files")]
    [InlineData(@"C:\Program Files (x86)")]
    public void IsProtectedPath_ProhibitsCriticalSystemLocations(string path)
    {
        bool isProtected = _scanner.IsProtectedPath(path);
        Assert.True(isProtected, $"Path '{path}' MUST be protected against accidental deletion.");
    }

    [Fact]
    public void IsProtectedPath_AllowsSafeAppSpecificDirectories()
    {
        string safePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyRandomUninstalledApp_12345");

        bool isProtected = _scanner.IsProtectedPath(safePath);
        Assert.False(isProtected, "App-specific directory under LocalAppData should be permissible for remnant cleanup.");
    }

    [Fact]
    public async Task ScanForAppLeftoversAsync_FindsResidualFolders()
    {
        // Create an isolated temporary remnant folder
        string tempAppDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AppTrace_TestRemnant_" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempAppDir);
            File.WriteAllText(Path.Combine(tempAppDir, "settings.json"), "{}");
            File.WriteAllText(Path.Combine(tempAppDir, "cache.tmp"), "test data");

            var testApp = new InstalledApp
            {
                DisplayName = Path.GetFileName(tempAppDir),
                Publisher = "TestVendor"
            };

            var results = await _scanner.ScanForAppLeftoversAsync(testApp);

            Assert.NotEmpty(results);
            var match = results.FirstOrDefault(r => r.Path.Equals(tempAppDir, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(match);
            Assert.Equal(ConfidenceLevel.High, match.Confidence);
            Assert.Contains("Verified remnants", match.DetectionReason);
        }
        finally
        {
            if (Directory.Exists(tempAppDir))
            {
                Directory.Delete(tempAppDir, true);
            }
        }
    }
}
