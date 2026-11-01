using System.Collections.Concurrent;
using System.IO;
using System.Security;
using System.Text.RegularExpressions;
using System.Windows.Media;
using AppTrace.Core.Interop;
using AppTrace.Core.Models;
using Microsoft.Win32;

namespace AppTrace.Core.Services;

public interface IAppDetectionService
{
    Task<List<InstalledApp>> DetectInstalledApplicationsAsync(CancellationToken cancellationToken = default);
    Task<long> CalculateDirectorySizeBytesAsync(string directoryPath, CancellationToken cancellationToken = default);
    List<string> FindRelatedDirectories(string appName, string? publisher, string? installLocation);
}

public class AppDetectionService : IAppDetectionService
{
    private static readonly Regex KbUpdateRegex = new(@"\bKB\d{6,}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly ConcurrentDictionary<string, ImageSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? GetOrCreateIcon(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            return _iconCache.GetOrAdd(path, p => ShellNativeMethods.ExtractIconFromPath(p));
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<InstalledApp>> DetectInstalledApplicationsAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var appsMap = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);

            // 1. Scan 64-bit HKLM
            ScanRegistryUninstall(
                RegistryHive.LocalMachine,
                RegistryView.Registry64,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                InstallSource.Registry64,
                appsMap);

            // 2. Scan 32-bit HKLM (on 64-bit systems)
            if (Environment.Is64BitOperatingSystem)
            {
                ScanRegistryUninstall(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry32,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    InstallSource.Registry32,
                    appsMap);
            }

            // 3. Scan HKCU (Current User)
            ScanRegistryUninstall(
                RegistryHive.CurrentUser,
                RegistryView.Default,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                InstallSource.UserRegistry,
                appsMap);

            // 4. Scan Microsoft Store / Appx Packages
            ScanStoreApplications(appsMap);

            // 5. Post-process apps: find related directories and categories in parallel
            var result = appsMap.Values.OrderBy(a => a.DisplayName).ToList();
            Parallel.ForEach(result, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = cancellationToken }, app =>
            {
                app.Category = CategorizeApp(app.DisplayName, app.Publisher);
                app.RelatedDirectories = FindRelatedDirectories(app.DisplayName, app.Publisher, app.InstallLocation);
            });

            return result;
        }, cancellationToken);
    }

    private void ScanRegistryUninstall(
        RegistryHive hive,
        RegistryView view,
        string subKeyPath,
        InstallSource source,
        Dictionary<string, InstalledApp> appsMap)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstallKey = baseKey.OpenSubKey(subKeyPath);
            if (uninstallKey == null) return;

            string[] subKeyNames = uninstallKey.GetSubKeyNames();
            foreach (string name in subKeyNames)
            {
                try
                {
                    using var appKey = uninstallKey.OpenSubKey(name);
                    if (appKey == null) continue;

                    string? displayName = appKey.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName)) continue;
                    displayName = displayName.Trim();

                    // Filter out Windows Updates (e.g. Security Update for Windows (KB500123))
                    if (KbUpdateRegex.IsMatch(displayName) && displayName.Contains("Update", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Filter out components explicitly marked as SystemComponent = 1
                    var systemComponent = appKey.GetValue("SystemComponent");
                    if (systemComponent is int sysCompInt && sysCompInt == 1)
                        continue;

                    // Filter out child patches/updates that have a ParentKeyName
                    string? parentKeyName = appKey.GetValue("ParentKeyName") as string;
                    if (!string.IsNullOrWhiteSpace(parentKeyName))
                        continue;

                    // Check if it's a Windows internal component
                    string? releaseType = appKey.GetValue("ReleaseType") as string;
                    if (string.Equals(releaseType, "Security Update", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(releaseType, "Update", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(releaseType, "Hotfix", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string publisher = (appKey.GetValue("Publisher") as string)?.Trim() ?? string.Empty;
                    string displayVersion = (appKey.GetValue("DisplayVersion") as string)?.Trim() ?? string.Empty;
                    string? installLocation = (appKey.GetValue("InstallLocation") as string)?.Trim();
                    string? uninstallString = (appKey.GetValue("UninstallString") as string)?.Trim();
                    string? quietUninstallString = (appKey.GetValue("QuietUninstallString") as string)?.Trim();
                    string? displayIcon = (appKey.GetValue("DisplayIcon") as string)?.Trim();
                    string? helpLink = (appKey.GetValue("HelpLink") as string)?.Trim();
                    string? urlInfo = (appKey.GetValue("URLInfoAbout") as string)?.Trim();
                    string? comments = (appKey.GetValue("Comments") as string)?.Trim();
                    string? installDateStr = (appKey.GetValue("InstallDate") as string)?.Trim();

                    DateTime? installDate = ParseInstallDate(installDateStr);

                    // Estimate or verify size
                    long sizeBytes = 0;
                    SizeConfidence sizeConfidence = SizeConfidence.Unknown;

                    var estimatedSize = appKey.GetValue("EstimatedSize");
                    if (estimatedSize is int estInt && estInt > 0)
                    {
                        sizeBytes = (long)estInt * 1024L;
                        sizeConfidence = SizeConfidence.Estimated;
                    }
                    else if (estimatedSize is long estLong && estLong > 0)
                    {
                        sizeBytes = estLong * 1024L;
                        sizeConfidence = SizeConfidence.Estimated;
                    }

                    // Fast check for install location if size is unknown
                    if (sizeBytes == 0 && !string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
                    {
                        if (IsValidApplicationDirectory(installLocation))
                        {
                            long measured = QuickCalculateFolderSizeBytes(installLocation, maxDepth: 2);
                            if (measured > 0)
                            {
                                sizeBytes = measured;
                                sizeConfidence = SizeConfidence.Verified;
                            }
                        }
                    }

                    string? cleanIcon = ExtractCleanIconPath(displayIcon, installLocation);
                    ImageSource? iconSource = !string.IsNullOrWhiteSpace(cleanIcon) ? GetOrCreateIcon(cleanIcon) : null;

                    string deduplicationKey = $"{displayName}___{publisher}".ToLowerInvariant();

                    // If already exists, prefer 64-bit or entry with more metadata (e.g. uninstaller or size)
                    if (appsMap.TryGetValue(deduplicationKey, out var existing))
                    {
                        if (existing.InstallSource == InstallSource.Registry32 && source == InstallSource.Registry64)
                        {
                            existing.InstallSource = InstallSource.Registry64;
                            if (sizeBytes > 0) existing.EstimatedSizeBytes = sizeBytes;
                            if (!string.IsNullOrEmpty(uninstallString)) existing.UninstallString = uninstallString;
                            if (iconSource != null) existing.IconSource = iconSource;
                        }
                        continue;
                    }

                    var app = new InstalledApp
                    {
                        Id = $"{hive}_{name}".GetHashCode().ToString("X8"),
                        DisplayName = displayName,
                        Publisher = publisher,
                        DisplayVersion = displayVersion,
                        InstallDate = installDate,
                        InstallLocation = installLocation,
                        EstimatedSizeBytes = sizeBytes,
                        SizeConfidence = sizeConfidence,
                        UninstallString = uninstallString,
                        QuietUninstallString = quietUninstallString,
                        InstallSource = source,
                        IconPath = cleanIcon,
                        IconSource = iconSource,
                        HelpLink = helpLink,
                        UrlInfoAbout = urlInfo,
                        Comments = comments,
                        RegistryKeyPath = $@"{hive}\{subKeyPath}\{name}",
                        IsWindowsComponent = false,
                        IsSystemProtected = IsProtectedComponent(displayName, publisher)
                    };

                    appsMap[deduplicationKey] = app;
                }
                catch
                {
                    // Skip inaccessible or corrupted registry subkey
                }
            }
        }
        catch
        {
            // Skip hive error
        }
    }

    private void ScanStoreApplications(Dictionary<string, InstalledApp> appsMap)
    {
        try
        {
            string appModelPath = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
            using var packagesKey = Registry.CurrentUser.OpenSubKey(appModelPath);
            if (packagesKey == null) return;

            foreach (string pkgName in packagesKey.GetSubKeyNames())
            {
                try
                {
                    using var pkg = packagesKey.OpenSubKey(pkgName);
                    if (pkg == null) continue;

                    string? displayName = pkg.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName) || displayName.StartsWith("@{") || displayName.StartsWith("ms-resource:"))
                    {
                        displayName = CleanStorePackageName(pkgName);
                    }

                    if (string.IsNullOrWhiteSpace(displayName) || IsInternalStoreFramework(pkgName))
                        continue;

                    // Filter out raw GUID store package IDs
                    if (Guid.TryParse(displayName, out _) || displayName.Length < 2)
                        continue;

                    string publisher = (pkg.GetValue("PublisherDisplayName") as string) ??
                                       (pkg.GetValue("Publisher") as string) ?? "Microsoft Store";
                    if (publisher.StartsWith("@{") || publisher.StartsWith("ms-resource:"))
                        publisher = "Microsoft Store";

                    string deduplicationKey = $"{displayName}___{publisher}".ToLowerInvariant();
                    if (appsMap.ContainsKey(deduplicationKey)) continue;

                    string? packageRootFolder = pkg.GetValue("PackageRootFolder") as string;
                    long sizeBytes = 0;
                    SizeConfidence sizeConf = SizeConfidence.Unknown;
                    string? storeIconPath = null;

                    if (!string.IsNullOrWhiteSpace(packageRootFolder) && Directory.Exists(packageRootFolder))
                    {
                        sizeBytes = QuickCalculateFolderSizeBytes(packageRootFolder, maxDepth: 2);
                        if (sizeBytes > 0) sizeConf = SizeConfidence.Verified;

                        try
                        {
                            var enumOptions = new EnumerationOptions { MaxRecursionDepth = 2, IgnoreInaccessible = true };
                            storeIconPath = Directory.EnumerateFiles(packageRootFolder, "*logo*.png", enumOptions).FirstOrDefault() ??
                                            Directory.EnumerateFiles(packageRootFolder, "*.ico", enumOptions).FirstOrDefault();
                        }
                        catch { }
                    }

                    ImageSource? iconSource = !string.IsNullOrWhiteSpace(storeIconPath) ? GetOrCreateIcon(storeIconPath) : null;

                    var app = new InstalledApp
                    {
                        Id = $"Store_{pkgName}".GetHashCode().ToString("X8"),
                        DisplayName = displayName,
                        Publisher = publisher,
                        DisplayVersion = ExtractVersionFromPackageName(pkgName),
                        InstallLocation = packageRootFolder,
                        EstimatedSizeBytes = sizeBytes,
                        SizeConfidence = sizeConf,
                        InstallSource = InstallSource.MicrosoftStore,
                        IconPath = storeIconPath,
                        IconSource = iconSource,
                        RegistryKeyPath = $@"HKCU\{appModelPath}\{pkgName}",
                        IsSystemProtected = IsProtectedComponent(displayName, publisher)
                    };

                    appsMap[deduplicationKey] = app;
                }
                catch
                {
                    // Skip
                }
            }
        }
        catch
        {
            // Store scan fallback
        }
    }

    private static bool IsInternalStoreFramework(string pkgName)
    {
        return pkgName.StartsWith("Microsoft.NET.", StringComparison.OrdinalIgnoreCase) ||
               pkgName.StartsWith("Microsoft.VCLibs", StringComparison.OrdinalIgnoreCase) ||
               pkgName.StartsWith("Microsoft.UI.Xaml", StringComparison.OrdinalIgnoreCase) ||
               pkgName.StartsWith("Microsoft.WindowsDesktop", StringComparison.OrdinalIgnoreCase) ||
               pkgName.StartsWith("Microsoft.DirectX", StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanStorePackageName(string pkgName)
    {
        int underscoreIdx = pkgName.IndexOf('_');
        if (underscoreIdx > 0)
        {
            string shortName = pkgName.Substring(0, underscoreIdx);
            return shortName.Replace('.', ' ');
        }
        return pkgName;
    }

    private static string ExtractVersionFromPackageName(string pkgName)
    {
        var match = Regex.Match(pkgName, @"_(\d+\.\d+\.\d+\.\d+)_");
        return match.Success ? match.Groups[1].Value : "1.0.0.0";
    }

    private static DateTime? ParseInstallDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;

        if (dateStr.Length == 8 && int.TryParse(dateStr, out _))
        {
            if (int.TryParse(dateStr.Substring(0, 4), out int year) &&
                int.TryParse(dateStr.Substring(4, 2), out int month) &&
                int.TryParse(dateStr.Substring(6, 2), out int day))
            {
                try
                {
                    return new DateTime(year, month, day);
                }
                catch { }
            }
        }

        if (DateTime.TryParse(dateStr, out var parsed))
            return parsed;

        return null;
    }

    private static string? ExtractCleanIconPath(string? displayIcon, string? installLocation)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
        {
            if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
            {
                try
                {
                    var exe = Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault();
                    if (exe != null) return exe;
                }
                catch { }
            }
            return null;
        }

        string clean = displayIcon.Trim('\"');
        int commaIndex = clean.IndexOf(',');
        if (commaIndex > 0)
        {
            clean = clean.Substring(0, commaIndex).Trim('\"');
        }

        if (File.Exists(clean)) return clean;

        if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
        {
            try
            {
                var exe = Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (exe != null) return exe;
            }
            catch { }
        }

        return null;
    }

    private static bool IsValidApplicationDirectory(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            var root = Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar);
            if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)) return false;

            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd(Path.DirectorySeparatorChar);
            string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd(Path.DirectorySeparatorChar);
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd(Path.DirectorySeparatorChar);

            if (string.Equals(fullPath, progFiles, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fullPath, progFilesX86, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fullPath, windows, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<long> CalculateDirectorySizeBytesAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(directoryPath)) return 0L;
                long totalSize = 0;
                var dirInfo = new DirectoryInfo(directoryPath);

                var files = dirInfo.EnumerateFiles("*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    MaxRecursionDepth = 6
                });

                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    totalSize += file.Length;
                }

                return totalSize;
            }
            catch
            {
                return 0L;
            }
        }, cancellationToken);
    }

    private static long QuickCalculateFolderSizeBytes(string directoryPath, int maxDepth = 2)
    {
        try
        {
            long size = 0;
            var dir = new DirectoryInfo(directoryPath);
            var files = dir.EnumerateFiles("*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                MaxRecursionDepth = maxDepth
            });

            int count = 0;
            foreach (var f in files)
            {
                size += f.Length;
                if (++count > 5000) break; // Fast performance bound for instant startup
            }
            return size;
        }
        catch
        {
            return 0L;
        }
    }

    public List<string> FindRelatedDirectories(string appName, string? publisher, string? installLocation)
    {
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
        {
            list.Add(installLocation);
        }

        string cleanName = SanitizeForFolderName(appName);
        string? cleanPub = !string.IsNullOrWhiteSpace(publisher) ? SanitizeForFolderName(publisher) : null;

        var baseFolders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow")
        };

        foreach (var baseFolder in baseFolders)
        {
            if (!Directory.Exists(baseFolder)) continue;

            try
            {
                string appDir = Path.Combine(baseFolder, cleanName);
                if (Directory.Exists(appDir) && !list.Contains(appDir, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(appDir);
                }

                if (!string.IsNullOrEmpty(cleanPub))
                {
                    string pubAppDir = Path.Combine(baseFolder, cleanPub, cleanName);
                    if (Directory.Exists(pubAppDir) && !list.Contains(pubAppDir, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(pubAppDir);
                    }
                }
            }
            catch { }
        }

        return list;
    }

    private static string SanitizeForFolderName(string name)
    {
        string clean = Regex.Replace(name, @"\((?:64-bit|32-bit|x86|x64)\)", "", RegexOptions.IgnoreCase).Trim();
        char[] invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(clean.Select(c => invalidChars.Contains(c) ? '_' : c)).Trim();
    }

    private static bool IsProtectedComponent(string displayName, string publisher)
    {
        string lowerName = displayName.ToLowerInvariant();
        string lowerPub = publisher.ToLowerInvariant();

        if (lowerPub.Contains("microsoft") && (
            lowerName.Contains("windows") ||
            lowerName.Contains("edge webview2") ||
            lowerName.Contains("directx") ||
            lowerName.Contains("visual c++") ||
            lowerName.Contains(".net framework") ||
            lowerName.Contains("subsystem")))
        {
            return true;
        }

        if (lowerName.Contains("realtek") || lowerName.Contains("intel(r)") || lowerName.Contains("nvidia graphics driver"))
        {
            return true;
        }

        return false;
    }

    public static AppCategory CategorizeApp(string displayName, string publisher)
    {
        string text = $"{displayName} {publisher}".ToLowerInvariant();

        if (text.Contains("visual studio") || text.Contains("vscode") || text.Contains("code") ||
            text.Contains("git") || text.Contains("sdk") || text.Contains("python") ||
            text.Contains("node") || text.Contains("compiler") || text.Contains("rider") ||
            text.Contains("intellij") || text.Contains("docker") || text.Contains("terminal") ||
            text.Contains("postman") || text.Contains("sublime"))
        {
            return AppCategory.Development;
        }

        if (text.Contains("office") || text.Contains("word") || text.Contains("excel") ||
            text.Contains("powerpoint") || text.Contains("onenote") || text.Contains("acrobat") ||
            text.Contains("pdf") || text.Contains("teams") || text.Contains("slack") ||
            text.Contains("zoom") || text.Contains("notion") || text.Contains("notes"))
        {
            return AppCategory.Productivity;
        }

        if (text.Contains("chrome") || text.Contains("firefox") || text.Contains("edge") ||
            text.Contains("browser") || text.Contains("brave") || text.Contains("opera") ||
            text.Contains("thunderbird") || text.Contains("discord") || text.Contains("telegram"))
        {
            return AppCategory.Internet;
        }

        if (text.Contains("spotify") || text.Contains("vlc") || text.Contains("media") ||
            text.Contains("player") || text.Contains("photoshop") || text.Contains("gimp") ||
            text.Contains("blender") || text.Contains("audacity") || text.Contains("obs") ||
            text.Contains("premiere") || text.Contains("music") || text.Contains("video"))
        {
            return AppCategory.Multimedia;
        }

        if (text.Contains("steam") || text.Contains("epic") || text.Contains("game") ||
            text.Contains("riot") || text.Contains("minecraft") || text.Contains("gog") ||
            text.Contains("ubisoft") || text.Contains("battle.net"))
        {
            return AppCategory.Games;
        }

        if (text.Contains("zip") || text.Contains("rar") || text.Contains("powertoys") ||
            text.Contains("everything") || text.Contains("cleaner") || text.Contains("treesize") ||
            text.Contains("benchmark") || text.Contains("driver") || text.Contains("util") ||
            text.Contains("tool"))
        {
            return AppCategory.Utilities;
        }

        if (text.Contains("windows") || text.Contains("microsoft corporation") || text.Contains("system"))
        {
            return AppCategory.System;
        }

        return AppCategory.Other;
    }
}
