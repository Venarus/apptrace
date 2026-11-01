<#
.SYNOPSIS
    AppTrace — Smart Application Manager Uninstaller
.DESCRIPTION
    Safely uninstalls AppTrace from Windows 11, removes shortcuts, and unregisters from registry.
#>

[CmdletBinding()]
param(
    [switch]$Force
)

Write-Host "==========================================================" -ForegroundColor Yellow
Write-Host "       AppTrace — Official Application Uninstaller        " -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Yellow

if (-not $Force) {
    $confirm = Read-Host "Are you sure you want to uninstall AppTrace from your system? (Y/N)"
    if ($confirm -notmatch "^[Yy]") {
        Write-Host "Uninstallation cancelled by user." -ForegroundColor Gray
        exit 0
    }
}

Write-Host "[1/4] Closing any running AppTrace processes..." -ForegroundColor Yellow
Get-Process -Name "AppTrace.UI" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

Write-Host "[2/4] Removing Start Menu and Desktop shortcuts..." -ForegroundColor Yellow
$desktopLnk = "$env:UserProfile\Desktop\AppTrace.lnk"
if (Test-Path $desktopLnk) { Remove-Item -Force $desktopLnk }

$startMenuFolder = "$env:AppData\Microsoft\Windows\Start Menu\Programs\AppTrace"
if (Test-Path $startMenuFolder) { Remove-Item -Recurse -Force $startMenuFolder }

Write-Host "[3/4] Removing Windows Uninstall registry registration..." -ForegroundColor Yellow
$regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\AppTrace"
if (Test-Path $regPath) { Remove-Item -Path $regPath -Recurse -Force }

Write-Host "[4/4] Scheduling removal of application files..." -ForegroundColor Yellow
$appDir = "$env:LocalAppData\Programs\AppTrace"

# Schedule folder cleanup after script exits
if (Test-Path $appDir) {
    Start-Process -FilePath "cmd.exe" -ArgumentList "/c timeout /t 2 & rmdir /s /q `"$appDir`"" -WindowStyle Hidden
}

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "  AppTrace has been successfully uninstalled.             " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Write-Host ""
