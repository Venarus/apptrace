@echo off
title AppTrace Installer
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "%~dp0Install-AppTrace.ps1"
pause
