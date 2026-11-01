using System.IO;
using AppTrace.Core.Models;
using Microsoft.Win32;

namespace AppTrace.Core.Services;

public interface ILeftoverScannerService
{
    Task<List<LeftoverCandidate>> ScanForAppLeftoversAsync(
        InstalledApp targetApp,
        CancellationToken cancellationToken = default);

    Task<List<LeftoverCandidate>> ScanAllOrphanLeftoversAsync(
        IEnumerable<InstalledApp> currentInstalledApps,
        IEnumerable<string> recentlyUninstalledAppNames,
        CancellationToken cancellationToken = default);

    bool IsProtectedPath(string path);
}

public class LeftoverScannerService : ILeftoverScannerService
{
    private static readonly HashSet<string> ProtectedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows", "System32", "SysWOW64", "WinSxS", "Microsoft", "Common Files",
        "Packages", "Package Cache", "WindowsApps", "Windows Defender",
        "Windows Security", "Internet Explorer", "Microsoft Shared", "Reference Assemblies",
        "MSBuild", "dotnet", "Desktop", "Documents", "Downloads", "Pictures", "Music",
        "Videos", "Favorites", "Contacts", "Searches", "Links", "Saved Games",
        "OneDrive", "Google Drive", "iCloudDrive", "Dropbox", "Temp"
    };

    public async Task<List<LeftoverCandidate>> ScanForAppLeftoversAsync(
        InstalledApp targetApp,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var candidates = new List<LeftoverCandidate>();
            string appName = targetApp.DisplayName;
            string publisher = targetApp.Publisher;

            var baseScanDirs = GetStandardAppDirectories();

            foreach (var baseDir in baseScanDirs)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (!Directory.Exists(baseDir)) continue;

                try
                {
                    // Scan direct matches: baseDir\AppName
                    string directDir = Path.Combine(baseDir, CleanName(appName));
                    if (Directory.Exists(directDir) && !IsProtectedPath(directDir))
                    {
                        var candidate = EvaluateDirectoryCandidate(directDir, appName, publisher, isExplicitTarget: true);
                        if (candidate != null) candidates.Add(candidate);
                    }

                    // Scan publisher folders: baseDir\Publisher\AppName
                    if (!string.IsNullOrWhiteSpace(publisher))
                    {
                        string pubDir = Path.Combine(baseDir, CleanName(publisher), CleanName(appName));
                        if (Directory.Exists(pubDir) && !IsProtectedPath(pubDir))
                        {
                            var candidate = EvaluateDirectoryCandidate(pubDir, appName, publisher, isExplicitTarget: true);
                            if (candidate != null) candidates.Add(candidate);
                        }
                    }
                }
                catch { }
            }

            // Scan Registry Leftovers
            ScanAppRegistryLeftovers(appName, publisher, candidates);

            return candidates;
        }, cancellationToken);
    }

    public async Task<List<LeftoverCandidate>> ScanAllOrphanLeftoversAsync(
        IEnumerable<InstalledApp> currentInstalledApps,
        IEnumerable<string> recentlyUninstalledAppNames,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var candidates = new List<LeftoverCandidate>();

            // Build active sets to avoid false positives
            var activeAppNames = new HashSet<string>(
                currentInstalledApps.Select(a => CleanName(a.DisplayName)),
                StringComparer.OrdinalIgnoreCase);

            var activePublishers = new HashSet<string>(
                currentInstalledApps.Where(a => !string.IsNullOrWhiteSpace(a.Publisher)).Select(a => CleanName(a.Publisher)),
                StringComparer.OrdinalIgnoreCase);

            var activeInstallPaths = new HashSet<string>(
                currentInstalledApps
                    .Where(a => !string.IsNullOrWhiteSpace(a.InstallLocation) && Directory.Exists(a.InstallLocation))
                    .Select(a => Path.GetFullPath(a.InstallLocation!).TrimEnd(Path.DirectorySeparatorChar)),
                StringComparer.OrdinalIgnoreCase);

            var recentUninstalled = new HashSet<string>(
                recentlyUninstalledAppNames.Select(CleanName),
                StringComparer.OrdinalIgnoreCase);

            var baseScanDirs = GetStandardAppDirectories();

            foreach (var baseDir in baseScanDirs)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (!Directory.Exists(baseDir)) continue;

                try
                {
                    var subDirs = Directory.GetDirectories(baseDir);
                    foreach (var subDir in subDirs)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        if (IsProtectedPath(subDir)) continue;

                        string folderName = Path.GetFileName(subDir);
                        if (string.IsNullOrWhiteSpace(folderName)) continue;

                        string fullPath = Path.GetFullPath(subDir).TrimEnd(Path.DirectorySeparatorChar);

                        // If directory is active install location, skip
                        if (activeInstallPaths.Contains(fullPath)) continue;

                        // Check if folder name corresponds to an active installed app
                        bool matchesActiveApp = activeAppNames.Any(name =>
                            name.Equals(folderName, StringComparison.OrdinalIgnoreCase) ||
                            (name.Length > 4 && folderName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0));

                        if (matchesActiveApp) continue;

                        // Check if recently uninstalled
                        bool isRecentlyUninstalled = recentUninstalled.Any(u =>
                            u.IndexOf(folderName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            folderName.IndexOf(u, StringComparison.OrdinalIgnoreCase) >= 0);

                        // Evaluate orphan candidate
                        var candidate = EvaluateDirectoryCandidate(
                            subDir,
                            associatedApp: isRecentlyUninstalled ? folderName : $"Orphaned ({folderName})",
                            associatedPub: string.Empty,
                            isExplicitTarget: isRecentlyUninstalled);

                        if (candidate != null)
                        {
                            candidates.Add(candidate);
                        }
                    }
                }
                catch { }
            }

            return candidates;
        }, cancellationToken);
    }

    private LeftoverCandidate? EvaluateDirectoryCandidate(
        string dirPath,
        string associatedApp,
        string associatedPub,
        bool isExplicitTarget)
    {
        try
        {
            if (!Directory.Exists(dirPath) || IsProtectedPath(dirPath))
                return null;

            var dirInfo = new DirectoryInfo(dirPath);
            long sizeBytes = 0;
            int fileCount = 0;
            int exeCount = 0;
            DateTime latestMod = dirInfo.LastWriteTime;

            var files = dirInfo.EnumerateFiles("*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                MaxRecursionDepth = 4
            });

            foreach (var f in files)
            {
                fileCount++;
                sizeBytes += f.Length;
                if (f.LastWriteTime > latestMod) latestMod = f.LastWriteTime;
                if (f.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)) exeCount++;
                if (fileCount > 5000) break; // Performance bound
            }

            // Determine Confidence and Reason
            ConfidenceLevel confidence;
            string reason;
            string recommendedAction;

            if (isExplicitTarget)
            {
                confidence = ConfidenceLevel.High;
                reason = $"Verified remnants from uninstalled application '{associatedApp}'. Contains {fileCount} residual files.";
                recommendedAction = "Safe to move to Recycle Bin / Quarantine.";
            }
            else if (fileCount == 0)
            {
                confidence = ConfidenceLevel.High;
                reason = "Empty residual application directory with no active installed software association.";
                recommendedAction = "Safe to delete empty folder.";
            }
            else if (exeCount == 0 && (dirPath.Contains("AppData", StringComparison.OrdinalIgnoreCase) || dirPath.Contains("ProgramData", StringComparison.OrdinalIgnoreCase)))
            {
                confidence = ConfidenceLevel.Possible;
                reason = $"Orphaned application data cache / configuration folder with no running or installed application association ({fileCount} files).";
                recommendedAction = "Review and send to Recycle Bin.";
            }
            else
            {
                confidence = ConfidenceLevel.Uncertain;
                reason = $"Contains {fileCount} files including executable binaries. May belong to a portable application or shared framework.";
                recommendedAction = "Manual inspection strongly recommended before removal.";
            }

            return new LeftoverCandidate
            {
                Path = dirPath,
                ItemType = CleanupItemType.Directory,
                EstimatedSizeBytes = sizeBytes,
                Confidence = confidence,
                DetectionReason = reason,
                LastModified = latestMod,
                AssociatedAppName = associatedApp,
                AssociatedPublisher = associatedPub,
                RecommendedAction = recommendedAction,
                IsSelected = confidence == ConfidenceLevel.High
            };
        }
        catch
        {
            return null;
        }
    }

    private void ScanAppRegistryLeftovers(string appName, string publisher, List<LeftoverCandidate> candidates)
    {
        string cleanApp = CleanName(appName);
        string[] hives = { "HKCU", "HKLM" };

        foreach (var hiveName in hives)
        {
            try
            {
                var baseKey = hiveName == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
                using var swKey = baseKey.OpenSubKey(@"Software");
                if (swKey == null) continue;

                foreach (var sub in swKey.GetSubKeyNames())
                {
                    if (ProtectedDirectoryNames.Contains(sub)) continue;

                    if (sub.Equals(cleanApp, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(publisher) && sub.Equals(CleanName(publisher), StringComparison.OrdinalIgnoreCase)))
                    {
                        candidates.Add(new LeftoverCandidate
                        {
                            Path = $@"{hiveName}\Software\{sub}",
                            ItemType = CleanupItemType.RegistryKey,
                            EstimatedSizeBytes = 0,
                            Confidence = ConfidenceLevel.High,
                            DetectionReason = $"Residual registry configuration key under {hiveName}\\Software\\{sub}",
                            AssociatedAppName = appName,
                            AssociatedPublisher = publisher,
                            RecommendedAction = "Export .reg backup and remove key.",
                            IsSelected = true,
                            RegistryRoot = hiveName,
                            RegistrySubKey = $@"Software\{sub}"
                        });
                    }
                }
            }
            catch { }
        }
    }

    public bool IsProtectedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;

        try
        {
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            string? root = Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar);

            // Drive root (e.g. C:\)
            if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase)) return true;

            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd(Path.DirectorySeparatorChar);
            string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System).TrimEnd(Path.DirectorySeparatorChar);
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd(Path.DirectorySeparatorChar);
            string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd(Path.DirectorySeparatorChar);
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).TrimEnd(Path.DirectorySeparatorChar);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).TrimEnd(Path.DirectorySeparatorChar);
            string commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData).TrimEnd(Path.DirectorySeparatorChar);

            // Prohibit roots themselves
            if (string.Equals(full, windows, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(full, system32, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(full, progFiles, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(full, progFilesX86, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(full, userProfile, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(full, appData, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(full, localAppData, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(full, commonAppData, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Prohibit anything inside Windows directory
            if (full.StartsWith(windows, StringComparison.OrdinalIgnoreCase))
                return true;

            // Prohibit protected folder names
            string folderName = Path.GetFileName(full);
            if (ProtectedDirectoryNames.Contains(folderName))
                return true;

            return false;
        }
        catch
        {
            return true;
        }
    }

    private static List<string> GetStandardAppDirectories()
    {
        return new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };
    }

    private static string CleanName(string name)
    {
        return Path.GetInvalidFileNameChars().Aggregate(name, (current, c) => current.Replace(c, '_')).Trim();
    }
}
