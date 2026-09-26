using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace UninstallerPro
{
    public static class AppPaths
    {
        public static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UninstallerPro");
        public static readonly string SettingsFile = Path.Combine(DataDir, "settings.json");
        public static readonly string LogFile = Path.Combine(DataDir, "Log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");

        public static void EnsureDataDir()
        {
            if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
        }
    }

    public static class Logger
    {
        public static void Log(string line)
        {
            try
            {
                AppPaths.EnsureDataDir();
                File.AppendAllText(AppPaths.LogFile, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + line + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        // מוחק אוטומטית קבצי לוג ישנים (נקרא פעם בהפעלה) - אותו עיקרון בטיחות
        // כמו ה-Quarantine: לא לתת לקבצי bookkeeping לגדול ללא הגבלה לאורך זמן.
        public static void PurgeOldLogs(int days)
        {
            try
            {
                if (!Directory.Exists(AppPaths.DataDir)) return;
                var cutoff = DateTime.Now.AddDays(-days);
                foreach (var f in Directory.GetFiles(AppPaths.DataDir, "Log_*.txt"))
                {
                    if (string.Equals(f, AppPaths.LogFile, StringComparison.OrdinalIgnoreCase)) continue;
                    try { if (File.GetLastWriteTime(f) < cutoff) File.Delete(f); } catch { }
                }
            }
            catch { }
        }
    }

    public static class SafetyGuard
    {
        private static readonly HashSet<string> ProtectedExactNames = new HashSet<string>(new[]
        {
            "microsoft","common files","windows","windowsapps","windows nt","google","adobe",
            "intel","amd","nvidia corporation","realtek","mozilla","internet explorer","msbuild",
            "reference assemblies","package cache","packages","systemapps",
            "installshield installation information","windows defender","windowspowershell",
            "dotnet","microsoft.net","modifiablewindowsapps","classes","policies","clients",
            "wow6432node","uninstall","clsid","interface","typelib","appid","mime",
            "default","public","all users"
        }, StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> ProtectedFullPaths = BuildProtectedFullPaths();

        private static HashSet<string> BuildProtectedFullPaths()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] candidates = new[]
            {
                Environment.GetEnvironmentVariable("SystemDrive"),
                Environment.GetEnvironmentVariable("windir"),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            };
            foreach (var c in candidates)
            {
                if (!string.IsNullOrEmpty(c)) set.Add(c.TrimEnd('\\'));
            }
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                var usersDir = Directory.GetParent(appData);
                if (usersDir != null && usersDir.Parent != null) set.Add(usersDir.Parent.FullName.TrimEnd('\\'));
            }
            return set;
        }

        public static bool IsProtectedName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            return ProtectedExactNames.Contains(name.Trim());
        }

        public static bool IsProtectedPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return true;
            var norm = path.TrimEnd('\\');
            if (ProtectedFullPaths.Contains(norm)) return true;
            if (norm.Length <= 3) return true;
            return false;
        }

        public static void RemoveEmptyParentChain(string path)
        {
            var current = path;
            while (!string.IsNullOrEmpty(current) && Directory.Exists(current) && !IsProtectedPath(current))
            {
                try
                {
                    if (Directory.GetFileSystemEntries(current).Length > 0) break;
                }
                catch { break; }
                var parent = Directory.GetParent(current);
                try
                {
                    Directory.Delete(current);
                    Logger.Log("נמחקה תיקייה ריקה: " + current);
                }
                catch { break; }
                current = parent != null ? parent.FullName : null;
            }
        }

        public static List<string> GetEmptyDirectoriesRecursive(string root)
        {
            var result = new List<string>();
            TestEmptyDir(root, result);
            return result;
        }

        private static bool TestEmptyDir(string dir, List<string> result)
        {
            string[] files;
            string[] subdirs;
            try
            {
                files = Directory.GetFiles(dir);
                subdirs = Directory.GetDirectories(dir);
            }
            catch { return false; }
            if (files.Length > 0) return false;
            bool allSubEmpty = true;
            foreach (var sd in subdirs)
            {
                if (TestEmptyDir(sd, result)) result.Add(sd);
                else allSubEmpty = false;
            }
            return allSubEmpty;
        }
    }

    public static class NameMatcher
    {
        private static readonly HashSet<string> StopWords = new HashSet<string>(new[]
        {
            "inc","corp","corporation","ltd","llc","co","company","software","technologies",
            "technology","the","and","group","systems","solutions","international","gmbh","sa","srl"
        }, StringComparer.OrdinalIgnoreCase);

        public static List<string> GetTokens(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            var clean = Regex.Replace(text, @"[^\p{L}\p{Nd}]+", " ").ToLowerInvariant();
            foreach (var t in clean.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (t.Length >= 3 && !StopWords.Contains(t) && !result.Contains(t)) result.Add(t);
            }
            return result;
        }

        // returns match reason, or null if no match
        public static string TestNameMatch(string folderName, string displayName, List<string> nameTokens)
        {
            var n = folderName.ToLowerInvariant();
            if (SafetyGuard.IsProtectedName(n)) return null;
            var noSpace = Regex.Replace(n, @"[^\p{L}\p{Nd}]", "");
            var fullNoSpace = Regex.Replace(displayName ?? "", @"[^\p{L}\p{Nd}]", "").ToLowerInvariant();
            if (fullNoSpace.Length >= 4 && noSpace == fullNoSpace) return I18n.T("residual_reason_exact_name");
            foreach (var t in nameTokens)
            {
                if (t.Length >= 4 && n.Contains(t)) return string.Format(I18n.T("residual_reason_word_match"), t);
            }
            return null;
        }

        public static bool TestPublisherMatch(string folderName, List<string> pubTokens)
        {
            var n = folderName.ToLowerInvariant();
            if (SafetyGuard.IsProtectedName(n)) return false;
            foreach (var t in pubTokens)
            {
                if (t.Length >= 4 && n.Contains(t)) return true;
            }
            return false;
        }
    }

    // מיצוי נתיב ה-exe מתוך שורת פקודה (עם/בלי מרכאות, עם/בלי ארגומנטים) -
    // אותה לוגיקה שכבר קיימת פרטית ב-RegistryCleanerData/SmartWizard, כאן
    // כשירות משותפת חדשה לשימוש נוסף (StartupData) בלי לגעת בשני המקומות
    // הקיימים ולהוסיף סיכון שינוי לקוד עובד.
    public static class CommandLineUtil
    {
        public static string ExtractExePath(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            command = command.Trim();
            if (command.StartsWith("\""))
            {
                int end = command.IndexOf('"', 1);
                return end > 0 ? command.Substring(1, end - 1) : null;
            }
            int exeIdx = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIdx > 0) return command.Substring(0, exeIdx + 4);
            var firstToken = command.Split(' ')[0];
            return string.IsNullOrWhiteSpace(firstToken) ? null : firstToken;
        }
    }

    public static class RegistryUtil
    {
        public static RegistryKey OpenBase(RegistryHive hive)
        {
            return RegistryKey.OpenBaseKey(hive, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default);
        }

        public static bool DeleteSubKeyTree(RegistryHive hive, string subKeyPath)
        {
            try
            {
                using (var baseKey = OpenBase(hive))
                {
                    baseKey.DeleteSubKeyTree(subKeyPath, false);
                }
                return true;
            }
            catch { return false; }
        }

        public static bool SubKeyExists(RegistryHive hive, string subKeyPath)
        {
            try
            {
                using (var baseKey = OpenBase(hive))
                using (var k = baseKey.OpenSubKey(subKeyPath))
                {
                    return k != null;
                }
            }
            catch { return false; }
        }
    }
}
