using System;
using System.Collections.Generic;
using System.Globalization;

namespace UninstallerPro
{
    public enum SecurityIssueKind { DefenderUnavailable, DefenderRtpOff, NoRecentScan, FirewallProfileOff, UpdateStale }

    public class SecurityIssue
    {
        public SecurityIssueKind Kind;
        public string TitleKey;
        public string DetailText;
        public string FixButtonKey;
        public string ProfileName;
    }

    // אשף אבטחה: לא מציג "סטטוס" כללי (זה תפקיד עמוד מרכז האבטחה) - מציג רק
    // בעיות קונקרטיות שנמצאו, עם כפתור תיקון מתאים לכל אחת. אם הכל תקין,
    // האשף פשוט אומר את זה ולא ממציא בעיות כדי להצדיק את קיומו.
    public static class SecurityWizard
    {
        public static List<SecurityIssue> Scan()
        {
            var issues = new List<SecurityIssue>();

            var defender = SecurityData.GetDefenderStatus();
            if (!defender.Available)
            {
                issues.Add(new SecurityIssue { Kind = SecurityIssueKind.DefenderUnavailable, TitleKey = "secwiz_defender_unavailable" });
            }
            else
            {
                if (!defender.RealTimeProtection)
                    issues.Add(new SecurityIssue { Kind = SecurityIssueKind.DefenderRtpOff, TitleKey = "secwiz_rtp_off", FixButtonKey = "secwiz_fix_open_security" });

                var scanText = defender.LastQuickScan ?? defender.LastFullScan;
                if ((DateTime.Now - ParseExactOrMin(scanText)).TotalDays > 14)
                    issues.Add(new SecurityIssue { Kind = SecurityIssueKind.NoRecentScan, TitleKey = "secwiz_no_recent_scan", FixButtonKey = "secwiz_fix_run_scan" });
            }

            foreach (var fw in SecurityData.GetFirewallStatus())
            {
                if (!fw.Enabled)
                    issues.Add(new SecurityIssue { Kind = SecurityIssueKind.FirewallProfileOff, TitleKey = "secwiz_firewall_off", DetailText = fw.Name, ProfileName = fw.Name, FixButtonKey = "secwiz_fix_enable_firewall" });
            }

            var wu = SecurityData.GetWindowsUpdateStatus();
            if ((DateTime.Now - ParseExactOrMin(wu.LastSearch)).TotalDays > 14)
                issues.Add(new SecurityIssue { Kind = SecurityIssueKind.UpdateStale, TitleKey = "secwiz_update_stale", FixButtonKey = "secwiz_fix_open_update" });

            return issues;
        }

        // כל תאריכי SecurityData מגיעים בפורמט הקבוע "dd/MM/yyyy HH:mm" (ExtractDateString) -
        // פרסינג גנרי לפי תרבות היה עלול להתבלבל בין יום לחודש.
        private static DateTime ParseExactOrMin(string text)
        {
            DateTime result;
            if (!string.IsNullOrEmpty(text) && DateTime.TryParseExact(text, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out result)) return result;
            return DateTime.MinValue;
        }
    }
}
