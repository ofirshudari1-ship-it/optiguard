using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

namespace UninstallerPro
{
    public class UpdateInfo
    {
        public bool Available;
        public string LatestVersion;
        public string DownloadUrl;
        public string Notes;
        public string Error;
    }

    // בדיקת עדכון גרסה מרוחקת. דורש שיציבו אי-שם קובץ manifest.json ציבורי בפורמט:
    // { "version": "3.3", "url": "https://.../OptiGuard-Setup-4.9.0.exe", "notes": "מה חדש..." }
    // ללא כתובת מוגדרת (Settings) הפיצ'ר פשוט לא פעיל - לא בודק כלום ולא מציג שגיאות.
    public static class UpdateChecker
    {
        public static UpdateInfo Check(string manifestUrl, string currentVersion)
        {
            var result = new UpdateInfo();
            if (string.IsNullOrWhiteSpace(manifestUrl))
            {
                result.Error = "not_configured";
                return result;
            }
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "OptiGuard-UpdateChecker");
                    var json = client.DownloadString(manifestUrl);
                    var serializer = new JavaScriptSerializer();
                    var dict = serializer.Deserialize<Dictionary<string, object>>(json);
                    var latest = dict.ContainsKey("version") ? dict["version"] as string : null;
                    var url = dict.ContainsKey("url") ? dict["url"] as string : null;
                    var notes = dict.ContainsKey("notes") ? dict["notes"] as string : null;
                    result.LatestVersion = latest;
                    result.DownloadUrl = url;
                    result.Notes = notes;
                    result.Available = !string.IsNullOrEmpty(latest) && IsNewer(latest, currentVersion);
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                Logger.Log("Update check failed: " + ex.Message);
            }
            return result;
        }

        private static bool IsNewer(string latest, string current)
        {
            Version vLatest, vCurrent;
            if (Version.TryParse(NormalizeVersion(latest), out vLatest) && Version.TryParse(NormalizeVersion(current), out vCurrent))
                return vLatest > vCurrent;
            return string.CompareOrdinal(latest, current) > 0;
        }

        private static string NormalizeVersion(string v)
        {
            if (string.IsNullOrEmpty(v)) return "0.0";
            var parts = v.Split('.');
            return parts.Length == 1 ? v + ".0" : v;
        }

        public static bool DownloadAndLaunchInstaller(string url, out string error)
        {
            error = null;
            try
            {
                var tempPath = Path.Combine(Path.GetTempPath(), "OptiGuard-Update-" + Guid.NewGuid().ToString("N") + ".exe");
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "OptiGuard-UpdateChecker");
                    client.DownloadFile(url, tempPath);
                }
                Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
                Logger.Log("Downloaded update installer and launched: " + tempPath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Logger.Log("Update download failed: " + ex.Message);
                return false;
            }
        }
    }
}
