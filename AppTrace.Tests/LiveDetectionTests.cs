using AppTrace.Core.Services;
using Xunit.Abstractions;

namespace AppTrace.Tests;

public class LiveDetectionTests
{
    private readonly ITestOutputHelper _output;

    public LiveDetectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task LiveSystemDetection_ChecksSystem()
    {
        var service = new AppDetectionService();
        var apps = await service.DetectInstalledApplicationsAsync();
        _output.WriteLine($"Total Apps: {apps.Count}");
        foreach (var a in apps.Take(20))
        {
            _output.WriteLine($"App: '{a.DisplayName}' | Publisher: '{a.Publisher}' | Source: {a.InstallSource} | Size: {a.FormattedSize}");
        }
        Assert.NotEmpty(apps);
    }
}
