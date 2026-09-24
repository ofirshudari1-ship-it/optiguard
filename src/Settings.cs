using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace UninstallerPro
{
    public class AppSettings
    {
        // No saved choice yet -> follow the Windows light/dark app mode
        // (STANDARDS.md section 3). An explicit choice from onboarding or
        // Settings is saved and always wins over this afterwards.
        public string Theme = UninstallerPro.Theme.DetectSystemTheme();
        public bool ShowSystemComponents = false;
        public string Language = I18n.English;
        // Retained (but no longer surfaced in Settings UI) for backward
        // compatibility with settings.json files written before 4.10.2,
        // when updates were checked against a self-hosted manifest URL
        // instead of GitHub Releases.
        public string UpdateManifestUrl = "";
        public bool CreateRestorePoints = true;
        public bool FirstLaunchCompleted = false;
        public bool EnableNotifications = true;
        // Default enabled (opt-out): checks GitHub Releases for a newer
        // OptiGuard version a few seconds after startup, at most once per
        // session. See UpdateChecker.cs.
        public bool AutoCheckUpdates = true;
        // Default DISABLED (opt-in): silently downloading and launching an
        // installer .exe is a more invasive action than the passive
        // "here's a banner/notification" behavior AutoCheckUpdates controls,
        // so a user has to turn this on deliberately. See UpdateChecker.cs
        // DownloadAndLaunchSilentInstall.
        public bool AutoInstallUpdates = false;
        public int QuarantineRetentionDays = 7;

        // Scheduled automatic cleanup (added 4.10.0, see ScheduledCleanupData.cs) -
        // registers a Windows Task Scheduler task that runs "OptiGuard.exe
        // --auto-clean" unattended. Off by default: this is an opt-in
        // convenience feature, not something enabled behind the user's back.
        public bool ScheduledCleanupEnabled = false;
        public string ScheduledCleanupFrequency = "Weekly"; // Daily | Weekly | Monthly

        // Which junk categories an unattended (--auto-clean) run is allowed to
        // touch, comma-separated JunkCategory.Key values (see
        // JunkCleanerData.cs). Recycle Bin is never included (see
        // ScheduledCleanupData.RunHeadlessCleanup). Default = every other
        // category, matching the original 4.10.0 behavior so upgrading users
        // see no change unless they visit Settings > Advanced themselves.
        public string ScheduledCleanupCategories = "user_temp,win_temp,prefetch,win_update,thumbnails,browser_cache";

        // Safety valve for unattended cleanup (added 4.11.0): if the total
        // size found across the included categories exceeds this many MB, the
        // scheduled run skips deleting anything and just notifies instead -
        // an unusually large amount of "junk" is more likely a sign something
        // unexpected is going on than something safe to auto-delete while the
        // user isn't watching. 0 = no limit (deletes regardless of size, the
        // original behavior).
        public int ScheduledCleanupMaxSizeMB = 0;

        // Window state persistence (size/position/maximized) - see MainWindow.cs.
        // -1 sentinel means "never saved yet" -> MainWindow falls back to its
        // built-in default size/centered position instead of using these.
        public double WindowWidth = -1;
        public double WindowHeight = -1;
        public double WindowLeft = -1;
        public double WindowTop = -1;
        public bool WindowMaximized = false;

        // Persistent desktop widget (added 4.14.0, see WidgetWindow.cs) - a
        // small always-on-top panel showing the Health Score with quick
        // actions, kept separate from the main window. Default ON: the user
        // explicitly wants it always visible without having to open it first.
        public bool ShowDesktopWidget = false; // opt-in - off by default, enable from Settings
        // -1 sentinel = never saved yet -> WidgetWindow falls back to its
        // default bottom-right corner position, same pattern as WindowLeft/Top.
        public double WidgetLeft = -1;
        public double WidgetTop = -1;

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(AppPaths.SettingsFile))
                {
                    var serializer = new JavaScriptSerializer();
                    var dict = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(AppPaths.SettingsFile));
                    var s = new AppSettings();
                    // Each field is read independently: one hand-edited or
                    // corrupted value (e.g. "WidgetLeft":"abc") only falls back
                    // to that field's default instead of throwing away every
                    // saved preference and re-running first-run onboarding.
                    try { if (dict.ContainsKey("Theme") && dict["Theme"] != null) s.Theme = dict["Theme"].ToString(); } catch { }
                    try { if (dict.ContainsKey("ShowSystemComponents") && dict["ShowSystemComponents"] != null) s.ShowSystemComponents = Convert.ToBoolean(dict["ShowSystemComponents"]); } catch { }
                    try { if (dict.ContainsKey("Language") && dict["Language"] != null) s.Language = NormalizeLanguage(dict["Language"].ToString()); } catch { }
                    try { if (dict.ContainsKey("UpdateManifestUrl") && dict["UpdateManifestUrl"] != null) s.UpdateManifestUrl = dict["UpdateManifestUrl"].ToString(); } catch { }
                    try { if (dict.ContainsKey("CreateRestorePoints") && dict["CreateRestorePoints"] != null) s.CreateRestorePoints = Convert.ToBoolean(dict["CreateRestorePoints"]); } catch { }
                    try { if (dict.ContainsKey("FirstLaunchCompleted") && dict["FirstLaunchCompleted"] != null) s.FirstLaunchCompleted = Convert.ToBoolean(dict["FirstLaunchCompleted"]); } catch { }
                    try { if (dict.ContainsKey("EnableNotifications") && dict["EnableNotifications"] != null) s.EnableNotifications = Convert.ToBoolean(dict["EnableNotifications"]); } catch { }
                    try { if (dict.ContainsKey("AutoCheckUpdates") && dict["AutoCheckUpdates"] != null) s.AutoCheckUpdates = Convert.ToBoolean(dict["AutoCheckUpdates"]); } catch { }
                    try { if (dict.ContainsKey("AutoInstallUpdates") && dict["AutoInstallUpdates"] != null) s.AutoInstallUpdates = Convert.ToBoolean(dict["AutoInstallUpdates"]); } catch { }
                    try { if (dict.ContainsKey("QuarantineRetentionDays") && dict["QuarantineRetentionDays"] != null) s.QuarantineRetentionDays = Convert.ToInt32(dict["QuarantineRetentionDays"]); } catch { }
                    try { if (dict.ContainsKey("ScheduledCleanupEnabled") && dict["ScheduledCleanupEnabled"] != null) s.ScheduledCleanupEnabled = Convert.ToBoolean(dict["ScheduledCleanupEnabled"]); } catch { }
                    try { if (dict.ContainsKey("ScheduledCleanupFrequency") && dict["ScheduledCleanupFrequency"] != null) s.ScheduledCleanupFrequency = dict["ScheduledCleanupFrequency"].ToString(); } catch { }
                    try { if (dict.ContainsKey("ScheduledCleanupCategories") && dict["ScheduledCleanupCategories"] != null) s.ScheduledCleanupCategories = dict["ScheduledCleanupCategories"].ToString(); } catch { }
                    try { if (dict.ContainsKey("ScheduledCleanupMaxSizeMB") && dict["ScheduledCleanupMaxSizeMB"] != null) s.ScheduledCleanupMaxSizeMB = Convert.ToInt32(dict["ScheduledCleanupMaxSizeMB"]); } catch { }
                    try { if (dict.ContainsKey("WindowWidth") && dict["WindowWidth"] != null) s.WindowWidth = Convert.ToDouble(dict["WindowWidth"]); } catch { }
                    try { if (dict.ContainsKey("WindowHeight") && dict["WindowHeight"] != null) s.WindowHeight = Convert.ToDouble(dict["WindowHeight"]); } catch { }
                    try { if (dict.ContainsKey("WindowLeft") && dict["WindowLeft"] != null) s.WindowLeft = Convert.ToDouble(dict["WindowLeft"]); } catch { }
                    try { if (dict.ContainsKey("WindowTop") && dict["WindowTop"] != null) s.WindowTop = Convert.ToDouble(dict["WindowTop"]); } catch { }
                    try { if (dict.ContainsKey("WindowMaximized") && dict["WindowMaximized"] != null) s.WindowMaximized = Convert.ToBoolean(dict["WindowMaximized"]); } catch { }
                    try { if (dict.ContainsKey("ShowDesktopWidget") && dict["ShowDesktopWidget"] != null) s.ShowDesktopWidget = Convert.ToBoolean(dict["ShowDesktopWidget"]); } catch { }
                    try { if (dict.ContainsKey("WidgetLeft") && dict["WidgetLeft"] != null) s.WidgetLeft = Convert.ToDouble(dict["WidgetLeft"]); } catch { }
                    try { if (dict.ContainsKey("WidgetTop") && dict["WidgetTop"] != null) s.WidgetTop = Convert.ToDouble(dict["WidgetTop"]); } catch { }
                    return s;
                }
            }
            catch { }
            return new AppSettings();
        }

        // Installers before 4.15.0 seeded settings.json with "HE"/"EN", which
        // never matched I18n.Hebrew ("he") - Hebrew picked in the installer
        // was silently ignored. Accept any casing and fall back to English
        // for anything unrecognized, so those existing files now work too.
        private static string NormalizeLanguage(string value)
        {
            var v = (value ?? "").Trim().ToLowerInvariant();
            return v == I18n.Hebrew ? I18n.Hebrew : I18n.English;
        }

        public void Save()
        {
            try
            {
                AppPaths.EnsureDataDir();
                var serializer = new JavaScriptSerializer();
                // Written to a temp file and swapped in, so a crash/power cut
                // mid-save can't leave a truncated settings.json behind.
                var tmp = AppPaths.SettingsFile + ".tmp";
                File.WriteAllText(tmp, serializer.Serialize(new Dictionary<string, object>
                {
                    { "Theme", Theme }, { "ShowSystemComponents", ShowSystemComponents }, { "Language", Language }, { "UpdateManifestUrl", UpdateManifestUrl }, { "CreateRestorePoints", CreateRestorePoints }, { "FirstLaunchCompleted", FirstLaunchCompleted }, { "EnableNotifications", EnableNotifications }, { "AutoCheckUpdates", AutoCheckUpdates }, { "AutoInstallUpdates", AutoInstallUpdates }, { "QuarantineRetentionDays", QuarantineRetentionDays },
                    { "ScheduledCleanupEnabled", ScheduledCleanupEnabled }, { "ScheduledCleanupFrequency", ScheduledCleanupFrequency },
                    { "ScheduledCleanupCategories", ScheduledCleanupCategories }, { "ScheduledCleanupMaxSizeMB", ScheduledCleanupMaxSizeMB },
                    { "WindowWidth", WindowWidth }, { "WindowHeight", WindowHeight }, { "WindowLeft", WindowLeft }, { "WindowTop", WindowTop }, { "WindowMaximized", WindowMaximized },
                    { "ShowDesktopWidget", ShowDesktopWidget }, { "WidgetLeft", WidgetLeft }, { "WidgetTop", WidgetTop }
                }));
                if (File.Exists(AppPaths.SettingsFile))
                {
                    try { File.Replace(tmp, AppPaths.SettingsFile, null); }
                    catch (IOException) { File.Copy(tmp, AppPaths.SettingsFile, true); File.Delete(tmp); }
                }
                else
                {
                    File.Move(tmp, AppPaths.SettingsFile);
                }
            }
            catch { }
        }
    }
}
