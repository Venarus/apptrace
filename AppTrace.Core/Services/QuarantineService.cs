using System.Diagnostics;
using System.IO;
using System.Text.Json;
using AppTrace.Core.Data;
using AppTrace.Core.Models;
using Microsoft.Win32;

namespace AppTrace.Core.Services;

public interface IQuarantineService
{
    Task<QuarantineItem> QuarantineFileOrDirectoryAsync(string path, string relatedAppName, CancellationToken cancellationToken = default);
    Task<QuarantineItem> QuarantineRegistryKeyAsync(string hive, string subKey, string relatedAppName, CancellationToken cancellationToken = default);
    Task<bool> RestoreItemAsync(string quarantineId, CancellationToken cancellationToken = default);
    Task<bool> PermanentlyDeleteItemAsync(string quarantineId, CancellationToken cancellationToken = default);
    Task<List<QuarantineItem>> GetQuarantinedItemsAsync();
}

public class QuarantineService : IQuarantineService
{
    private readonly IDatabaseService _databaseService;
    private readonly string _quarantineBaseDir;

    public QuarantineService(IDatabaseService databaseService, string? customQuarantineDir = null)
    {
        _databaseService = databaseService;
        _quarantineBaseDir = customQuarantineDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AppTrace",
            "Quarantine");
    }

    public async Task<QuarantineItem> QuarantineFileOrDirectoryAsync(
        string path,
        string relatedAppName,
        CancellationToken cancellationToken = default)
    {
        string id = Guid.NewGuid().ToString("N");
        string itemBackupDir = Path.Combine(_quarantineBaseDir, id);
        Directory.CreateDirectory(itemBackupDir);

        long sizeBytes = 0;
        bool isDir = Directory.Exists(path);
        string itemType = isDir ? "Directory" : "File";
        string targetBackupPath;

        if (isDir)
        {
            string folderName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
            targetBackupPath = Path.Combine(itemBackupDir, folderName);
            Directory.CreateDirectory(targetBackupPath);

            var dirInfo = new DirectoryInfo(path);
            foreach (var f in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                if (cancellationToken.IsCancellationRequested) break;
                sizeBytes += f.Length;
                string rel = Path.GetRelativePath(path, f.FullName);
                string destFile = Path.Combine(targetBackupPath, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                f.CopyTo(destFile, true);
            }
        }
        else
        {
            string fileName = Path.GetFileName(path);
            targetBackupPath = Path.Combine(itemBackupDir, fileName);
            File.Copy(path, targetBackupPath, true);
            sizeBytes = new FileInfo(path).Length;
        }

        var item = new QuarantineItem
        {
            Id = id,
            OriginalPath = path,
            BackupPath = targetBackupPath,
            OperationDate = DateTime.Now,
            RelatedAppName = relatedAppName,
            ItemType = itemType,
            SizeBytes = sizeBytes,
            ManifestJson = JsonSerializer.Serialize(new
            {
                Id = id,
                OriginalPath = path,
                BackupPath = targetBackupPath,
                Date = DateTime.Now,
                ItemType = itemType,
                SizeBytes = sizeBytes
            }),
            IsRestored = false
        };

        await _databaseService.AddQuarantineItemAsync(item);
        return item;
    }

    public async Task<QuarantineItem> QuarantineRegistryKeyAsync(
        string hive,
        string subKey,
        string relatedAppName,
        CancellationToken cancellationToken = default)
    {
        string id = Guid.NewGuid().ToString("N");
        string itemBackupDir = Path.Combine(_quarantineBaseDir, id);
        Directory.CreateDirectory(itemBackupDir);

        string regBackupPath = Path.Combine(itemBackupDir, "registry_backup.reg");

        // Export registry key via reg.exe export
        string fullKeyPath = $@"{hive}\{subKey}";
        var psi = new ProcessStartInfo
        {
            FileName = "reg.exe",
            Arguments = $"export \"{fullKeyPath}\" \"{regBackupPath}\" /y",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        using var proc = Process.Start(psi);
        if (proc != null)
        {
            await proc.WaitForExitAsync(cancellationToken);
        }

        long sizeBytes = File.Exists(regBackupPath) ? new FileInfo(regBackupPath).Length : 0;

        var item = new QuarantineItem
        {
            Id = id,
            OriginalPath = fullKeyPath,
            BackupPath = regBackupPath,
            OperationDate = DateTime.Now,
            RelatedAppName = relatedAppName,
            ItemType = "RegistryKey",
            SizeBytes = sizeBytes,
            ManifestJson = JsonSerializer.Serialize(new
            {
                Id = id,
                Hive = hive,
                SubKey = subKey,
                OriginalPath = fullKeyPath,
                BackupPath = regBackupPath,
                Date = DateTime.Now
            }),
            IsRestored = false
        };

        await _databaseService.AddQuarantineItemAsync(item);
        return item;
    }

    public async Task<bool> RestoreItemAsync(string quarantineId, CancellationToken cancellationToken = default)
    {
        var items = await _databaseService.GetQuarantineItemsAsync(includeRestored: false);
        var target = items.FirstOrDefault(i => i.Id == quarantineId);
        if (target == null) return false;

        try
        {
            if (target.ItemType == "RegistryKey")
            {
                if (!File.Exists(target.BackupPath)) return false;

                // Import .reg file back into registry
                var psi = new ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = $"import \"{target.BackupPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync(cancellationToken);
                    if (proc.ExitCode == 0)
                    {
                        target.IsRestored = true;
                        await _databaseService.UpdateQuarantineItemAsync(target);
                        return true;
                    }
                }
                return false;
            }

            if (target.ItemType == "Directory")
            {
                if (!Directory.Exists(target.BackupPath)) return false;

                Directory.CreateDirectory(target.OriginalPath);
                var dirInfo = new DirectoryInfo(target.BackupPath);
                foreach (var f in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(target.BackupPath, f.FullName);
                    string destFile = Path.Combine(target.OriginalPath, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                    f.CopyTo(destFile, true);
                }

                // Verify restored folder exists
                if (Directory.Exists(target.OriginalPath))
                {
                    target.IsRestored = true;
                    await _databaseService.UpdateQuarantineItemAsync(target);
                    return true;
                }
                return false;
            }

            if (target.ItemType == "File")
            {
                if (!File.Exists(target.BackupPath)) return false;

                string? dir = Path.GetDirectoryName(target.OriginalPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.Copy(target.BackupPath, target.OriginalPath, true);

                // Verify file exists
                if (File.Exists(target.OriginalPath))
                {
                    target.IsRestored = true;
                    await _databaseService.UpdateQuarantineItemAsync(target);
                    return true;
                }
                return false;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> PermanentlyDeleteItemAsync(string quarantineId, CancellationToken cancellationToken = default)
    {
        try
        {
            string itemDir = Path.Combine(_quarantineBaseDir, quarantineId);
            if (Directory.Exists(itemDir))
            {
                Directory.Delete(itemDir, true);
            }

            await _databaseService.DeleteQuarantineItemAsync(quarantineId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<QuarantineItem>> GetQuarantinedItemsAsync()
    {
        return await _databaseService.GetQuarantineItemsAsync(includeRestored: false);
    }
}
