using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace UninstallerPro
{
    public class AppSettings
    {
        public string Theme = "Light";
        public bool ShowSystemComponents = false;
        public string Language = I18n.English;
        public string UpdateManifestUrl = "";
        public bool CreateRestorePoints = true;
        public bool FirstLaunchCompleted = false;
        public bool EnableNotifications = true;
        public bool AutoCheckUpdates = false;
        public int QuarantineRetentionDays = 7;

        // Scheduled automatic cleanup (added 4.10.0, see ScheduledCleanupData.cs) -
        // registers a Windows Task Scheduler task that runs "OptiGuard.exe
        // --auto-clean" unattended. Off by default: this is an opt-in
        // convenience feature, not something enabled behind the user's back.
        public bool ScheduledCleanupEnabled = false;
        public string ScheduledCleanupFrequency = "Weekly"; // Daily | Weekly | Monthly

        // Window state persistence (size/position/maximized) - see MainWindow.cs.
        // -1 sentinel means "never saved yet" -> MainWindow falls back to its
        // built-in default size/centered position instead of using these.
        public double WindowWidth = -1;
        public double WindowHeight = -1;
        public double WindowLeft = -1;
        public double WindowTop = -1;
        public bool WindowMaximized = false;

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(AppPaths.SettingsFile))
                {
                    var serializer = new JavaScriptSerializer();
                    var dict = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(AppPaths.SettingsFile));
                    var s = new AppSettings();
                    if (dict.ContainsKey("Theme") && dict["Theme"] != null) s.Theme = dict["Theme"].ToString();
                    if (dict.ContainsKey("ShowSystemComponents") && dict["ShowSystemComponents"] != null) s.ShowSystemComponents = Convert.ToBoolean(dict["ShowSystemComponents"]);
                    if (dict.ContainsKey("Language") && dict["Language"] != null) s.Language = dict["Language"].ToString();
                    if (dict.ContainsKey("UpdateManifestUrl") && dict["UpdateManifestUrl"] != null) s.UpdateManifestUrl = dict["UpdateManifestUrl"].ToString();
                    if (dict.ContainsKey("CreateRestorePoints") && dict["CreateRestorePoints"] != null) s.CreateRestorePoints = Convert.ToBoolean(dict["CreateRestorePoints"]);
                    if (dict.ContainsKey("FirstLaunchCompleted") && dict["FirstLaunchCompleted"] != null) s.FirstLaunchCompleted = Convert.ToBoolean(dict["FirstLaunchCompleted"]);
                    if (dict.ContainsKey("EnableNotifications") && dict["EnableNotifications"] != null) s.EnableNotifications = Convert.ToBoolean(dict["EnableNotifications"]);
                    if (dict.ContainsKey("AutoCheckUpdates") && dict["AutoCheckUpdates"] != null) s.AutoCheckUpdates = Convert.ToBoolean(dict["AutoCheckUpdates"]);
                    if (dict.ContainsKey("QuarantineRetentionDays") && dict["QuarantineRetentionDays"] != null) s.QuarantineRetentionDays = Convert.ToInt32(dict["QuarantineRetentionDays"]);
                    if (dict.ContainsKey("ScheduledCleanupEnabled") && dict["ScheduledCleanupEnabled"] != null) s.ScheduledCleanupEnabled = Convert.ToBoolean(dict["ScheduledCleanupEnabled"]);
                    if (dict.ContainsKey("ScheduledCleanupFrequency") && dict["ScheduledCleanupFrequency"] != null) s.ScheduledCleanupFrequency = dict["ScheduledCleanupFrequency"].ToString();
                    if (dict.ContainsKey("WindowWidth") && dict["WindowWidth"] != null) s.WindowWidth = Convert.ToDouble(dict["WindowWidth"]);
                    if (dict.ContainsKey("WindowHeight") && dict["WindowHeight"] != null) s.WindowHeight = Convert.ToDouble(dict["WindowHeight"]);
                    if (dict.ContainsKey("WindowLeft") && dict["WindowLeft"] != null) s.WindowLeft = Convert.ToDouble(dict["WindowLeft"]);
                    if (dict.ContainsKey("WindowTop") && dict["WindowTop"] != null) s.WindowTop = Convert.ToDouble(dict["WindowTop"]);
                    if (dict.ContainsKey("WindowMaximized") && dict["WindowMaximized"] != null) s.WindowMaximized = Convert.ToBoolean(dict["WindowMaximized"]);
                    return s;
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                AppPaths.EnsureDataDir();
                var serializer = new JavaScriptSerializer();
                File.WriteAllText(AppPaths.SettingsFile, serializer.Serialize(new Dictionary<string, object>
                {
                    { "Theme", Theme }, { "ShowSystemComponents", ShowSystemComponents }, { "Language", Language }, { "UpdateManifestUrl", UpdateManifestUrl }, { "CreateRestorePoints", CreateRestorePoints }, { "FirstLaunchCompleted", FirstLaunchCompleted }, { "EnableNotifications", EnableNotifications }, { "AutoCheckUpdates", AutoCheckUpdates }, { "QuarantineRetentionDays", QuarantineRetentionDays },
                    { "ScheduledCleanupEnabled", ScheduledCleanupEnabled }, { "ScheduledCleanupFrequency", ScheduledCleanupFrequency },
                    { "WindowWidth", WindowWidth }, { "WindowHeight", WindowHeight }, { "WindowLeft", WindowLeft }, { "WindowTop", WindowTop }, { "WindowMaximized", WindowMaximized }
                }));
            }
            catch { }
        }
    }
}
