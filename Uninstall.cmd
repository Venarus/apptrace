@echo off
title AppTrace Uninstaller
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "%~dp0Uninstall-AppTrace.ps1"
pause
