# Contributing to AppTrace

Thank you for your interest in contributing to **AppTrace — Smart Application Manager for Windows 11**!

AppTrace is built with a primary commitment to **safety, reliability, and precision over aggressive cleaning**. Every contribution must respect these principles.

---

## 🧭 Principles & Code Standards

1. **Safety First**: Never introduce silent deletions, process terminations of unrelated applications, or modifications to Windows system files (`C:\Windows`, `System32`, protected drivers).
2. **Native Windows Integration**: Follow Windows 11 Fluent Design guidelines. Leverage native APIs (`dwmapi.dll` for Mica/Acrylic, `shell32.dll` for Recycle Bin, Registry APIs with full elevation guardrails).
3. **MVVM & Separation of Concerns**: Keep Presentation (`AppTrace.UI`), Domain logic & Interop (`AppTrace.Core`), and Tests (`AppTrace.Tests`) strictly separated.
4. **Transparent Size & Metrics**: Distinguish between Verified, Estimated, and Unknown sizes. Never present unverified estimations as exact measurements.

---

## 🛠️ Development Setup

### Prerequisites
- Windows 10 (version 1809+) or Windows 11 (22H2+ recommended for Mica/Acrylic support)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022+ (with *.NET Desktop Development* workload) or VS Code / JetBrains Rider

### Build & Run
```powershell
# Clone the repository
git clone https://github.com/Venarus/apptrace.git
cd apptrace

# Build the solution
dotnet build AppTrace.slnx

# Execute automated tests
dotnet test AppTrace.slnx

# Run the desktop application
dotnet run --project AppTrace.UI\AppTrace.UI.csproj
```

---

## 🌿 Branching & Git Workflow

- **`main`**: Production-ready, stable codebase.
- **`feature/<name>`**: New capabilities or enhancements.
- **`fix/<name>`**: Bug fixes or security patches.

### Commit Message Conventions
We follow [Conventional Commits](https://www.conventionalcommits.org/):
- `feat:` New user-facing feature or capability
- `fix:` Bug fix or issue resolution
- `refactor:` Code refactoring with no functional change
- `docs:` Documentation updates
- `style:` Formatting, UI visual polish, or theme styling
- `test:` Adding or updating unit/integration tests
- `chore:` Build scripts, dependencies, or configuration changes

---

## 🧪 Testing Guidelines

- All pull requests must pass 100% of automated unit and integration tests.
- When adding new functionality (e.g., leftover heuristics, uninstaller handlers), include corresponding unit tests in `AppTrace.Tests`.
- Run tests locally before opening a pull request:
  ```powershell
  dotnet test AppTrace.slnx -c Release
  ```

---

## 📬 Submitting Pull Requests

1. Fork the repository and create your branch from `main`.
2. Ensure code builds cleanly with zero warnings/errors.
3. Verify all automated tests pass.
4. Open a Pull Request with a clear description of the problem solved and test evidence.
