using AppTrace.Core.Data;
using AppTrace.Core.Models;
using AppTrace.Core.Services;

namespace AppTrace.Tests;

public class DatabaseAndSettingsTests
{
    [Fact]
    public async Task DatabaseService_LogsAndQueriesActivities()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "AppTrace_DbTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string dbPath = Path.Combine(tempDir, "audit.db");

        try
        {
            var db = new DatabaseService(dbPath);
            await db.InitializeAsync();

            await db.LogActivityAsync(new ActivityLogEntry
            {
                ActionType = "Scan",
                ApplicationName = "System",
                Description = "Full inventory scan completed",
                Success = true
            });

            await db.LogActivityAsync(new ActivityLogEntry
            {
                ActionType = "Uninstall",
                ApplicationName = "SampleApp",
                Description = "Uninstallation finished",
                Success = true
            });

            var all = await db.GetActivitiesAsync();
            Assert.Equal(2, all.Count);

            var uninstallsOnly = await db.GetActivitiesAsync(actionType: "Uninstall");
            Assert.Single(uninstallsOnly);
            Assert.Equal("SampleApp", uninstallsOnly[0].ApplicationName);

            await db.ClearActivitiesAsync();
            var empty = await db.GetActivitiesAsync();
            Assert.Empty(empty);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task SettingsService_SavesAndLoadsConfiguration()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "AppTrace_SettingsTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string settingsFile = Path.Combine(tempDir, "settings.json");

        try
        {
            var service = new SettingsService(settingsFile);
            var settings = new AppSettings
            {
                Theme = "Dark",
                BackdropType = "Acrylic",
                AccentColorHex = "#10B981",
                ScanMode = "Deep",
                SendToRecycleBin = true,
                CreateQuarantineBackup = true,
                ExcludedPaths = new List<string> { @"C:\CustomSafeFolder" }
            };

            await service.SaveSettingsAsync(settings);

            var reloadedService = new SettingsService(settingsFile);
            var loaded = await reloadedService.LoadSettingsAsync();

            Assert.Equal("Dark", loaded.Theme);
            Assert.Equal("Acrylic", loaded.BackdropType);
            Assert.Equal("#10B981", loaded.AccentColorHex);
            Assert.Equal("Deep", loaded.ScanMode);
            Assert.Contains(@"C:\CustomSafeFolder", loaded.ExcludedPaths);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
