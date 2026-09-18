using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UninstallerPro
{
    public class WizardCleanSummary
    {
        public long JunkBytesFreed;
        public int StartupItemsCleaned;
        public int EmptyFoldersRemoved;
    }

    // אשף חכם: סורק ב"מבט אחד" זבל, ערכי הפעלה אוטומטית שבורים (מצביעים לקובץ
    // שכבר לא קיים - שריד נפוץ של תוכנה שהוסרה ידנית), ותיקיות ריקות במיקומים
    // נפוצים. מציג הכל למשתמש לאישור ומנקה רק את מה שסומן.
    public static class SmartWizard
    {
        public static List<WizardFinding> Scan()
        {
            var findings = new List<WizardFinding>();

            foreach (var junk in JunkCleanerData.ScanAll())
            {
                findings.Add(new WizardFinding { Kind = WizardKind.Junk, Name = junk.Name, Detail = junk.SizeText, Payload = junk });
            }

            foreach (var item in StartupData.GetStartupItems())
            {
                if (!item.Enabled || item.Type != StartupType.Registry) continue;
                var exePath = ExtractExePath(item.Command);
                if (!string.IsNullOrEmpty(exePath) && !File.Exists(exePath))
                {
                    findings.Add(new WizardFinding { Kind = WizardKind.BrokenStartup, Name = item.DisplayName, Detail = exePath, Payload = item });
                }
            }

            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
            }.Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r)).Distinct();

            foreach (var root in roots)
            {
                string[] topDirs;
                try { topDirs = Directory.GetDirectories(root); } catch { topDirs = new string[0]; }
                foreach (var dir in topDirs)
                {
                    if (SafetyGuard.IsProtectedPath(dir)) continue;
                    List<string> empties;
                    try { empties = SafetyGuard.GetEmptyDirectoriesRecursive(dir); } catch { continue; }
                    // אם התיקייה העליונה עצמה ריקה לגמרי, נציע למחוק גם אותה
                    try
                    {
                        if (Directory.GetFileSystemEntries(dir).Length == 0) empties.Add(dir);
                    }
                    catch { }
                    foreach (var e in empties)
                    {
                        findings.Add(new WizardFinding { Kind = WizardKind.EmptyFolder, Name = Path.GetFileName(e.TrimEnd('\\')), Detail = e, Payload = e });
                    }
                }
            }

            return findings;
        }

        private static string ExtractExePath(string command)
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

        public static WizardCleanSummary Clean(List<WizardFinding> selected, Action<int, int> onProgress = null)
        {
            var summary = new WizardCleanSummary();
            var batchId = Quarantine.NewBatchId();
            RestorePoint.Create("OptiGuard - before Smart Scan cleanup");
            for (int idx = 0; idx < selected.Count; idx++)
            {
                var f = selected[idx];
                if (onProgress != null) onProgress(idx, selected.Count);
                try
                {
                    switch (f.Kind)
                    {
                        case WizardKind.Junk:
                            summary.JunkBytesFreed += JunkCleanerData.Clean((JunkCategory)f.Payload, batchId);
                            break;
                        case WizardKind.BrokenStartup:
                            StartupData.Disable((StartupItem)f.Payload);
                            summary.StartupItemsCleaned++;
                            break;
                        case WizardKind.EmptyFolder:
                            var path = (string)f.Payload;
                            if (Directory.Exists(path) && !SafetyGuard.IsProtectedPath(path))
                            {
                                Directory.Delete(path);
                                summary.EmptyFoldersRemoved++;
                            }
                            break;
                    }
                }
                catch (Exception ex) { Logger.Log("Smart Wizard clean error (" + f.Kind + " " + f.Name + "): " + ex.Message); }
            }
            Stats.Add(d =>
            {
                d.JunkBytesFreed += summary.JunkBytesFreed;
                if (summary.JunkBytesFreed > 0) d.JunkCleanupRuns++;
                d.StartupItemsCleaned += summary.StartupItemsCleaned;
            });
            return summary;
        }
    }
}
