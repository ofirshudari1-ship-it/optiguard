using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace UninstallerPro
{
    public class GhostRegistryEntry
    {
        public string DisplayName;
        public string Hive;
        public string SubKeyPath;
        public string Reason;
        public string MissingPath;
        public bool IsSelected;
        public string ReasonText { get { return I18n.T("reason_" + Reason); } }
    }

    // ניקוי רשומות רישום "רפאים": לא סריקה עיוורת של כל הרישום (זה מה ששובר
    // מערכות) - רק רשומות Uninstall שבהן יש הוכחה קונקרטית שהנתיב שאליו הן
    // מצביעות (InstallLocation / קובץ ההסרה / אייקון) כבר לא קיים בדיסק.
    // כל רשומה מגובה לקובץ .reg לפני מחיקה כדי שאפשר יהיה לשחזר.
    public static class RegistryCleanerData
    {
        private static readonly Tuple<RegistryHive, string>[] UninstallRoots = new[]
        {
            Tuple.Create(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            Tuple.Create(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            Tuple.Create(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        };

        private static string ExtractExePath(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return null;
            var trimmed = cmd.Trim();
            string path;
            if (trimmed.StartsWith("\""))
            {
                var end = trimmed.IndexOf('"', 1);
                path = end > 0 ? trimmed.Substring(1, end - 1) : trimmed.Trim('"');
            }
            else
            {
                var sp = trimmed.IndexOf(" -");
                if (sp < 0) sp = trimmed.IndexOf(" /");
                path = sp > 0 ? trimmed.Substring(0, sp) : trimmed;
            }
            path = path.Trim();
            var lower = path.ToLowerInvariant();
            if (lower.Contains("msiexec") || lower.Contains("rundll32") || lower.Contains("%") || string.IsNullOrWhiteSpace(path)) return null;
            return path;
        }

        private static string ExtractIconPath(string displayIcon)
        {
            if (string.IsNullOrWhiteSpace(displayIcon)) return null;
            var trimmed = displayIcon.Trim();
            string path;
            if (trimmed.StartsWith("\""))
            {
                var end = trimmed.IndexOf('"', 1);
                path = end > 0 ? trimmed.Substring(1, end - 1) : trimmed.Trim('"');
            }
            else
            {
                var comma = trimmed.LastIndexOf(',');
                path = (comma > 1 && comma > trimmed.Length - 6) ? trimmed.Substring(0, comma) : trimmed;
            }
            path = path.Trim();
            if (path.ToLowerInvariant().Contains("%") || string.IsNullOrWhiteSpace(path)) return null;
            return path;
        }

        // InstallLocation לפעמים מגיע עם מרכאות מקיפות מיותרות, ולפעמים מוגדר (בניגוד
        // לתקן) כנתיב לקובץ בודד ולא לתיקייה - בודקים את שני המקרים לפני שמסמנים כחסר.
        private static bool InstallPathExists(string path)
        {
            var clean = path.Trim().Trim('"');
            return Directory.Exists(clean) || File.Exists(clean);
        }

        public static List<GhostRegistryEntry> Scan()
        {
            var result = new List<GhostRegistryEntry>();
            foreach (var root in UninstallRoots)
            {
                using (var baseKey = RegistryUtil.OpenBase(root.Item1))
                using (var uninstallKey = baseKey.OpenSubKey(root.Item2))
                {
                    if (uninstallKey == null) continue;
                    foreach (var subName in uninstallKey.GetSubKeyNames())
                    {
                        try
                        {
                            using (var sub = uninstallKey.OpenSubKey(subName))
                            {
                                if (sub == null) continue;
                                var displayName = sub.GetValue("DisplayName") as string;
                                if (string.IsNullOrWhiteSpace(displayName)) continue;

                                var parentKeyName = sub.GetValue("ParentKeyName") as string;
                                if (!string.IsNullOrEmpty(parentKeyName)) continue; // MSI patch sub-entries, not real programs

                                var installLocation = sub.GetValue("InstallLocation") as string;
                                var uninstallString = sub.GetValue("UninstallString") as string;
                                var quietUninstallString = sub.GetValue("QuietUninstallString") as string;
                                var displayIcon = sub.GetValue("DisplayIcon") as string;

                                string reason = null;
                                string missingPath = null;

                                if (!string.IsNullOrWhiteSpace(installLocation) && !InstallPathExists(installLocation))
                                {
                                    reason = "missing_install_location";
                                    missingPath = installLocation.Trim().Trim('"');
                                }
                                else
                                {
                                    var exePath = ExtractExePath(uninstallString) ?? ExtractExePath(quietUninstallString);
                                    if (exePath != null && !File.Exists(exePath))
                                    {
                                        reason = "missing_uninstaller";
                                        missingPath = exePath;
                                    }
                                    else if (string.IsNullOrWhiteSpace(installLocation))
                                    {
                                        var iconPath = ExtractIconPath(displayIcon);
                                        if (iconPath != null && !File.Exists(iconPath))
                                        {
                                            reason = "missing_icon_target";
                                            missingPath = iconPath;
                                        }
                                    }
                                }

                                if (reason == null) continue;

                                result.Add(new GhostRegistryEntry
                                {
                                    DisplayName = displayName,
                                    Hive = root.Item1 == RegistryHive.LocalMachine ? "HKLM" : "HKCU",
                                    SubKeyPath = root.Item2 + "\\" + subName,
                                    Reason = reason,
                                    MissingPath = missingPath,
                                    IsSelected = true
                                });
                            }
                        }
                        catch (Exception ex) { Logger.Log("Registry scan error for " + subName + ": " + ex.Message); }
                    }
                }
            }
            return result;
        }

        // מגבה כל רשומה נבחרת לקובץ .reg יחיד (ב-AppData) לפני המחיקה, כדי שאפשר יהיה לשחזר ידנית.
        public static string BackupToRegFile(List<GhostRegistryEntry> entries, Action<int, int> onProgress = null)
        {
            try
            {
                var dir = Path.Combine(AppPaths.DataDir, "RegistryBackups");
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, "backup_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".reg");
                // reg.exe export works on one key at a time; export each entry into its own temp file then merge.
                var mergedHeader = "Windows Registry Editor Version 5.00\r\n\r\n";
                var tempFiles = new List<string>();
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (onProgress != null) onProgress(i, entries.Count);
                    var hiveFull = e.Hive == "HKLM" ? "HKEY_LOCAL_MACHINE" : "HKEY_CURRENT_USER";
                    var tempFile = Path.Combine(dir, "tmp_" + Guid.NewGuid().ToString("N") + ".reg");
                    var exportPsi = new ProcessStartInfo("reg.exe", "export \"" + hiveFull + "\\" + e.SubKeyPath + "\" \"" + tempFile + "\" /y")
                    {
                        UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
                    };
                    using (var p = Process.Start(exportPsi)) { p.WaitForExit(15000); }
                    if (File.Exists(tempFile)) tempFiles.Add(tempFile);
                }
                using (var writer = new StreamWriter(file, false, System.Text.Encoding.Unicode))
                {
                    writer.Write(mergedHeader);
                    foreach (var tf in tempFiles)
                    {
                        var content = File.ReadAllText(tf, System.Text.Encoding.Unicode);
                        var idx = content.IndexOf("\n\r\n");
                        var body = content.StartsWith("Windows Registry Editor")
                            ? content.Substring(content.IndexOf('[') >= 0 ? content.IndexOf('[') : 0)
                            : content;
                        writer.Write(body);
                        writer.Write("\r\n");
                        try { File.Delete(tf); } catch { }
                    }
                }
                return file;
            }
            catch (Exception ex) { Logger.Log("Registry backup failed: " + ex.Message); return null; }
        }

        // מוחק אוטומטית גיבויי .reg ישנים (נקרא פעם בהפעלה) - כל הסרה יוצרת קובץ
        // חדש שאף פעם לא נמחק לבד; בלי גבול זה מצטבר ללא הגבלה לאורך זמן.
        public static void PurgeOldBackups(int days)
        {
            try
            {
                var dir = Path.Combine(AppPaths.DataDir, "RegistryBackups");
                if (!Directory.Exists(dir)) return;
                var cutoff = DateTime.Now.AddDays(-days);
                foreach (var f in Directory.GetFiles(dir, "backup_*.reg"))
                {
                    try { if (File.GetLastWriteTime(f) < cutoff) File.Delete(f); } catch { }
                }
            }
            catch (Exception ex) { Logger.Log("PurgeOldBackups failed: " + ex.Message); }
        }

        public static int RemoveEntries(List<GhostRegistryEntry> entries)
        {
            int removed = 0;
            foreach (var e in entries)
            {
                var hive = e.Hive == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
                if (RegistryUtil.DeleteSubKeyTree(hive, e.SubKeyPath))
                {
                    removed++;
                    Logger.Log("Removed ghost registry entry: " + e.Hive + "\\" + e.SubKeyPath + " (" + e.DisplayName + ")");
                }
            }
            return removed;
        }
    }
}
