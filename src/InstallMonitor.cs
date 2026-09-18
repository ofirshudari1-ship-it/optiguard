using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace UninstallerPro
{
    // "מעקב התקנה" - חלופה חינמית לפיצ'ר ה-Pro הכבוד של Revo Uninstaller
    // (Real-Time Installation Monitor). לוקח תמונת מצב של הרישום והקבצים
    // הנפוצים לפני התקנה, ותמונה נוספת אחריה - ההפרש הוא בדיוק מה שהתוכנה
    // הוסיפה. זה מדויק פי כמה מהתאמת-שם היוריסטית, כי אין בו ניחוש בכלל.
    public class InstallFingerprint
    {
        public string Name { get; set; }
        public string CreatedAt { get; set; }
        public List<string> RegistryItems { get; set; } // "HiveName|subkeypath"
        public List<string> FileItems { get; set; }
    }

    public static class InstallMonitor
    {
        private static readonly string FingerprintDir = Path.Combine(AppPaths.DataDir, "InstallFingerprints");

        private static readonly Tuple<RegistryHive, string>[] RegistryRoots = new[]
        {
            Tuple.Create(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            Tuple.Create(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            Tuple.Create(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            Tuple.Create(RegistryHive.CurrentUser, "SOFTWARE"),
            Tuple.Create(RegistryHive.LocalMachine, "SOFTWARE"),
            Tuple.Create(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node"),
        };

        private static IEnumerable<string> FileSystemRoots()
        {
            var localLow = Path.Combine(Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)).FullName, "LocalLow");
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                localLow,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            };
            return roots.Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r)).Distinct();
        }

        // תמונת מצב מהירה - רק רמה עליונה (לא רקורסיבית) של המיקומים הנפוצים
        // ביותר שבהם תוכנות משתילות את עצמן. מספיק מהיר להרצה בלייב.
        public static HashSet<string> TakeSnapshot()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in RegistryRoots)
            {
                RegistryKey baseKey;
                try { baseKey = RegistryUtil.OpenBase(root.Item1); } catch { continue; }
                using (baseKey)
                using (var key = baseKey.OpenSubKey(root.Item2))
                {
                    if (key == null) continue;
                    foreach (var sub in key.GetSubKeyNames())
                    {
                        set.Add("R:" + root.Item1 + "|" + root.Item2 + "\\" + sub);
                    }
                }
            }
            foreach (var root in FileSystemRoots())
            {
                string[] entries;
                try { entries = Directory.GetFileSystemEntries(root); } catch { continue; }
                foreach (var e in entries) set.Add("F:" + e);
            }
            return set;
        }

        public class DiffResult
        {
            public List<string> NewRegistryPaths; // "HiveName|subkeypath"
            public List<string> NewFilePaths;
            public string SuggestedName;
        }

        public static DiffResult Diff(HashSet<string> before, HashSet<string> after)
        {
            var added = after.Where(x => !before.Contains(x)).ToList();
            var result = new DiffResult
            {
                NewRegistryPaths = added.Where(x => x.StartsWith("R:")).Select(x => x.Substring(2)).ToList(),
                NewFilePaths = added.Where(x => x.StartsWith("F:")).Select(x => x.Substring(2)).ToList()
            };

            // ניחוש שם התוכנה: מחפשים ערך הסרה חדש עם DisplayName אמיתי
            foreach (var regPath in result.NewRegistryPaths)
            {
                if (!regPath.Contains(@"\Uninstall\")) continue;
                var parts = regPath.Split(new[] { '|' }, 2);
                if (parts.Length != 2) continue;
                RegistryHive hive;
                if (!Enum.TryParse(parts[0], out hive)) continue;
                try
                {
                    using (var baseKey = RegistryUtil.OpenBase(hive))
                    using (var sub = baseKey.OpenSubKey(parts[1]))
                    {
                        var dn = sub != null ? sub.GetValue("DisplayName") as string : null;
                        if (!string.IsNullOrEmpty(dn)) { result.SuggestedName = dn; break; }
                    }
                }
                catch { }
            }
            return result;
        }

        private static string SafeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var clean = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return clean.Length > 100 ? clean.Substring(0, 100) : clean;
        }

        public static void SaveFingerprint(string name, List<string> registryItems, List<string> fileItems)
        {
            Directory.CreateDirectory(FingerprintDir);
            var fp = new InstallFingerprint { Name = name, CreatedAt = DateTime.Now.ToString("o"), RegistryItems = registryItems, FileItems = fileItems };
            var serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            File.WriteAllText(Path.Combine(FingerprintDir, SafeFileName(name) + ".json"), serializer.Serialize(fp));
            Logger.Log("Install fingerprint saved: " + name + " (" + registryItems.Count + " registry, " + fileItems.Count + " files)");
        }

        public static List<InstallFingerprint> GetAll()
        {
            var result = new List<InstallFingerprint>();
            if (!Directory.Exists(FingerprintDir)) return result;
            var serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            foreach (var file in Directory.GetFiles(FingerprintDir, "*.json"))
            {
                try { result.Add(serializer.Deserialize<InstallFingerprint>(File.ReadAllText(file))); }
                catch { }
            }
            return result.OrderByDescending(f => f.CreatedAt).ToList();
        }

        public static InstallFingerprint LoadFingerprintFor(string programName)
        {
            if (string.IsNullOrWhiteSpace(programName)) return null;
            var norm = Normalize(programName);
            return GetAll().FirstOrDefault(f => Normalize(f.Name) == norm);
        }

        private static string Normalize(string s)
        {
            return new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        }

        public static void Delete(string name)
        {
            try
            {
                var path = Path.Combine(FingerprintDir, SafeFileName(name) + ".json");
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        // ממיר Fingerprint שמור לרשימת ResidualItem מדויקת (לא ניחוש) לשילוב
        // בתוצאות סריקת השאריות הרגילה.
        public static List<ResidualItem> ToResidualItems(InstallFingerprint fp)
        {
            var items = new List<ResidualItem>();
            foreach (var r in fp.RegistryItems)
            {
                var parts = r.Split(new[] { '|' }, 2);
                if (parts.Length != 2) continue;
                RegistryHive hive;
                if (!Enum.TryParse(parts[0], out hive)) continue;
                if (!RegistryUtil.SubKeyExists(hive, parts[1])) continue;
                items.Add(new ResidualItem
                {
                    Type = ResidualType.Registry, Hive = hive, SubKeyPath = parts[1], Path = parts[1],
                    DisplayPath = ProgramsData.HiveName(hive) + "\\" + parts[1],
                    Reason = I18n.T("residual_reason_tracked")
                });
            }
            foreach (var f in fp.FileItems)
            {
                if (SafetyGuard.IsProtectedPath(f)) continue;
                if (!Directory.Exists(f) && !File.Exists(f)) continue;
                items.Add(new ResidualItem
                {
                    Type = Directory.Exists(f) ? ResidualType.Folder : ResidualType.Shortcut,
                    Path = f, DisplayPath = f, Reason = I18n.T("residual_reason_tracked")
                });
            }
            return items;
        }
    }
}
