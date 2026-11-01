using AppTrace.Core.Models;
using AppTrace.Core.Services;

namespace AppTrace.Tests;

public class StorageAnalysisTests
{
    private readonly StorageAnalysisService _service = new();

    [Fact]
    public void AnalyzeStorage_CalculatesCorrectCategoriesAndSizeRanges()
    {
        var apps = new List<InstalledApp>
        {
            new()
            {
                DisplayName = "Visual Studio 2022",
                Publisher = "Microsoft Corporation",
                Category = AppCategory.Development,
                EstimatedSizeBytes = 8L * 1024 * 1024 * 1024 // 8 GB
            },
            new()
            {
                DisplayName = "Adobe Photoshop 2024",
                Publisher = "Adobe Systems",
                Category = AppCategory.Multimedia,
                EstimatedSizeBytes = 3L * 1024 * 1024 * 1024 // 3 GB
            },
            new()
            {
                DisplayName = "7-Zip",
                Publisher = "Igor Pavlov",
                Category = AppCategory.Utilities,
                EstimatedSizeBytes = 5L * 1024 * 1024 // 5 MB
            }
        };

        var leftovers = new List<LeftoverCandidate>
        {
            new()
            {
                Path = "C:\\test\\remnant",
                EstimatedSizeBytes = 250L * 1024 * 1024 // 250 MB
            }
        };

        var result = _service.AnalyzeStorage(apps, leftovers);

        Assert.Equal(apps.Sum(a => a.EstimatedSizeBytes), result.TotalAppStorageBytes);
        Assert.Equal(250L * 1024 * 1024, result.RemovableCandidateBytes);
        Assert.Equal(3, result.LargestApps.Count);
        Assert.Equal("Visual Studio 2022", result.LargestApps[0].DisplayName);

        // Check size ranges
        var massiveRange = result.SizeRanges.First(r => r.Label.Contains("> 5 GB"));
        Assert.Equal(1, massiveRange.Count);

        var largeRange = result.SizeRanges.First(r => r.Label.Contains("1 GB - 5 GB"));
        Assert.Equal(1, largeRange.Count);

        var lightRange = result.SizeRanges.First(r => r.Label.Contains("< 100 MB"));
        Assert.Equal(1, lightRange.Count);
    }
}
