using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace UninstallerPro
{
    public class DefenderStatus
    {
        public bool ServiceEnabled;
        public bool RealTimeProtection;
        public string SignatureLastUpdated;
        public string LastQuickScan;
        public string LastFullScan;
        public bool Available;
        public string Error;
    }

    public class FirewallProfileStatus
    {
        public string Name;
        public bool Enabled;
    }

    // אבטחה: לא בונים מנוע אנטי-וירוס עצמאי (זה דורש מעבדת מחקר תוכנות זדוניות
    // שלמה ועדכוני חתימות מתמשכים - לא ריאלי כתוסף לכלי הסרת תוכנות). במקום
    // זאת, חושפים בצורה נוחה את המצב האמיתי של מנועי ההגנה המובנים של
    // Windows (Defender, חומת האש) ומאפשרים להפעיל סריקה אמיתית איתם.
    public static class SecurityData
    {
        private static Tuple<int, string> RunPS(string command, int timeoutMs = 30000)
        {
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"" + command.Replace("\"", "\\\"") + "\"")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8
            };
            var stdout = new StringBuilder();
            using (var p = new Process { StartInfo = psi })
            {
                p.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) Logger.Log("security ps stderr: " + e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return Tuple.Create(-1, ""); }
                return Tuple.Create(p.ExitCode, stdout.ToString());
            }
        }

        public static DefenderStatus GetDefenderStatus()
        {
            var result = new DefenderStatus();
            var output = RunPS("Get-MpComputerStatus | Select-Object AMServiceEnabled,RealTimeProtectionEnabled,AntivirusSignatureLastUpdated,QuickScanEndTime,FullScanEndTime | ConvertTo-Json -Compress");
            if (string.IsNullOrWhiteSpace(output.Item2)) { result.Error = "unavailable"; return result; }
            try
            {
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                var dict = serializer.Deserialize<Dictionary<string, object>>(output.Item2.Trim());
                result.Available = true;
                result.ServiceEnabled = dict.ContainsKey("AMServiceEnabled") && Convert.ToBoolean(dict["AMServiceEnabled"]);
                result.RealTimeProtection = dict.ContainsKey("RealTimeProtectionEnabled") && Convert.ToBoolean(dict["RealTimeProtectionEnabled"]);
                result.SignatureLastUpdated = ExtractDateString(dict, "AntivirusSignatureLastUpdated");
                result.LastQuickScan = ExtractDateString(dict, "QuickScanEndTime");
                result.LastFullScan = ExtractDateString(dict, "FullScanEndTime");
            }
            catch (Exception ex) { result.Error = ex.Message; }
            return result;
        }

        private static string ExtractDateString(Dictionary<string, object> dict, string key)
        {
            if (!dict.ContainsKey(key) || dict[key] == null) return null;
            // JavaScriptSerializer recognizes the "/Date(ticks)/" pattern natively and
            // already hands back a real DateTime here in most cases - only fall back to
            // regex-parsing the raw "/Date(ticks)/" string if it didn't. Formatting is
            // always fixed to "dd/MM/yyyy HH:mm" (never seconds) so downstream exact-format
            // parsing (e.g. Security Wizard staleness checks) stays reliable.
            if (dict[key] is DateTime) return ((DateTime)dict[key]).ToString("dd/MM/yyyy HH:mm");
            var raw = dict[key].ToString();
            var m = System.Text.RegularExpressions.Regex.Match(raw, @"/Date\((\d+)\)/");
            if (m.Success)
            {
                long ms = long.Parse(m.Groups[1].Value);
                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ms).ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            }
            return raw;
        }

        public static List<FirewallProfileStatus> GetFirewallStatus()
        {
            var result = new List<FirewallProfileStatus>();
            var output = RunPS("Get-NetFirewallProfile | Select-Object Name,Enabled | ConvertTo-Json -Compress");
            if (string.IsNullOrWhiteSpace(output.Item2)) return result;
            try
            {
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                object parsed = serializer.DeserializeObject(output.Item2.Trim());
                var list = new List<object>();
                if (parsed is object[]) list.AddRange((object[])parsed);
                else if (parsed is System.Collections.ArrayList) list.AddRange(((System.Collections.ArrayList)parsed).Cast<object>());
                else if (parsed is Dictionary<string, object>) list.Add(parsed);
                foreach (var o in list)
                {
                    var d = o as Dictionary<string, object>;
                    if (d == null) continue;
                    result.Add(new FirewallProfileStatus { Name = d["Name"] as string, Enabled = Convert.ToBoolean(d["Enabled"]) });
                }
            }
            catch (Exception ex) { Logger.Log("Firewall status parse error: " + ex.Message); }
            return result;
        }

        public class WindowsUpdateStatus
        {
            public string LastSearch;
            public string LastInstall;
        }

        public static WindowsUpdateStatus GetWindowsUpdateStatus()
        {
            var result = new WindowsUpdateStatus();
            var output = RunPS("$r=(New-Object -ComObject Microsoft.Update.AutoUpdate).Results; " +
                "[PSCustomObject]@{LastSearch=$r.LastSearchSuccessDate; LastInstall=$r.LastInstallationSuccessDate} | ConvertTo-Json -Compress");
            if (string.IsNullOrWhiteSpace(output.Item2)) return result;
            try
            {
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                var dict = serializer.Deserialize<Dictionary<string, object>>(output.Item2.Trim());
                result.LastSearch = ExtractDateString(dict, "LastSearch");
                result.LastInstall = ExtractDateString(dict, "LastInstall");
            }
            catch (Exception ex) { Logger.Log("Windows Update status parse error: " + ex.Message); }
            return result;
        }

        public static bool SetFirewallProfileEnabled(string profileName, bool enabled)
        {
            var safe = new string(profileName.Where(char.IsLetter).ToArray());
            var result = RunPS("Set-NetFirewallProfile -Profile '" + safe + "' -Enabled " + (enabled ? "True" : "False"));
            return result.Item1 == 0;
        }

        public static void OpenWindowsSecurityApp()
        {
            try { Process.Start(new ProcessStartInfo("windowsdefender:") { UseShellExecute = true }); }
            catch (Exception ex) { Logger.Log("Failed to open Windows Security: " + ex.Message); }
        }

        public static void OpenWindowsUpdateSettings()
        {
            try { Process.Start(new ProcessStartInfo("ms-settings:windowsupdate") { UseShellExecute = true }); }
            catch (Exception ex) { Logger.Log("Failed to open Windows Update settings: " + ex.Message); }
        }

        public static bool RunQuickScan(out string error)
        {
            error = null;
            try
            {
                var platformDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows Defender\Platform");
                if (!Directory.Exists(platformDir)) { error = "not_found"; return false; }
                var latest = Directory.GetDirectories(platformDir).OrderByDescending(d => d).FirstOrDefault();
                if (latest == null) { error = "not_found"; return false; }
                var exe = Path.Combine(latest, "MpCmdRun.exe");
                if (!File.Exists(exe)) { error = "not_found"; return false; }

                var psi = new ProcessStartInfo(exe, "-Scan -ScanType 1")
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit(15 * 60 * 1000);
                    Logger.Log("Defender quick scan exit code: " + p.ExitCode);
                    return p.ExitCode == 0;
                }
            }
            catch (Exception ex) { error = ex.Message; Logger.Log("Defender scan failed: " + ex.Message); return false; }
        }
    }
}
