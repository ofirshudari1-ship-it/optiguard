using System;
using System.IO;
using System.Management;

namespace UninstallerPro
{
    // יצירת נקודת שחזור לפני פעולות ניקוי מסוכנות. זהו רשת ביטחון שרוב כלי
    // הניקוי/הסרה בשוק לא יוצרים אוטומטית - וזו הסיבה המרכזית לתלונות נפוצות
    // על מחיקת ערכי רישום/קבצים ש"שברו" את המערכת בלי דרך חזרה.
    // הפעולה best-effort לגמרי: אם System Restore כבוי, מוגבל בתדירות ע"י
    // Windows, או נכשל מכל סיבה - היא רק נרשמת ללוג ולעולם לא חוסמת את הפעולה.
    //
    // יצירת נקודת שחזור אמיתית (WMI/VSS) יכולה לקחת בין כמה שניות לדקה שלמה -
    // אם היינו קוראים לזה בכל ניקוי קטן, כל פעולה הייתה מרגישה תקועה. לכן
    // מדלגים אם כבר נוצרה נקודת שחזור על ידינו ב-N השעות האחרונות; יש כבר
    // רשת ביטחון תקפה מהפעם הקודמת.
    public static class RestorePoint
    {
        public static bool Enabled = true;
        private const int ThrottleHours = 4;
        private static readonly string ThrottleFile = Path.Combine(AppPaths.DataDir, "last_restore_point.txt");

        private static bool RecentlyCreated()
        {
            try
            {
                if (!File.Exists(ThrottleFile)) return false;
                DateTime last;
                if (!DateTime.TryParse(File.ReadAllText(ThrottleFile), out last)) return false;
                return (DateTime.Now - last).TotalHours < ThrottleHours;
            }
            catch { return false; }
        }

        private static void MarkCreated()
        {
            try { AppPaths.EnsureDataDir(); File.WriteAllText(ThrottleFile, DateTime.Now.ToString("o")); }
            catch { }
        }

        public static bool Create(string description)
        {
            if (!Enabled) return false;
            if (RecentlyCreated())
            {
                Logger.Log("System Restore Point skipped (one already created within the last " + ThrottleHours + "h): " + description);
                return true;
            }
            try
            {
                var scope = new ManagementScope(@"\\.\root\default");
                var path = new ManagementPath("SystemRestore");
                using (var mc = new ManagementClass(scope, path, null))
                using (var inParams = mc.GetMethodParameters("CreateRestorePoint"))
                {
                    inParams["Description"] = description;
                    inParams["RestorePointType"] = 12; // MODIFY_SETTINGS
                    inParams["EventType"] = 100; // BEGIN_SYSTEM_CHANGE
                    using (var outParams = mc.InvokeMethod("CreateRestorePoint", inParams, null))
                    {
                        var ret = outParams != null ? Convert.ToUInt32(outParams["ReturnValue"]) : 1u;
                        if (ret == 0)
                        {
                            Logger.Log("System Restore Point created: " + description);
                            MarkCreated();
                            return true;
                        }
                        Logger.Log("System Restore Point not created (code " + ret + ") - continuing without it.");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("System Restore Point creation failed (continuing): " + ex.Message);
                return false;
            }
        }
    }
}
