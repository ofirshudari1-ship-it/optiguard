using System;
using Microsoft.Win32;

namespace UninstallerPro
{
    public class InstalledProgram
    {
        public string DisplayName { get; set; }
        public string Publisher { get; set; }
        public string DisplayVersion { get; set; }
        public double? SizeMB { get; set; }
        public string InstallDate { get; set; }
        public string UninstallString { get; set; }
        public string QuietUninstallString { get; set; }
        public string InstallLocation { get; set; }
        public RegistryHive Hive { get; set; }
        public string SubKeyPath { get; set; }   // e.g. SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{guid}
        public bool SystemComponent { get; set; }
        public string Category { get; set; }
        public bool IsSelected { get; set; }
        public string CategoryText { get { return I18n.T(Category); } }

        public string SizeText { get { return SizeMB.HasValue ? SizeMB.Value.ToString("0.0") + " MB" : "-"; } }

        public string InstallDateText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(InstallDate)) return "-";
                DateTime dt;
                if (DateTime.TryParseExact(InstallDate, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
                    return dt.ToString("dd/MM/yyyy");
                if (DateTime.TryParse(InstallDate, out dt))
                    return dt.ToString("dd/MM/yyyy");
                return InstallDate;
            }
        }

        // "Safe to remove" confidence - local heuristic computed by SafeRemovalData
        // from Windows Prefetch (.pf) last-run timestamps, not from any network
        // source. "unknown" means we found no prefetch evidence either way (e.g.
        // prefetch disabled, or the program's exe couldn't be matched) - it is
        // never treated as "safe", only as "no signal".
        public string SafeRemovalLevel { get; set; }
        public DateTime? LastUsedEstimate { get; set; }

        public string SafeRemovalText
        {
            get
            {
                switch (SafeRemovalLevel)
                {
                    case "high": return I18n.T("safe_removal_high");
                    case "medium": return I18n.T("safe_removal_medium");
                    case "low": return I18n.T("safe_removal_low");
                    default: return I18n.T("safe_removal_unknown");
                }
            }
        }

        public string LastUsedText
        {
            get { return LastUsedEstimate.HasValue ? LastUsedEstimate.Value.ToString("dd/MM/yyyy") : "-"; }
        }
    }

    public enum ResidualType { Registry, Folder, Shortcut }

    public class ResidualItem
    {
        public ResidualType Type { get; set; }
        public string Path { get; set; }        // full path (folder/shortcut) or registry sub-key path
        public string DisplayPath { get; set; }
        public string Reason { get; set; }
        public RegistryHive Hive { get; set; }      // used when Type == Registry
        public string SubKeyPath { get; set; }      // used when Type == Registry

        public string TypeText
        {
            get
            {
                switch (Type)
                {
                    case ResidualType.Registry: return "רישום";
                    case ResidualType.Shortcut: return "קיצור דרך";
                    default: return "תיקייה";
                }
            }
        }
    }

    public class GameInfo
    {
        public string Source { get; set; }      // Steam / Epic Games / GOG / Ubisoft Connect
        public string Name { get; set; }
        public string AppId { get; set; }
        public string InstallDir { get; set; }
        public string ManifestPath { get; set; }
        public string CatalogNamespace { get; set; }
        public string CatalogItemId { get; set; }
        public double? SizeMB { get; set; }
        public RegistryHive? Hive { get; set; }
        public string SubKeyPath { get; set; }
    }

    public class BrowserExtension
    {
        public string Browser { get; set; }
        public string Profile { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Path { get; set; }
        public string ExtensionsJsonPath { get; set; } // Firefox only

        // סימון עבור הסרה מרוכזת (batch) של כמה תוספים בבת אחת - עקבי עם
        // הדפוס הקיים ב-IsSelected של InstalledProgram/DuplicateFile.
        public bool IsSelected { get; set; }

        // כאשר אותו תוסף מותקן במספר פרופילים, הם מאוחדים לשורה אחת -
        // ProfileEntries מחזיק את כל (פרופיל, נתיב, קובץ-json) לצורך הסרה מלאה.
        public System.Collections.Generic.List<BrowserExtension> ProfileEntries { get; set; }

        // דירוג סיכון הרשאות: מחושב מרשימת ה-permissions/host_permissions
        // במניפסט (רק Chromium, ל-Firefox אין פורמט אחיד זמין). "unknown" כאשר לא נבדק.
        public string RiskLevel { get; set; }
        public System.Collections.Generic.List<string> RiskReasons { get; set; }
        public string RiskReasonsText { get { return RiskReasons != null && RiskReasons.Count > 0 ? string.Join(", ", RiskReasons) : null; } }
        public string RiskLevelText
        {
            get
            {
                switch (RiskLevel)
                {
                    case "high": return I18n.T("risk_high");
                    case "medium": return I18n.T("risk_medium");
                    case "low": return I18n.T("risk_low");
                    default: return I18n.T("risk_unknown");
                }
            }
        }

        public string ProfilesText
        {
            get
            {
                if (ProfileEntries == null || ProfileEntries.Count <= 1) return Profile;
                return string.Join(", ", ProfileEntries.ConvertAll(p => p.Profile));
            }
        }
    }

    public enum StartupType { Registry, Folder }

    public class StartupItem
    {
        public bool Enabled { get; set; }
        public StartupType Type { get; set; }
        public string Location { get; set; }
        public RegistryHive RegHive { get; set; }
        public string RegSubKey { get; set; }
        public string ValueName { get; set; }
        public string Command { get; set; }
        public string FolderPath { get; set; }
        public string BookmarkSubKey { get; set; } // when disabled, id under our bookkeeping key

        // Bulk-selection checkbox in the Startup grid (mirrors InstalledProgram.IsSelected).
        public bool IsSelected { get; set; }

        public string DisplayName
        {
            get { return Type == StartupType.Folder ? System.IO.Path.GetFileNameWithoutExtension(FolderPath ?? "") : ValueName; }
        }

        public string StatusText { get { return Enabled ? "פעיל" : "מושבת"; } }

        // אימות חתימה דיגיטלית של קובץ ה-exe שמופעל בהפעלה אוטומטית - נבדק
        // ב-StartupData מול תעודת Authenticode אמיתית של Windows, לא היוריסטיקה.
        // "unknown" = קובץ לא נמצא/לא ניתן לבדיקה (למשל פקודת מערכת בלי exe
        // ברור) - לעולם לא מוצג כ"לא בטוח", רק כ"לא זוהה", בהתאם לעקרון
        // "ראיות, לא הפחדות" של הכלי.
        public string SignatureStatus { get; set; } // "signed" / "unsigned" / "unknown"
        public string SignaturePublisher { get; set; }

        public string SignatureText
        {
            get
            {
                switch (SignatureStatus)
                {
                    case "signed": return string.IsNullOrEmpty(SignaturePublisher) ? I18n.T("sig_verified") : SignaturePublisher;
                    case "unsigned": return I18n.T("sig_unsigned");
                    default: return I18n.T("sig_unknown");
                }
            }
        }
    }

    public enum WizardKind { Junk, BrokenStartup, EmptyFolder }

    public class WizardFinding
    {
        public WizardKind Kind { get; set; }
        public string Name { get; set; }
        public string Detail { get; set; }
        public object Payload { get; set; }
    }

}
