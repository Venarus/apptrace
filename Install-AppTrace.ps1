<#
.SYNOPSIS
    AppTrace — Smart Application Manager Installer for Windows 11
.DESCRIPTION
    Installs AppTrace to %LocalAppData%\Programs\AppTrace, creates Start Menu and Desktop shortcuts,
    and registers AppTrace in the Windows Uninstall registry.
#>

[CmdletBinding()]
param(
    [string]$InstallPath = "$env:LocalAppData\Programs\AppTrace"
)

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       AppTrace — Smart Application Manager Installer    " -ForegroundColor White
Write-Host "                     Windows 11 Edition                  " -ForegroundColor Gray
Write-Host "==========================================================" -ForegroundColor Cyan

$sourceDir = Join-Path $PSScriptRoot "publish"
if (-not (Test-Path $sourceDir)) {
    $sourceDir = $PSScriptRoot
}

$exeSource = Join-Path $sourceDir "AppTrace.UI.exe"
if (-not (Test-Path $exeSource)) {
    Write-Error "AppTrace.UI.exe not found in $sourceDir. Please ensure publish folder is intact."
    exit 1
}

Write-Host "[1/4] Preparing destination folder: $InstallPath..." -ForegroundColor Yellow
if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

Write-Host "[2/4] Copying application files..." -ForegroundColor Yellow
Copy-Item -Path "$sourceDir\*" -Destination $InstallPath -Recurse -Force

# Copy uninstaller script to installation dir
$uninstallerScript = Join-Path $PSScriptRoot "Uninstall-AppTrace.ps1"
if (Test-Path $uninstallerScript) {
    Copy-Item -Path $uninstallerScript -Destination $InstallPath -Force
}

Write-Host "[3/4] Creating Start Menu & Desktop Shortcuts..." -ForegroundColor Yellow
$wshShell = New-Object -ComObject WScript.Shell

# Start Menu Shortcut
$startMenuDir = "$env:AppData\Microsoft\Windows\Start Menu\Programs\AppTrace"
if (-not (Test-Path $startMenuDir)) {
    New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null
}
$targetExe = Join-Path $InstallPath "AppTrace.UI.exe"
$iconPath = Join-Path $InstallPath "app.ico"

$startShortcut = $wshShell.CreateShortcut("$startMenuDir\AppTrace.lnk")
$startShortcut.TargetPath = $targetExe
$startShortcut.WorkingDirectory = $InstallPath
$startShortcut.Description = "AppTrace — Smart Application Manager for Windows 11"
if (Test-Path $iconPath) { $startShortcut.IconLocation = "$iconPath,0" }
$startShortcut.Save()

# Desktop Shortcut
$desktopShortcut = $wshShell.CreateShortcut("$env:UserProfile\Desktop\AppTrace.lnk")
$desktopShortcut.TargetPath = $targetExe
$desktopShortcut.WorkingDirectory = $InstallPath
$desktopShortcut.Description = "AppTrace — Smart Application Manager for Windows 11"
if (Test-Path $iconPath) { $desktopShortcut.IconLocation = "$iconPath,0" }
$desktopShortcut.Save()

Write-Host "[4/4] Registering in Windows Uninstall Control Panel..." -ForegroundColor Yellow
$regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AppTrace"
if (-not (Test-Path $regPath)) {
    New-Item -Path $regPath -Force | Out-Null
}

$uninstallCmd = "powershell.exe -ExecutionPolicy Bypass -NoProfile -File `"$InstallPath\Uninstall-AppTrace.ps1`""

Set-ItemProperty -Path $regPath -Name "DisplayName" -Value "AppTrace — Smart Application Manager"
Set-ItemProperty -Path $regPath -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path $regPath -Name "Publisher" -Value "Antigravity Systems"
Set-ItemProperty -Path $regPath -Name "InstallLocation" -Value $InstallPath
Set-ItemProperty -Path $regPath -Name "DisplayIcon" -Value "$iconPath,0"
Set-ItemProperty -Path $regPath -Name "UninstallString" -Value $uninstallCmd
Set-ItemProperty -Path $regPath -Name "NoModify" -Value 1 -Type DWord
Set-ItemProperty -Path $regPath -Name "NoRepair" -Value 1 -Type DWord
Set-ItemProperty -Path $regPath -Name "EstimatedSize" -Value 1200 -Type DWord

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "  AppTrace was successfully installed!                   " -ForegroundColor Green
Write-Host "  Destination : $InstallPath                             " -ForegroundColor White
Write-Host "  Shortcut    : Desktop & Start Menu                     " -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green
Write-Host ""
