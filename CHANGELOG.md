# Changelog

All notable changes to **AppTrace — Smart Application Manager** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - 2026-11-01

### Added
- **Multi-Source Detection Engine**: Comprehensive scanning of 64-bit and 32-bit registry uninstall keys, per-user installations (`HKCU`), and Microsoft Store / MSIX packages.
- **Intelligent Deduplication**: Merged duplicate cross-architecture entries with Windows security patch (`KBxxxxxx`) and system framework filtering.
- **Transparent Size Measurement**: Distinction between Verified folder sizes, Estimated registry DWORD sizes, and Unknown sizes.
- **Windows 11 Fluent Design & Glassmorphism**:
  - Native Desktop Window Manager (DWM) **Mica** and **Acrylic** translucency.
  - Semi-transparent glass cards, subtle borders, and smooth hover animations.
  - Dark and Light theme support with real-time accent color selector (Blue, Emerald, Purple, Amber, Crimson, Cyan).
  - Configurable glass transparency slider (80% – 100%).
- **Interactive Overview Dashboard**:
  - Six real-time system metrics cards: Installed Apps, Software Footprint, Startup Programs, Remnant Candidates, Safe to Clean, and C: Drive Health indicator.
  - Recently installed software showcase with authentic application icons.
- **Deep Leftover Remnant Scanner**:
  - Targeted residual file scanning for specific applications or full system orphan sweeps.
  - Locations inspected: `%ProgramFiles%`, `%ProgramFiles(x86)%`, `%LocalAppData%`, `%AppData%`, `%ProgramData%`, and Windows Registry.
  - Three-tier confidence scoring: **High Confidence**, **Possible Match**, and **Uncertain / Review**.
- **Safe Review-Based Cleanup System**:
  - Native Windows Recycle Bin routing (`SHFileOperation` with `FOF_ALLOWUNDO`) for instant native undo.
  - Automatic reversible snapshot backup into **Quarantine Vault**.
  - Verified registry `.reg` export before removal.
- **Non-Destructive Startup Manager**:
  - Scans `HKCU\Run`, `HKLM\Run`, `WOW6432Node\Run`, and user startup folders.
  - Safe toggling via Windows Task Manager `StartupApproved\Run` masks.
- **Storage Analysis**:
  - Visual category breakdown (Development, Productivity, Internet, Multimedia, Utilities, Games, System, Other).
  - Storage size distribution brackets and Top 10 largest installed applications leaderboard.
- **Quarantine Vault & Activity Audit**:
  - One-click restoration of quarantined files and registry keys with permission verification.
  - SQLite tamper-evident local audit log with search, action filtering, and CSV export.
- **Bilingual Localization System**:
  - Full native Turkish (`tr`) and English (`en`) support with instant runtime language switching.
  - Dynamic indexed binding notifications.
- **Automated Test Suite**:
  - 22 automated unit and integration tests covering detection, deduplication, leftovers, uninstaller parsing, quarantine, startup management, and SQLite operations.
- **Automated Windows Installer**:
  - `Install-AppTrace.ps1` and double-click `Install.cmd` with Start Menu and Desktop shortcut registration.
