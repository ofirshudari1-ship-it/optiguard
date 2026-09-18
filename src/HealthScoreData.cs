using System;
using System.Collections.Generic;
using System.Linq;

namespace UninstallerPro
{
    public class HealthIssue
    {
        public string TitleKey;
        public string Detail;
        public int Penalty;
        public string NavTarget;
    }

    public class HealthScoreResult
    {
        public int Score;
        public List<HealthIssue> Issues = new List<HealthIssue>();
    }

    // ציון בריאות מחשב: לא מדידה שרירותית - מצרף תוצאות אמיתיות מהמודולים
    // הקיימים (Defender, חומת אש, זבל, הפעלה אוטומטית, בריאות דיסק, פרטיות)
    // למספר אחד שמספר את הסיפור במבט אחד, עם קישור ישיר לכל בעיה שנמצאה.
    public static class HealthScoreData
    {
        public static HealthScoreResult Compute()
        {
            var result = new HealthScoreResult { Score = 100 };

            try
            {
                var defender = SecurityData.GetDefenderStatus();
                if (defender.Available && !defender.RealTimeProtection)
                {
                    Deduct(result, 20, "health_issue_rtp_off", null, "seccenter");
                }
                var fw = SecurityData.GetFirewallStatus();
                var offProfiles = fw.Where(p => !p.Enabled).Select(p => p.Name).ToList();
                if (offProfiles.Count > 0)
                {
                    Deduct(result, 12, "health_issue_firewall_off", string.Join(", ", offProfiles), "seccenter");
                }
            }
            catch (Exception ex) { Logger.Log("HealthScore: security check failed: " + ex.Message); }

            try
            {
                var junk = JunkCleanerData.ScanAll();
                long totalJunk = junk.Sum(j => j.SizeBytes);
                if (totalJunk > 5L * 1024 * 1024 * 1024) Deduct(result, 15, "health_issue_junk_high", JunkCleanerData.FormatSize(totalJunk), "junk");
                else if (totalJunk > 1L * 1024 * 1024 * 1024) Deduct(result, 8, "health_issue_junk_medium", JunkCleanerData.FormatSize(totalJunk), "junk");
            }
            catch (Exception ex) { Logger.Log("HealthScore: junk check failed: " + ex.Message); }

            try
            {
                var startup = StartupData.GetStartupItems();
                int enabledCount = startup.Count(s => s.Enabled);
                if (enabledCount > 15) Deduct(result, 10, "health_issue_startup_heavy", enabledCount.ToString(), "startup");
            }
            catch (Exception ex) { Logger.Log("HealthScore: startup check failed: " + ex.Message); }

            try
            {
                var disks = DiskHealthData.GetDrives();
                foreach (var d in disks)
                {
                    if (d.UsedPercent >= 95) Deduct(result, 10, "health_issue_disk_full", d.Drive, "disk");
                    if (!d.HealthOk) Deduct(result, 15, "health_issue_disk_unhealthy", d.Drive, "disk");
                }
            }
            catch (Exception ex) { Logger.Log("HealthScore: disk check failed: " + ex.Message); }

            try
            {
                var privacy = PrivacyData.GetAll();
                var exposedKeys = new HashSet<string> { "advertising_id", "tailored_experiences", "diagnostic_data", "activity_history", "app_suggestions" };
                int exposedOn = privacy.Count(t => exposedKeys.Contains(t.Key) && t.CurrentState);
                if (exposedOn >= 4) Deduct(result, 8, "health_issue_privacy_exposed", exposedOn.ToString(), "privacy");
            }
            catch (Exception ex) { Logger.Log("HealthScore: privacy check failed: " + ex.Message); }

            try
            {
                var ghosts = RegistryCleanerData.Scan();
                if (ghosts.Count >= 5) Deduct(result, 6, "health_issue_registry_ghosts", ghosts.Count.ToString(), "regclean");
            }
            catch (Exception ex) { Logger.Log("HealthScore: registry check failed: " + ex.Message); }

            try
            {
                var extensions = ExtensionsData.GetAllExtensions();
                int highRisk = extensions.Count(e => e.RiskLevel == "high");
                if (highRisk > 0) Deduct(result, 10, "health_issue_risky_extensions", highRisk.ToString(), "ext");
            }
            catch (Exception ex) { Logger.Log("HealthScore: extensions check failed: " + ex.Message); }

            result.Score = Math.Max(0, Math.Min(100, result.Score));
            return result;
        }

        private static void Deduct(HealthScoreResult result, int penalty, string titleKey, string detail, string navTarget)
        {
            result.Score -= penalty;
            result.Issues.Add(new HealthIssue { TitleKey = titleKey, Detail = detail, Penalty = penalty, NavTarget = navTarget });
        }
    }
}
