using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using AppTrace.Core.Models;
using Microsoft.Win32;

namespace AppTrace.Core.Services;

public interface IStartupManagerService
{
    Task<List<StartupEntry>> GetStartupEntriesAsync(CancellationToken cancellationToken = default);
    Task<bool> ToggleStartupEntryStateAsync(StartupEntry entry, bool enable, CancellationToken cancellationToken = default);
}

public class StartupManagerService : IStartupManagerService
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedSubKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public async Task<List<StartupEntry>> GetStartupEntriesAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var entries = new List<StartupEntry>();

            // 1. HKCU Run
            ScanRegistryRunKey(Registry.CurrentUser, RunSubKey, StartupSource.HKCU_Run, entries);

            // 2. HKLM Run
            ScanRegistryRunKey(Registry.LocalMachine, RunSubKey, StartupSource.HKLM_Run, entries);

            // 3. HKLM WOW6432Node Run (on 64-bit Windows)
            if (Environment.Is64BitOperatingSystem)
            {
                using var hklm32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                ScanRegistryKeyDirect(hklm32, RunSubKey, StartupSource.HKLM_WOW64_Run, entries);
            }

            // 4. Startup Folders
            ScanStartupFolder(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                StartupSource.UserStartupFolder,
                entries);

            ScanStartupFolder(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                StartupSource.CommonStartupFolder,
                entries);

            return entries;
        }, cancellationToken);
    }

    public async Task<bool> ToggleStartupEntryStateAsync(
        StartupEntry entry,
        bool enable,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (entry.Source == StartupSource.HKCU_Run)
                {
                    return SetStartupApprovedState(Registry.CurrentUser, entry.Name, enable);
                }
                else if (entry.Source == StartupSource.HKLM_Run || entry.Source == StartupSource.HKLM_WOW64_Run)
                {
                    return SetStartupApprovedState(Registry.LocalMachine, entry.Name, enable);
                }
                else if (entry.Source == StartupSource.UserStartupFolder || entry.Source == StartupSource.CommonStartupFolder)
                {
                    // For startup folder shortcuts: rename with .disabled extension or restore
                    string path = entry.RawCommand;
                    if (enable && path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                    {
                        string target = path.Substring(0, path.Length - ".disabled".Length);
                        File.Move(path, target, true);
                        entry.IsEnabled = true;
                        entry.RawCommand = target;
                        entry.StateExplanation = "Active in Startup Folder";
                        return true;
                    }
                    else if (!enable && !path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                    {
                        string target = path + ".disabled";
                        File.Move(path, target, true);
                        entry.IsEnabled = false;
                        entry.RawCommand = target;
                        entry.StateExplanation = "Disabled (.disabled file)";
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    private static void ScanRegistryRunKey(RegistryKey rootKey, string subKeyPath, StartupSource source, List<StartupEntry> entries)
    {
        try
        {
            using var runKey = rootKey.OpenSubKey(subKeyPath);
            if (runKey == null) return;

            ScanRegistryKeyDirect(runKey, subKeyPath, source, entries);
        }
        catch { }
    }

    private static void ScanRegistryKeyDirect(RegistryKey baseKey, string subKeyPath, StartupSource source, List<StartupEntry> entries)
    {
        try
        {
            using var key = baseKey.Name.Contains('\\') ? baseKey : baseKey.OpenSubKey(subKeyPath);
            if (key == null) return;

            string[] valueNames = key.GetValueNames();
            foreach (var name in valueNames)
            {
                try
                {
                    string? rawCommand = key.GetValue(name) as string;
                    if (string.IsNullOrWhiteSpace(rawCommand)) continue;

                    string exePath = ExtractExePath(rawCommand);
                    bool fileExists = File.Exists(exePath);
                    long fileSize = 0;
                    string publisher = "Unknown Publisher";

                    if (fileExists)
                    {
                        try
                        {
                            fileSize = new FileInfo(exePath).Length;
                            var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
                            if (!string.IsNullOrWhiteSpace(versionInfo.CompanyName))
                            {
                                publisher = versionInfo.CompanyName;
                            }
                        }
                        catch { }
                    }

                    // Check StartupApproved status
                    bool isEnabled = CheckStartupApproved(baseKey, name);

                    entries.Add(new StartupEntry
                    {
                        Name = name,
                        Publisher = publisher,
                        ExecutablePath = exePath,
                        RawCommand = rawCommand,
                        Source = source,
                        IsEnabled = isEnabled,
                        StateExplanation = isEnabled ? "Enabled in Windows Startup" : "Disabled by User",
                        IsFileExisting = fileExists,
                        FileSizeBytes = fileSize
                    });
                }
                catch { }
            }
        }
        catch { }
    }

    private static void ScanStartupFolder(string folderPath, StartupSource source, List<StartupEntry> entries)
    {
        if (!Directory.Exists(folderPath)) return;

        try
        {
            var files = Directory.GetFiles(folderPath);
            foreach (var file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                bool isFileDisabled = file.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);

                string exePath = file;
                string publisher = "Local User Entry";
                long fileSize = 0;

                try
                {
                    var fi = new FileInfo(file);
                    fileSize = fi.Length;
                }
                catch { }

                entries.Add(new StartupEntry
                {
                    Name = name,
                    Publisher = publisher,
                    ExecutablePath = exePath,
                    RawCommand = file,
                    Source = source,
                    IsEnabled = !isFileDisabled,
                    StateExplanation = isFileDisabled ? "Disabled in Startup folder" : "Active in Startup folder",
                    IsFileExisting = true,
                    FileSizeBytes = fileSize
                });
            }
        }
        catch { }
    }

    private static bool CheckStartupApproved(RegistryKey baseKey, string valueName)
    {
        try
        {
            // Open HKLM or HKCU StartupApproved\Run
            var root = baseKey.Name.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase)
                ? Registry.CurrentUser
                : Registry.LocalMachine;

            using var approvedKey = root.OpenSubKey(StartupApprovedSubKey);
            if (approvedKey == null) return true; // Default enabled

            var val = approvedKey.GetValue(valueName);
            if (val is byte[] bytes && bytes.Length > 0)
            {
                // In Windows StartupApproved: 0x02 = enabled, 0x03 = disabled, 0x01 = disabled
                return bytes[0] % 2 == 0;
            }
            return true;
        }
        catch
        {
            return true;
        }
    }

    private static bool SetStartupApprovedState(RegistryKey rootKey, string valueName, bool enable)
    {
        try
        {
            using var approvedKey = rootKey.CreateSubKey(StartupApprovedSubKey, writable: true);
            if (approvedKey == null) return false;

            byte[] bytes;
            var existing = approvedKey.GetValue(valueName) as byte[];
            if (existing != null && existing.Length >= 8)
            {
                bytes = (byte[])existing.Clone();
                bytes[0] = enable ? (byte)0x02 : (byte)0x03;
            }
            else
            {
                bytes = new byte[12];
                bytes[0] = enable ? (byte)0x02 : (byte)0x03;
            }

            approvedKey.SetValue(valueName, bytes, RegistryValueKind.Binary);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ExtractExePath(string commandLine)
    {
        commandLine = commandLine.Trim();
        if (commandLine.StartsWith("\""))
        {
            int nextQuote = commandLine.IndexOf('\"', 1);
            if (nextQuote > 0)
            {
                return commandLine.Substring(1, nextQuote - 1).Trim();
            }
        }

        int spaceIdx = commandLine.IndexOf(' ');
        if (spaceIdx > 0)
        {
            string candidate = commandLine.Substring(0, spaceIdx);
            if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return commandLine;
    }
}
