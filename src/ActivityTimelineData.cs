using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UninstallerPro
{
    public class TimelineEvent
    {
        public DateTime When;
        public string Kind;
        public string TitleKey;
        public string Detail;
        public string ExtraId;

        public string TitleText { get { return I18n.T(TitleKey); } }
        public string WhenText { get { return When.ToString("dd/MM/yyyy HH:mm"); } }
    }

    // ציר זמן אמיתי: לא לוג טקסט גולמי, אלא איסוף מובנה של כל מה שהתוכנה כבר
    // רושמת בכל מקרה (מניפסט ההסגר, טביעות אצבע התקנה, גיבויי רישום) למסך
    // אחד כרונולוגי - שקיפות מלאה על מה שהתוכנה עשתה, בלי צורך במנגנון לוג חדש.
    public static class ActivityTimelineData
    {
        public static List<TimelineEvent> GetRecent(int max = 200)
        {
            var events = new List<TimelineEvent>();

            try
            {
                foreach (var batch in Quarantine.GetBatchSummaries())
                {
                    events.Add(new TimelineEvent
                    {
                        When = batch.When,
                        Kind = "cleanup",
                        TitleKey = "timeline_cleanup",
                        Detail = string.Format(I18n.T("timeline_cleanup_detail"), batch.Count),
                        ExtraId = batch.BatchId
                    });
                }
            }
            catch (Exception ex) { Logger.Log("Timeline: quarantine read failed: " + ex.Message); }

            try
            {
                foreach (var fp in InstallMonitor.GetAll())
                {
                    DateTime dt;
                    if (DateTime.TryParse(fp.CreatedAt, out dt))
                    {
                        events.Add(new TimelineEvent
                        {
                            When = dt,
                            Kind = "fingerprint",
                            TitleKey = "timeline_fingerprint",
                            Detail = fp.Name
                        });
                    }
                }
            }
            catch (Exception ex) { Logger.Log("Timeline: install monitor read failed: " + ex.Message); }

            try
            {
                var regBackupDir = Path.Combine(AppPaths.DataDir, "RegistryBackups");
                if (Directory.Exists(regBackupDir))
                {
                    foreach (var f in Directory.GetFiles(regBackupDir, "backup_*.reg"))
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        var tsPart = name.Length > "backup_".Length ? name.Substring("backup_".Length) : null;
                        DateTime dt;
                        if (tsPart != null && DateTime.TryParseExact(tsPart, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt))
                        {
                            events.Add(new TimelineEvent
                            {
                                When = dt,
                                Kind = "registry_backup",
                                TitleKey = "timeline_registry_backup",
                                Detail = Path.GetFileName(f)
                            });
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.Log("Timeline: registry backups read failed: " + ex.Message); }

            return events.OrderByDescending(e => e.When).Take(max).ToList();
        }
    }
}
