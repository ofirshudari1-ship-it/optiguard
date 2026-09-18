using System;
using System.Collections.Generic;
using System.Management;

namespace UninstallerPro
{
    public class DriverIssue
    {
        public string DeviceName;
        public string DeviceClass;
        public int ErrorCode;
    }

    // בדיקת בריאות דרייברים - לא "עדכון דרייברים" אוטומטי. מחקר בשוק מראה
    // שכלי "עדכון דרייברים" הם קטגוריה מוכרת של scareware/PUP (מושכים
    // דרייברים ממאגרים לא מאומתים ומפחידים משתמשים לשלם) - ולכן זה חושף רק
    // את הסטטוס האמיתי מ-Device Manager ומפנה ל-Windows Update/היצרן, בלי
    // להוריד או להתקין שום דבר בעצמו.
    public static class DriverHealthData
    {
        public static List<DriverIssue> GetProblemDevices()
        {
            var result = new List<DriverIssue>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name,PNPClass,ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode != 0"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var name = mo["Name"] as string;
                        if (string.IsNullOrEmpty(name)) continue;
                        result.Add(new DriverIssue
                        {
                            DeviceName = name,
                            DeviceClass = mo["PNPClass"] as string ?? "-",
                            ErrorCode = mo["ConfigManagerErrorCode"] != null ? System.Convert.ToInt32(mo["ConfigManagerErrorCode"]) : 0
                        });
                    }
                }
            }
            catch (Exception ex) { Logger.Log("Driver health check failed: " + ex.Message); }
            return result;
        }
    }
}
