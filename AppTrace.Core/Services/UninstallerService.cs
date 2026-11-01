using System.Diagnostics;
using System.Text.RegularExpressions;
using AppTrace.Core.Models;

namespace AppTrace.Core.Services;

public class UninstallPlan
{
    public string AppName { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string UninstallType { get; set; } = "Standard Uninstaller";
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public bool RequiresElevation { get; set; } = false;
    public bool IsStoreApp { get; set; } = false;
    public string? StorePackageName { get; set; }
    public bool CanExecute { get; set; } = true;
    public string? SafetyWarning { get; set; }
}

public class UninstallResult
{
    public bool Success { get; set; }
    public int? ExitCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
}

public interface IUninstallerService
{
    Task<UninstallPlan> PrepareUninstallAsync(InstalledApp app);
    Task<UninstallResult> ExecuteUninstallAsync(UninstallPlan plan, CancellationToken cancellationToken = default);
}

public class UninstallerService : IUninstallerService
{
    public Task<UninstallPlan> PrepareUninstallAsync(InstalledApp app)
    {
        return Task.Run(() =>
        {
            var plan = new UninstallPlan
            {
                AppName = app.DisplayName,
                Publisher = app.Publisher
            };

            if (app.IsSystemProtected)
            {
                plan.CanExecute = false;
                plan.SafetyWarning = "This application is marked as an essential Windows component or critical system driver. Removal is blocked for system safety.";
                return plan;
            }

            if (app.InstallSource == InstallSource.MicrosoftStore)
            {
                plan.IsStoreApp = true;
                plan.UninstallType = "Microsoft Store App (MSIX / Appx)";
                plan.ExecutablePath = "powershell.exe";
                plan.StorePackageName = app.DisplayName;
                plan.Arguments = $"-NoProfile -Command \"Get-AppxPackage -Name *{app.DisplayName.Replace(" ", "*")}* | Remove-AppxPackage\"";
                return plan;
            }

            string? cmd = !string.IsNullOrWhiteSpace(app.UninstallString)
                ? app.UninstallString
                : app.QuietUninstallString;

            if (string.IsNullOrWhiteSpace(cmd))
            {
                plan.CanExecute = false;
                plan.SafetyWarning = "No registered uninstall command was found for this application.";
                return plan;
            }

            cmd = cmd.Trim();

            // Check for MSI
            if (cmd.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
            {
                plan.UninstallType = "Windows Installer (MSI)";
                plan.ExecutablePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe");

                // Extract GUID
                var match = Regex.Match(cmd, @"(\{[A-Fa-f0-9\-]+\})");
                if (match.Success)
                {
                    // Use standard interactive removal (/x)
                    plan.Arguments = $"/x {match.Groups[1].Value}";
                }
                else
                {
                    // Clean msiexec flags
                    plan.Arguments = Regex.Replace(cmd, @"^.*?msiexec(?:\.exe)?\s*", "", RegexOptions.IgnoreCase);
                }
                return plan;
            }

            // Standard Executable uninstaller
            ParseExecutableAndArguments(cmd, out string exePath, out string args);
            plan.ExecutablePath = exePath;
            plan.Arguments = args;
            plan.UninstallType = "Official Application Uninstaller";

            if (!File.Exists(exePath))
            {
                // In case executable path is without extension or in system path
                string fullPath = FindInPath(exePath);
                if (File.Exists(fullPath))
                {
                    plan.ExecutablePath = fullPath;
                }
                else
                {
                    plan.CanExecute = false;
                    plan.SafetyWarning = $"Uninstaller executable was not found on disk: {exePath}";
                }
            }

            return plan;
        });
    }

    public async Task<UninstallResult> ExecuteUninstallAsync(UninstallPlan plan, CancellationToken cancellationToken = default)
    {
        if (!plan.CanExecute)
        {
            return new UninstallResult
            {
                Success = false,
                Message = plan.SafetyWarning ?? "Cannot execute uninstallation."
            };
        }

        var sw = Stopwatch.StartNew();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = plan.ExecutablePath,
                Arguments = plan.Arguments,
                UseShellExecute = true,
                CreateNoWindow = false,
                WindowStyle = ProcessWindowStyle.Normal
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new UninstallResult
                {
                    Success = false,
                    Message = "Failed to launch the official uninstaller process."
                };
            }

            await process.WaitForExitAsync(cancellationToken);
            sw.Stop();

            int exitCode = process.ExitCode;

            // In Windows Installer, exit code 0 is Success, 3010 is Restart Required, 1602 is User Cancelled
            if (exitCode == 0 || exitCode == 3010)
            {
                return new UninstallResult
                {
                    Success = true,
                    ExitCode = exitCode,
                    Duration = sw.Elapsed,
                    Message = exitCode == 3010
                        ? "Uninstallation succeeded. A system restart may be required to complete file cleanup."
                        : "Official uninstallation finished successfully."
                };
            }

            if (exitCode == 1602)
            {
                return new UninstallResult
                {
                    Success = false,
                    ExitCode = exitCode,
                    Duration = sw.Elapsed,
                    Message = "Uninstallation was cancelled by user."
                };
            }

            return new UninstallResult
            {
                Success = false,
                ExitCode = exitCode,
                Duration = sw.Elapsed,
                Message = $"Uninstaller finished with non-zero exit code: {exitCode}"
            };
        }
        catch (OperationCanceledException)
        {
            return new UninstallResult
            {
                Success = false,
                Message = "Uninstallation monitoring was cancelled."
            };
        }
        catch (Exception ex)
        {
            return new UninstallResult
            {
                Success = false,
                Message = $"An error occurred while launching uninstaller: {ex.Message}"
            };
        }
    }

    private static void ParseExecutableAndArguments(string commandLine, out string executable, out string arguments)
    {
        commandLine = commandLine.Trim();
        if (commandLine.StartsWith("\""))
        {
            int closingQuote = commandLine.IndexOf('\"', 1);
            if (closingQuote > 0)
            {
                executable = commandLine.Substring(1, closingQuote - 1).Trim();
                arguments = commandLine.Substring(closingQuote + 1).Trim();
                return;
            }
        }

        int firstSpace = commandLine.IndexOf(' ');
        if (firstSpace > 0)
        {
            string candidateExe = commandLine.Substring(0, firstSpace);
            if (candidateExe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                executable = candidateExe;
                arguments = commandLine.Substring(firstSpace + 1).Trim();
                return;
            }
        }

        executable = commandLine;
        arguments = string.Empty;
    }

    private static string FindInPath(string filename)
    {
        if (File.Exists(filename)) return Path.GetFullPath(filename);

        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return filename;

        foreach (string folder in pathEnv.Split(';'))
        {
            try
            {
                string combined = Path.Combine(folder.Trim(), filename);
                if (File.Exists(combined)) return combined;
                if (File.Exists(combined + ".exe")) return combined + ".exe";
            }
            catch { }
        }

        return filename;
    }
}
