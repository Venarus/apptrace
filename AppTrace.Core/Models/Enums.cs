namespace AppTrace.Core.Models;

public enum SizeConfidence
{
    Verified,
    Estimated,
    Unknown
}

public enum InstallSource
{
    Registry64,
    Registry32,
    UserRegistry,
    MicrosoftStore
}

public enum ConfidenceLevel
{
    High,
    Possible,
    Uncertain
}

public enum CleanupItemType
{
    Directory,
    File,
    RegistryKey
}

public enum StartupSource
{
    HKCU_Run,
    HKLM_Run,
    HKLM_WOW64_Run,
    UserStartupFolder,
    CommonStartupFolder
}

public enum AppCategory
{
    Development,
    Productivity,
    Internet,
    Multimedia,
    Utilities,
    Games,
    System,
    Other
}
