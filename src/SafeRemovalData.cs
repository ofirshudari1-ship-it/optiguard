using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UninstallerPro
{
    // "Safe to remove" confidence score for the Programs list - a small, fully
    // local heuristic (no network, no telemetry) added after competitor research
    // (2026-09) showed several leading uninstallers surface some notion of
    // "last used" to help users decide what to remove, which OptiGuard had no
    // equivalent for.
    //
    // Windows does NOT reliably expose "when was this program last run" for
    // ordinary desktop (Win32) applications - NTFS last-access-time updates
    // have been disabled by default since Windows Vista for performance
    // reasons, so InstallLocation file LastAccessTime is not trustworthy. The
    // one broadly-available, purely local signal that keeps working under that
    // default is the Windows Prefetch cache (%WINDIR%\Prefetch\*.pf): Windows
    // writes/rewrites a prefetch file named "<EXENAME>-<HASH>.pf" every time an
    // executable runs, and its own LastWriteTime is a reliable proxy for "last
    // run". This class cross-references each installed program's candidate exe
    // name(s) against that prefetch index.
    //
    // Deliberately conservative: if prefetch is empty/unavailable (disabled on
    // some SSD configurations, or access denied) or no exe name matches, the
    // result is "unknown" - never silently treated as "safe". This is a
    // confidence signal to help the user decide, not an autonomous action -
    // nothing is ever removed based on this score without the existing
    // explicit uninstall confirmation flow.
    public static class SafeRemovalData
    {
        private static Dictionary<string, DateTime> _prefetchIndex;
        private static readonly object _lock = new object();

        private static Dictionary<string, DateTime> GetPrefetchIndex()
        {
            lock (_lock)
            {
                if (_prefetchIndex != null) return _prefetchIndex;
                var index = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
                    if (Directory.Exists(dir))
                    {
                        foreach (var f in Directory.EnumerateFiles(dir, "*.pf"))
                        {
                            var baseName = Path.GetFileNameWithoutExtension(f);
                            var dash = baseName.LastIndexOf('-');
                            if (dash <= 0) continue;
                            var exeName = baseName.Substring(0, dash);
                            DateTime writeTime;
                            try { writeTime = File.GetLastWriteTime(f); } catch { continue; }
                            DateTime existing;
                            if (!index.TryGetValue(exeName, out existing) || writeTime > existing)
                                index[exeName] = writeTime;
                        }
                    }
                }
                catch { /* prefetch folder inaccessible - fall through to empty index -> "unknown" for everything */ }
                _prefetchIndex = index;
                return _prefetchIndex;
            }
        }

        // Only used by tests / a future "rescan" action - lets a fresh app
        // session re-read Prefetch instead of trusting a stale in-memory copy.
        public static void ResetCache()
        {
            lock (_lock) { _prefetchIndex = null; }
        }

        private static IEnumerable<string> CandidateExeNames(InstalledProgram p)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(p.InstallLocation) && Directory.Exists(p.InstallLocation))
            {
                try
                {
                    foreach (var f in Directory.EnumerateFiles(p.InstallLocation, "*.exe", SearchOption.TopDirectoryOnly))
                        names.Add(Path.GetFileNameWithoutExtension(f));
                }
                catch { }
            }
            // Cheap fallback guess from the display name itself (e.g. "Zoom" ->
            // "Zoom") - no extra disk I/O; prefetch lookup is an exact match so a
            // wrong guess simply misses rather than producing a false positive.
            if (!string.IsNullOrEmpty(p.DisplayName))
            {
                var guess = new string(p.DisplayName.Where(char.IsLetterOrDigit).ToArray());
                if (guess.Length > 0) names.Add(guess);
            }
            return names;
        }

        public static DateTime? EstimateLastUsed(InstalledProgram p)
        {
            var index = GetPrefetchIndex();
            if (index.Count == 0) return null;
            DateTime? best = null;
            foreach (var name in CandidateExeNames(p))
            {
                DateTime dt;
                if (index.TryGetValue(name, out dt))
                {
                    if (best == null || dt > best.Value) best = dt;
                }
            }
            return best;
        }

        // Tiers: "high" = probably safe to remove (no run evidence in 6+
        // months), "medium" = used within the last 30-180 days, "low" = used
        // recently (last 30 days) - removing it is more likely to surprise the
        // user, "unknown" = no prefetch evidence either way.
        public static void Apply(InstalledProgram p)
        {
            if (p.SystemComponent) { p.SafeRemovalLevel = "unknown"; return; }
            var last = EstimateLastUsed(p);
            p.LastUsedEstimate = last;
            if (last == null) { p.SafeRemovalLevel = "unknown"; return; }
            var days = (DateTime.Now - last.Value).TotalDays;
            if (days >= 180) p.SafeRemovalLevel = "high";
            else if (days >= 30) p.SafeRemovalLevel = "medium";
            else p.SafeRemovalLevel = "low";
        }
    }
}
