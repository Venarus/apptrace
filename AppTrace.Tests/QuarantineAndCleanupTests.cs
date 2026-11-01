using AppTrace.Core.Data;
using AppTrace.Core.Models;
using AppTrace.Core.Services;

namespace AppTrace.Tests;

public class QuarantineAndCleanupTests
{
    [Fact]
    public async Task QuarantineAndRestore_VerifiesRestorationIntegrity()
    {
        string testBaseDir = Path.Combine(Path.GetTempPath(), "AppTrace_Tests_" + Guid.NewGuid().ToString("N"));
        string dbPath = Path.Combine(testBaseDir, "test_audit.db");
        string quarantineDir = Path.Combine(testBaseDir, "Quarantine");
        string sourceDir = Path.Combine(testBaseDir, "OriginalAppDir");

        Directory.CreateDirectory(testBaseDir);
        Directory.CreateDirectory(sourceDir);

        string sampleFile = Path.Combine(sourceDir, "data.txt");
        await File.WriteAllTextAsync(sampleFile, "Hello AppTrace Safe Quarantine!");

        try
        {
            var db = new DatabaseService(dbPath);
            await db.InitializeAsync();

            var quarantineService = new QuarantineService(db, quarantineDir);

            // 1. Quarantine file
            var item = await quarantineService.QuarantineFileOrDirectoryAsync(
                sampleFile,
                relatedAppName: "UnitTestApp");

            Assert.NotNull(item);
            Assert.True(File.Exists(item.BackupPath));

            // Delete original file to simulate removal
            File.Delete(sampleFile);
            Assert.False(File.Exists(sampleFile));

            // 2. Restore file
            bool restoreSuccess = await quarantineService.RestoreItemAsync(item.Id);
            Assert.True(restoreSuccess);
            Assert.True(File.Exists(sampleFile));

            string restoredContent = await File.ReadAllTextAsync(sampleFile);
            Assert.Equal("Hello AppTrace Safe Quarantine!", restoredContent);
        }
        finally
        {
            if (Directory.Exists(testBaseDir))
            {
                try { Directory.Delete(testBaseDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task CleanupService_BlocksProtectedSystemPaths()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "AppTrace_Safety_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var db = new DatabaseService(Path.Combine(tempDir, "test.db"));
            var scanner = new LeftoverScannerService();
            var quarantine = new QuarantineService(db, Path.Combine(tempDir, "Quarantine"));
            var cleanup = new CleanupService(scanner, quarantine, db);

            var dangerousCandidate = new LeftoverCandidate
            {
                Path = @"C:\Windows\System32",
                IsSelected = true,
                ItemType = CleanupItemType.Directory,
                EstimatedSizeBytes = 1000000,
                AssociatedAppName = "Windows System"
            };

            var report = await cleanup.CleanSelectedItemsAsync(
                new[] { dangerousCandidate },
                createQuarantineBackup: false,
                sendToRecycleBin: false);

            Assert.Equal(1, report.FailedCount);
            Assert.Equal(0, report.SucceededCount);
            Assert.Contains(report.ErrorMessages, msg => msg.Contains("Safety Block"));
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
