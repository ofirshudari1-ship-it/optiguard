using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace UninstallerPro
{
    // Scheduled automatic cleanup (added 4.10.0) - the one clearly-missing
    // capability found in competitor research (CCleaner and IObit Uninstaller
    // both offer scheduled/automatic cleanup; OptiGuard was manual-only).
    //
    // Implemented via the built-in Windows Task Scheduler (schtasks.exe) rather
    // than a custom always-running background service - OptiGuard is not a
    // tray-resident app (see _AUDIT/STANDARDS.md 12.1: "one-shot tools - X
    // closes fully, no tray"), so a scheduled task that launches the exe
    // briefly, does its work, and exits is the correct fit, and it is what the
    // user already sees and can manage themselves via Task Scheduler if they
    // want to (full transparency, no hidden background process).
    //
    // Safety: the scheduled run (Program.cs "--auto-clean") only ever touches
    // the same junk categories the manual Junk Cleaner tab offers, EXCLUDING
    // the Recycle Bin (an instant, non-reversible delete - fine for an
    // interactive click, not fine to run unattended). Every other category is
    // still routed through Quarantine.QuarantineItems with a real batch id, so
    // an unattended run remains fully reversible via "Undo Last Cleanup" - the
    // same confirm-before-destructive safety net the standard requires, just
    // expressed as "reversible" instead of "requires a click" since there is
    // no one present to click.
    public static class ScheduledCleanupData
    {
        public const string TaskName = "OptiGuard Scheduled Cleanup";

        public class LastRunInfo
        {
            public DateTime RanAt { get; set; }
            public long FreedBytes { get; set; }
            public bool Notified { get; set; }
            // True when the run found more junk than the user's configured
            // safety limit (AppSettings.ScheduledCleanupMaxSizeMB) and skipped
            // deleting anything instead of cleaning unattended. FoundBytes is
            // how much it found (for the notification), while FreedBytes stays 0.
            public bool SkippedThreshold { get; set; }
            public long FoundBytes { get; set; }
        }

        private static string MarkerFile { get { return Path.Combine(AppPaths.DataDir, "last-auto-clean.json"); } }

        public static bool IsSupportedFrequency(string freq)
        {
            return freq == "Daily" || freq == "Weekly" || freq == "Monthly";
        }

        // Registers/updates the task to run this exe with --auto-clean on the
        // requested schedule, at 03:00 local time (a quiet hour for most
        // desktop users) with highest privileges (the app already requires
        // admin - schtasks needs that too, to write junk-category paths under
        // Windows\Temp etc.). /F overwrites any existing task with this name so
        // changing the frequency in Settings just re-registers it.
        public static bool CreateOrUpdate(string frequency, out string error)
        {
            error = null;
            if (!IsSupportedFrequency(frequency)) frequency = "Weekly";
            try
            {
                var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var schtasksFreq = frequency.ToUpperInvariant(); // DAILY | WEEKLY | MONTHLY - schtasks /sc values
                var args = "/Create /TN \"" + TaskName + "\" /TR \"\\\"" + exePath + "\\\" --auto-clean\" " +
                           "/SC " + schtasksFreq + " /ST 03:00 /RL HIGHEST /F";
                var result = RunSchtasks(args);
                if (result.ExitCode != 0)
                {
                    error = result.Output;
                    Logger.Log("Failed to create scheduled cleanup task (exit " + result.ExitCode + "): " + result.Output);
                    return false;
                }
                Logger.Log("Scheduled cleanup task registered: " + frequency);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Logger.Log("Error registering scheduled cleanup task: " + ex.Message);
                return false;
            }
        }

        public static void Remove()
        {
            try
            {
                var result = RunSchtasks("/Delete /TN \"" + TaskName + "\" /F");
                if (result.ExitCode == 0) Logger.Log("Scheduled cleanup task removed.");
            }
            catch (Exception ex) { Logger.Log("Error removing scheduled cleanup task: " + ex.Message); }
        }

        private class ProcResult { public int ExitCode; public string Output; }

        private static ProcResult RunSchtasks(string args)
        {
            var psi = new ProcessStartInfo("schtasks.exe", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var p = Process.Start(psi))
            {
                var stdout = p.StandardOutput.ReadToEnd();
                var stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                return new ProcResult { ExitCode = p.ExitCode, Output = string.IsNullOrEmpty(stderr) ? stdout : stderr };
            }
        }

        // Runs the actual unattended cleanup - called from Program.cs when
        // launched with --auto-clean (by the scheduled task, headless, no UI).
        // Writes a small marker file so the next time a human opens OptiGuard
        // normally, MainWindow can show a one-time Toast summarizing what
        // happened while nobody was watching (transparency, per the "never
        // silently do things" principle already applied to quarantine purges).
        public static void RunHeadlessCleanup()
        {
            RunHeadlessCleanup(AppSettings.Load());
        }

        // Overload used by Program.cs (which already has the loaded settings
        // on hand) so the unattended run respects the user's category
        // selection and safety-limit choices from Settings > Advanced.
        public static void RunHeadlessCleanup(AppSettings settings)
        {
            long freed = 0;
            long found = 0;
            bool skipped = false;
            try
            {
                var allowedCategories = new HashSet<string>(
                    (settings != null ? settings.ScheduledCleanupCategories : null ?? "")
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

                var candidates = JunkCleanerData.ScanAll()
                    .Where(c => !c.IsRecycleBin) // not reversible - never touched unattended
                    .Where(c => allowedCategories.Count == 0 || allowedCategories.Contains(c.Key))
                    .ToList();

                found = candidates.Sum(c => c.SizeBytes);

                var maxSizeMb = settings != null ? settings.ScheduledCleanupMaxSizeMB : 0;
                if (maxSizeMb > 0 && found > (long)maxSizeMb * 1024 * 1024)
                {
                    skipped = true;
                    Logger.Log("Scheduled auto-clean skipped: found " + JunkCleanerData.FormatSize(found) +
                               ", over the " + maxSizeMb + " MB safety limit.");
                }
                else
                {
                    var batchId = Quarantine.NewBatchId();
                    foreach (var category in candidates)
                    {
                        freed += JunkCleanerData.Clean(category, batchId);
                    }
                    Logger.Log("Scheduled auto-clean finished, freed " + JunkCleanerData.FormatSize(freed));
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Scheduled auto-clean failed: " + ex.Message);
            }
            try
            {
                AppPaths.EnsureDataDir();
                var info = new LastRunInfo { RanAt = DateTime.Now, FreedBytes = freed, Notified = false, SkippedThreshold = skipped, FoundBytes = found };
                var serializer = new JavaScriptSerializer();
                File.WriteAllText(MarkerFile, serializer.Serialize(info));
            }
            catch { }
        }

        // Reads (and marks consumed) the result of the most recent unattended
        // run, if any and if not already shown. Returns null when there is
        // nothing new to tell the user about.
        public static LastRunInfo TakePendingNotification()
        {
            try
            {
                if (!File.Exists(MarkerFile)) return null;
                var serializer = new JavaScriptSerializer();
                var info = serializer.Deserialize<LastRunInfo>(File.ReadAllText(MarkerFile));
                if (info == null || info.Notified) return null;
                info.Notified = true;
                File.WriteAllText(MarkerFile, serializer.Serialize(info));
                return info;
            }
            catch { return null; }
        }
    }
}
