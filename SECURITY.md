# Security Policy

## 🛡️ Supported Versions

| Version | Supported | Status |
| :--- | :--- | :--- |
| **v1.0.x** | :white_check_mark: | Currently Supported (Active LTS) |
| < v1.0.0 | :x: | Deprecated / Development |

---

## 🔒 Security Architecture & Guarantees

AppTrace is engineered from the ground up as a **safe, local-first utility**. We adhere to the following strict security commitments:

1. **100% Offline & Private**:
   - Zero telemetry, analytics, or background tracking.
   - Zero remote network connections for scanning or cleaning.
   - All audit logs and metadata are stored exclusively on the user's local machine (`%LocalAppData%\AppTrace\apptrace.db`).

2. **Least Privilege Principle**:
   - AppTrace runs as a standard user process by default.
   - Administrator elevation is requested *only* when an official uninstaller or elevated registry operation explicitly demands it.
   - Never demands permanent system administrative privileges.

3. **Protected System Exclusions**:
   - Critical system directories (`C:\Windows`, `System32`, `SysWOW64`, `Program Files` root, user profile roots) and operating system drivers are strictly hardcoded as immutable protected paths.
   - The remnant cleanup engine refuses to scan or delete files within protected directories regardless of heuristic confidence.

4. **Reversible Cleanup & Non-Destructive Operations**:
   - Files are routed to the **Windows Recycle Bin** first (`SHFileOperation` with `FOF_ALLOWUNDO`) for instant native restoration.
   - All approved deletions create an isolated snapshot backup in the **Quarantine Vault**.
   - Registry changes export a verified `.reg` backup prior to key removal.
   - Startup items are toggled using official Windows `StartupApproved\Run` masks—registry keys are never deleted simply to disable an autorun entry.

---

## 🚨 Reporting a Vulnerability

If you discover a security vulnerability or safety-critical bug within AppTrace:

1. **Do not open a public GitHub issue**.
2. Report the issue privately via [GitHub Security Advisory](https://github.com/Venarus/apptrace/security/advisories) or by contacting the project maintainer at `venarus@proton.me`.
3. Please include:
   - Detailed description of the vulnerability.
   - Steps to reproduce or proof-of-concept.
   - Impact assessment on system files or user data.
4. We aim to acknowledge receipt within 48 hours and provide a patched release promptly.
