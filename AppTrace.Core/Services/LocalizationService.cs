using System.ComponentModel;

namespace AppTrace.Core.Services;

public interface ILocalizationService : INotifyPropertyChanged
{
    string CurrentLanguage { get; set; }
    string this[string key] { get; }
    string GetString(string key);
    void SetLanguage(string langCode);
}

public class LocalizationService : ILocalizationService
{
    private string _currentLanguage = "tr";
    public event PropertyChangedEventHandler? PropertyChanged;

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set => SetLanguage(value);
    }

    public string this[string key] => GetString(key);

    private static readonly Dictionary<string, string> TurkishStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Navigation
        ["Nav_Overview"] = "📊  Genel Bakış",
        ["Nav_InstalledApps"] = "📦  Yüklü Uygulamalar",
        ["Nav_LeftoverScanner"] = "🔍  Artık Dosya Tarayıcısı",
        ["Nav_StartupManager"] = "⚡  Başlangıç Yöneticisi",
        ["Nav_StorageAnalysis"] = "💾  Depolama Analizi",
        ["Nav_Quarantine"] = "🛡️  Karantina Kasası",
        ["Nav_ActivityHistory"] = "📜  Etkinlik Geçmişi",
        ["Nav_Settings"] = "⚙️  Ayarlar",

        // Overview
        ["Overview_Title"] = "Sistem Genel Bakışı",
        ["Overview_Subtitle"] = "Windows 11 için akıllı yazılım izleme, depolama analizi ve kalıntı tespiti",
        ["Overview_InstalledApps"] = "Yüklü Yazılımlar",
        ["Overview_InstalledAppsSub"] = "32/64-bit ve Mağaza genelinde tespit edildi",
        ["Overview_Storage"] = "Yazılım Depolaması",
        ["Overview_StorageSub"] = "Yüklü uygulamaların tahmini kapladığı alan",
        ["Overview_Startup"] = "Başlangıç Programları",
        ["Overview_StartupSub"] = "Sistem açılışında çalışan kayıtlı programlar",
        ["Overview_Leftovers"] = "Artık Kalıntılar",
        ["Overview_SafeToClean"] = "Temizlemeye Uygun",
        ["Overview_SafeToCleanSub"] = "Yüksek güvenilirlikteki artık klasörler",
        ["Overview_DriveHealth"] = "Sistem Diski Durumu",
        ["Overview_RecentApps"] = "Son Kurulan Yazılımlar",
        ["Overview_ViewAll"] = "Tümünü Gör →",
        ["Overview_Refresh"] = "↻ Envanteri Yenile",
        ["Overview_Scan"] = "🔍 Artıkları Tara",

        // Installed Apps
        ["Apps_Title"] = "Yüklü Uygulamalar",
        ["Apps_SearchPlaceholder"] = "Uygulama, yayıncı ara...",
        ["Apps_Filter_All"] = "Tümü",
        ["Apps_Filter_Desktop"] = "Masaüstü (Win32)",
        ["Apps_Filter_Store"] = "Mağaza Uygulamaları",
        ["Apps_Filter_Large"] = "Büyük (>1GB)",
        ["Apps_Filter_Recent"] = "Son Kurulanlar",
        ["Apps_Col_Name"] = "Uygulama Adı",
        ["Apps_Col_Publisher"] = "Yayıncı",
        ["Apps_Col_InstallDate"] = "Kurulum Tarihi",
        ["Apps_Col_Size"] = "Tahmini Boyut",
        ["Apps_Col_Source"] = "Kaynak",
        ["Apps_Col_Actions"] = "İşlemler",
        ["Apps_Inspect"] = "İncele →",
        ["Apps_Launch"] = "🚀 Başlat",
        ["Apps_OpenFolder"] = "📁 Klasörü Aç",
        ["Apps_ScanLeftovers"] = "🔍 Artıkları Tara",
        ["Apps_Details"] = "Uygulama Detayları →",
        ["Apps_Uninstall"] = "Kaldır",

        // App Details
        ["Details_Back"] = "← Uygulamalara Dön",
        ["Details_Title"] = "Kurulum ve Sistem Bilgileri",
        ["Details_DiskUsage"] = "Disk Kullanımı",
        ["Details_PackageSource"] = "Paket Kaynağı",
        ["Details_StartupStatus"] = "Başlangıç Durumu",
        ["Details_InstallDir"] = "Kurulum Klasörü:",
        ["Details_RegistryKey"] = "Kayıt Defteri Konumu:",
        ["Details_UninstallCmd"] = "Kaldırma Komutu:",
        ["Details_PublisherUrl"] = "Destek / Yayıncı:",
        ["Details_VisitWebsite"] = "Siteyi Ziyaret Et 🌐",
        ["Details_RelatedDirs"] = "İlişkili Uygulama Klasörleri",

        // Leftover Scanner
        ["Scanner_Title"] = "Akıllı Artık Dosya Tarayıcısı",
        ["Scanner_DeepScan"] = "🔍 Derin Tarama Başlat",
        ["Scanner_Cancel"] = "İptal Et",
        ["Scanner_TotalRemnants"] = "Toplam Artık",
        ["Scanner_Recoverable"] = "Kurtarılabilir Alan",
        ["Scanner_Col_Path"] = "Kalıntı Yolu",
        ["Scanner_Col_Size"] = "Boyut",
        ["Scanner_Col_Confidence"] = "Güvenilirlik",
        ["Scanner_Col_Reason"] = "Tespit Nedeni",
        ["Scanner_Col_Action"] = "Aç",
        ["Scanner_SelectSafe"] = "Yalnızca Güvenlileri Seç",
        ["Scanner_SelectAll"] = "Tümünü Seç",
        ["Scanner_DeselectAll"] = "Seçimi Kaldır",
        ["Scanner_RecycleBinCheck"] = "♻️ Windows Geri Dönüşüm Kutusu'na gönder",
        ["Scanner_QuarantineCheck"] = "🛡️ Önce Karantina yedeği oluştur",
        ["Scanner_CleanButton"] = "Seçilenleri İncele ve Temizle →",

        // Startup Manager
        ["Startup_Title"] = "Başlangıç Uygulamaları Yöneticisi",
        ["Startup_Subtitle"] = "Windows ile otomatik başlayan programları güvenle yönetin",
        ["Startup_Refresh"] = "↻ Başlangıcı Yenile",
        ["Startup_SearchPlaceholder"] = "Başlangıç programı ara...",
        ["Startup_Total"] = "Toplam Kayıtlı",
        ["Startup_Active"] = "Açılışta Aktif",
        ["Startup_Disabled"] = "Devre Dışı",
        ["Startup_Col_Program"] = "Başlangıç Programı",
        ["Startup_Col_Source"] = "Kaynak",
        ["Startup_Col_Path"] = "Çalıştırılabilir Dosya Yolu",
        ["Startup_Col_Status"] = "Durum",
        ["Startup_Col_Actions"] = "İşlemler",
        ["Startup_Disable"] = "Devre Dışı Bırak",
        ["Startup_Enable"] = "Etkinleştir",
        ["Startup_Locate"] = "Konumu Aç 📁",

        // Storage Analysis
        ["Storage_Title"] = "Depolama ve Boyut Analizi",
        ["Storage_Subtitle"] = "Ayrıntılı disk tüketimi görselleştirmesi ve yazılım alanı dağılımı",
        ["Storage_ScanRemovable"] = "🧹 Artık Dosyaları Tara",
        ["Storage_Categories"] = "Uygulama Kategorileri",
        ["Storage_Distribution"] = "Boyut Dağılımı",
        ["Storage_Top10"] = "En Çok Yer Kaplayan 10 Uygulama",

        // Quarantine
        ["Quarantine_Title"] = "Karantina ve Geri Alma Kasası",
        ["Quarantine_Subtitle"] = "Silme işlemlerinden önce alınan izole yedekler. Her öğe orijinal yerine tam olarak geri yüklenebilir.",
        ["Quarantine_Refresh"] = "↻ Kasayı Yenile",
        ["Quarantine_TotalItems"] = "Karantinadaki Öğeler",
        ["Quarantine_TotalSize"] = "Kasa Boyutu",
        ["Quarantine_Restore"] = "↺ Geri Yükle",
        ["Quarantine_Delete"] = "Kalıcı Sil",

        // Activity History
        ["History_Title"] = "Etkinlik Geçmişi ve Denetim Günlüğü",
        ["History_Subtitle"] = "Tüm yazılım kaldırma, tarama ve temizlik adımlarını kaydeden yerel SQLite denetim kaydı",
        ["History_ExportCsv"] = "📥 CSV Olarak Dışa Aktar",
        ["History_Clear"] = "🗑️ Günlüğü Temizle",
        ["History_Refresh"] = "↻ Yenile",
        ["History_SearchPlaceholder"] = "Günlüklerde ara...",

        // Settings
        ["Settings_Title"] = "Ayarlar ve Tercihler",
        ["Settings_Subtitle"] = "Windows 11 görsel görünümünü özelleştirin, artık tespit hassasiyetini ve dili ayarlayın",
        ["Settings_Visual"] = "Görsel Tasarım ve Fluent Arayüz",
        ["Settings_Theme"] = "Uygulama Teması",
        ["Settings_Backdrop"] = "Arka Plan Malzemesi (DWM)",
        ["Settings_Transparency"] = "Cam Şeffaflığı",
        ["Settings_AccentColor"] = "Vurgu Rengi",
        ["Settings_Language"] = "Uygulama Dili (Language)",
        ["Settings_Safety"] = "Güvenlik Motoru ve Temizlik Ayarları",
        ["Settings_ScanMode"] = "Tarama Hassasiyeti Modu",
        ["Settings_RecycleBinOption"] = "Dosyaları Geri Dönüşüm Kutusu'na taşı",
        ["Settings_QuarantineOption"] = "Geri alınabilir Karantina yedeği oluştur",
        ["Settings_Exclusions"] = "Özel Korunan Dizinler",
        ["Settings_ExclusionDesc"] = "Burada listelenen yollar temizleme motoru tarafından asla taranmaz veya silinmez:",
        ["Settings_AddExclusion"] = "+ İstisna Ekle",
        ["Settings_Info"] = "Uygulama Bilgileri ve Tanılama",
        ["Settings_Reset"] = "↺ Tüm Ayarları Varsayılana Sıfırla"
    };

    private static readonly Dictionary<string, string> EnglishStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Navigation
        ["Nav_Overview"] = "📊  Overview",
        ["Nav_InstalledApps"] = "📦  Installed Applications",
        ["Nav_LeftoverScanner"] = "🔍  Leftover Scanner",
        ["Nav_StartupManager"] = "⚡  Startup Manager",
        ["Nav_StorageAnalysis"] = "💾  Storage Analysis",
        ["Nav_Quarantine"] = "🛡️  Quarantine Vault",
        ["Nav_ActivityHistory"] = "📜  Activity History",
        ["Nav_Settings"] = "⚙️  Settings",

        // Overview
        ["Overview_Title"] = "System Overview",
        ["Overview_Subtitle"] = "Smart monitoring, storage analysis, and remnant detection for Windows 11",
        ["Overview_InstalledApps"] = "Installed Software",
        ["Overview_InstalledAppsSub"] = "Detected across 32/64-bit and Store",
        ["Overview_Storage"] = "Software Storage",
        ["Overview_StorageSub"] = "Estimated installed application footprint",
        ["Overview_Startup"] = "Startup Applications",
        ["Overview_StartupSub"] = "Registered boot autorun entries",
        ["Overview_Leftovers"] = "Orphaned Remnants",
        ["Overview_SafeToClean"] = "Safe to Clean",
        ["Overview_SafeToCleanSub"] = "High confidence residual directories",
        ["Overview_DriveHealth"] = "System Drive Health",
        ["Overview_RecentApps"] = "Recently Installed Software",
        ["Overview_ViewAll"] = "View All →",
        ["Overview_Refresh"] = "↻ Refresh Inventory",
        ["Overview_Scan"] = "🔍 Scan for Leftovers",

        // Installed Apps
        ["Apps_Title"] = "Installed Applications",
        ["Apps_SearchPlaceholder"] = "Search applications, publishers...",
        ["Apps_Filter_All"] = "All",
        ["Apps_Filter_Desktop"] = "Desktop",
        ["Apps_Filter_Store"] = "Store Apps",
        ["Apps_Filter_Large"] = "Large (>1GB)",
        ["Apps_Filter_Recent"] = "Recent",
        ["Apps_Col_Name"] = "Application Name",
        ["Apps_Col_Publisher"] = "Publisher",
        ["Apps_Col_InstallDate"] = "Install Date",
        ["Apps_Col_Size"] = "Estimated Size",
        ["Apps_Col_Source"] = "Source",
        ["Apps_Col_Actions"] = "Actions",
        ["Apps_Inspect"] = "Inspect →",
        ["Apps_Launch"] = "🚀 Launch",
        ["Apps_OpenFolder"] = "📁 Open Folder",
        ["Apps_ScanLeftovers"] = "🔍 Scan Leftovers",
        ["Apps_Details"] = "Application Details →",
        ["Apps_Uninstall"] = "Uninstall App",

        // App Details
        ["Details_Back"] = "← Back to Applications",
        ["Details_Title"] = "Installation Details",
        ["Details_DiskUsage"] = "Disk Usage",
        ["Details_PackageSource"] = "Package Source",
        ["Details_StartupStatus"] = "Startup Status",
        ["Details_InstallDir"] = "Install Directory:",
        ["Details_RegistryKey"] = "Registry Location:",
        ["Details_UninstallCmd"] = "Uninstall Command:",
        ["Details_PublisherUrl"] = "Support / Publisher:",
        ["Details_VisitWebsite"] = "Visit Website 🌐",
        ["Details_RelatedDirs"] = "Associated Application Folders",

        // Leftover Scanner
        ["Scanner_Title"] = "Intelligent Leftover Scanner",
        ["Scanner_DeepScan"] = "🔍 Start Deep Scan",
        ["Scanner_Cancel"] = "Cancel Scan",
        ["Scanner_TotalRemnants"] = "Total Remnants",
        ["Scanner_Recoverable"] = "Recoverable Space",
        ["Scanner_Col_Path"] = "Residual Path",
        ["Scanner_Col_Size"] = "Size",
        ["Scanner_Col_Confidence"] = "Confidence",
        ["Scanner_Col_Reason"] = "Detection Reason",
        ["Scanner_Col_Action"] = "Explore",
        ["Scanner_SelectSafe"] = "Select Safe Only",
        ["Scanner_SelectAll"] = "Select All",
        ["Scanner_DeselectAll"] = "Deselect All",
        ["Scanner_RecycleBinCheck"] = "♻️ Move to Windows Recycle Bin",
        ["Scanner_QuarantineCheck"] = "🛡️ Create Quarantine Backup first",
        ["Scanner_CleanButton"] = "Review & Clean Selected Items →",

        // Startup Manager
        ["Startup_Title"] = "Startup Application Manager",
        ["Startup_Subtitle"] = "Inspect and safely manage applications configured to boot with Windows",
        ["Startup_Refresh"] = "↻ Refresh Startup",
        ["Startup_SearchPlaceholder"] = "Search startup programs...",
        ["Startup_Total"] = "Total Registered",
        ["Startup_Active"] = "Active on Boot",
        ["Startup_Disabled"] = "Disabled",
        ["Startup_Col_Program"] = "Startup Program",
        ["Startup_Col_Source"] = "Source",
        ["Startup_Col_Path"] = "Executable Path",
        ["Startup_Col_Status"] = "Status",
        ["Startup_Col_Actions"] = "Actions",
        ["Startup_Disable"] = "Disable",
        ["Startup_Enable"] = "Enable",
        ["Startup_Locate"] = "Locate 📁",

        // Storage Analysis
        ["Storage_Title"] = "Storage Analysis",
        ["Storage_Subtitle"] = "Detailed disk usage visualization and application footprint breakdown",
        ["Storage_ScanRemovable"] = "🧹 Scan Removable Leftovers",
        ["Storage_Categories"] = "Application Categories",
        ["Storage_Distribution"] = "Size Distribution",
        ["Storage_Top10"] = "Top 10 Largest Applications",

        // Quarantine
        ["Quarantine_Title"] = "Quarantine & Rollback Vault",
        ["Quarantine_Subtitle"] = "Isolated backups created before removals. Any item can be safely restored to its exact original location.",
        ["Quarantine_Refresh"] = "↻ Refresh Vault",
        ["Quarantine_TotalItems"] = "Quarantined Items",
        ["Quarantine_TotalSize"] = "Vault Storage Footprint",
        ["Quarantine_Restore"] = "↺ Restore",
        ["Quarantine_Delete"] = "Delete",

        // Activity History
        ["History_Title"] = "Activity History & Audit Log",
        ["History_Subtitle"] = "Tamper-evident local SQLite audit trail for all software installations, scans, and cleanups",
        ["History_ExportCsv"] = "📥 Export CSV",
        ["History_Clear"] = "🗑️ Clear Logs",
        ["History_Refresh"] = "↻ Refresh",
        ["History_SearchPlaceholder"] = "Search logs by app name or description...",

        // Settings
        ["Settings_Title"] = "Settings & Preferences",
        ["Settings_Subtitle"] = "Personalize Windows 11 visual appearance, adjust remnant detection heuristics, and manage safe exclusions",
        ["Settings_Visual"] = "Visual Aesthetics & Fluent Design",
        ["Settings_Theme"] = "Application Theme",
        ["Settings_Backdrop"] = "Backdrop Material",
        ["Settings_Transparency"] = "Glass Transparency",
        ["Settings_AccentColor"] = "Accent Color",
        ["Settings_Language"] = "Application Language",
        ["Settings_Safety"] = "Safety Engine & Cleanup Controls",
        ["Settings_ScanMode"] = "Scanning Heuristic Mode",
        ["Settings_RecycleBinOption"] = "Move Files to Windows Recycle Bin",
        ["Settings_QuarantineOption"] = "Create Reversible Quarantine Backups",
        ["Settings_Exclusions"] = "Custom Excluded Paths",
        ["Settings_ExclusionDesc"] = "Paths listed here are strictly prohibited from being scanned or removed by the cleanup engine:",
        ["Settings_AddExclusion"] = "+ Add Exclusion",
        ["Settings_Info"] = "Application Information & Diagnostics",
        ["Settings_Reset"] = "↺ Reset All Settings to Defaults"
    };

    public string GetString(string key)
    {
        var dict = _currentLanguage.Equals("tr", StringComparison.OrdinalIgnoreCase)
            ? TurkishStrings
            : EnglishStrings;

        if (dict.TryGetValue(key, out var val))
            return val;

        if (EnglishStrings.TryGetValue(key, out var fallbackVal))
            return fallbackVal;

        return key;
    }

    public void SetLanguage(string langCode)
    {
        _currentLanguage = langCode.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ? "tr" : "en";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
