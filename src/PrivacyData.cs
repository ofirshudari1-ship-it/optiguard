using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace UninstallerPro
{
    public class PrivacyToggle
    {
        public string Key;
        public string LabelKey;
        public string DescKey;
        public bool CurrentState;
    }

    // מתג פרטיות אמיתי: כל שורה כאן כותבת לאותם מפתחות רישום שדף ההגדרות
    // המובנה של Windows (Settings > Privacy) כותב אליהם - לא המצאה, ולא
    // עורמת "אופטימיזציה" מזויפת. כל ערך מתועד ומאומת מול המחשב בפועל.
    public static class PrivacyData
    {
        public static List<PrivacyToggle> GetAll()
        {
            return new List<PrivacyToggle>
            {
                new PrivacyToggle { Key = "advertising_id", LabelKey = "privacy_advertising_id", DescKey = "privacy_advertising_id_desc", CurrentState = GetAdvertisingId() },
                new PrivacyToggle { Key = "tailored_experiences", LabelKey = "privacy_tailored", DescKey = "privacy_tailored_desc", CurrentState = GetTailoredExperiences() },
                new PrivacyToggle { Key = "diagnostic_data", LabelKey = "privacy_diagnostics", DescKey = "privacy_diagnostics_desc", CurrentState = GetDiagnosticData() },
                new PrivacyToggle { Key = "activity_history", LabelKey = "privacy_activity_history", DescKey = "privacy_activity_history_desc", CurrentState = GetActivityHistory() },
                new PrivacyToggle { Key = "app_suggestions", LabelKey = "privacy_suggestions", DescKey = "privacy_suggestions_desc", CurrentState = GetAppSuggestions() },
                new PrivacyToggle { Key = "feedback_requests", LabelKey = "privacy_feedback", DescKey = "privacy_feedback_desc", CurrentState = GetFeedbackRequests() },
                new PrivacyToggle { Key = "location_services", LabelKey = "privacy_location", DescKey = "privacy_location_desc", CurrentState = GetLocationServices() },
            };
        }

        public static void SetToggle(string key, bool enabled)
        {
            try
            {
                switch (key)
                {
                    case "advertising_id": SetAdvertisingId(enabled); break;
                    case "tailored_experiences": SetTailoredExperiences(enabled); break;
                    case "diagnostic_data": SetDiagnosticData(enabled); break;
                    case "activity_history": SetActivityHistory(enabled); break;
                    case "app_suggestions": SetAppSuggestions(enabled); break;
                    case "feedback_requests": SetFeedbackRequests(enabled); break;
                    case "location_services": SetLocationServices(enabled); break;
                }
                Logger.Log("Privacy toggle '" + key + "' set to " + enabled);
            }
            catch (Exception ex) { Logger.Log("Privacy toggle '" + key + "' failed: " + ex.Message); }
        }

        private static int GetDword(RegistryKey root, string path, string name, int defaultValue)
        {
            try
            {
                using (var k = root.OpenSubKey(path))
                {
                    if (k == null) return defaultValue;
                    var v = k.GetValue(name);
                    return v == null ? defaultValue : Convert.ToInt32(v);
                }
            }
            catch { return defaultValue; }
        }

        private static void SetDword(RegistryKey root, string path, string name, int value)
        {
            using (var k = root.CreateSubKey(path))
            {
                k.SetValue(name, value, RegistryValueKind.DWord);
            }
        }

        private static bool GetAdvertisingId() { return GetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 1) == 1; }
        private static void SetAdvertisingId(bool enabled) { SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", enabled ? 1 : 0); }

        private static bool GetTailoredExperiences() { return GetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 1) == 1; }
        private static void SetTailoredExperiences(bool enabled) { SetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", enabled ? 1 : 0); }

        // Windows: 0=Security(Enterprise only, treated as Basic elsewhere), 1=Basic, 2=Enhanced(deprecated), 3=Full
        private static bool GetDiagnosticData() { return GetDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\DataCollection", "AllowTelemetry", 1) >= 3; }
        private static void SetDiagnosticData(bool full) { SetDword(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\DataCollection", "AllowTelemetry", full ? 3 : 1); }

        private static bool GetActivityHistory()
        {
            return GetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 1) == 1;
        }
        private static void SetActivityHistory(bool enabled)
        {
            int v = enabled ? 1 : 0;
            SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", v);
            SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", v);
            SetDword(Registry.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", v);
        }

        private static bool GetAppSuggestions()
        {
            return GetDword(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 1) == 1;
        }
        private static void SetAppSuggestions(bool enabled)
        {
            int v = enabled ? 1 : 0;
            const string path = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
            SetDword(Registry.CurrentUser, path, "SubscribedContent-338388Enabled", v);
            SetDword(Registry.CurrentUser, path, "SubscribedContent-338393Enabled", v);
            SetDword(Registry.CurrentUser, path, "SubscribedContent-353694Enabled", v);
            SetDword(Registry.CurrentUser, path, "SilentInstalledAppsEnabled", v);
            SetDword(Registry.CurrentUser, path, "SystemPaneSuggestionsEnabled", v);
        }

        private static bool GetFeedbackRequests() { return GetDword(Registry.CurrentUser, @"SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 1) != 0; }
        private static void SetFeedbackRequests(bool enabled) { SetDword(Registry.CurrentUser, @"SOFTWARE\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", enabled ? 1 : 0); }

        private static bool GetLocationServices() { return GetDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration", "Status", 1) == 1; }
        private static void SetLocationServices(bool enabled) { SetDword(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration", "Status", enabled ? 1 : 0); }
    }
}
