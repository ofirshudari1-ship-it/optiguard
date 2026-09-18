using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace UninstallerPro
{
    public class JunkCategory
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public List<string> Paths { get; set; }
        public bool IsRecycleBin { get; set; }
        public long SizeBytes { get; set; }

        public string SizeText { get { return JunkCleanerData.FormatSize(SizeBytes); } }
    }

    public static class JunkCleanerData
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);
        private const uint SHERB_NOCONFIRMATION = 0x00000001;
        private const uint SHERB_NOPROGRESSUI = 0x00000002;
        private const uint SHERB_NOSOUND = 0x00000004;

        public static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "0 MB";
            double mb = bytes / 1024.0 / 1024.0;
            if (mb >= 1024) return Math.Round(mb / 1024.0, 2) + " GB";
            return Math.Round(mb, 1) + " MB";
        }

        private static long GetDirSize(string path)
        {
            long total = 0;
            if (!Directory.Exists(path)) return 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(file).Length; } catch { }
                }
            }
            catch { }
            return total;
        }

        public static List<JunkCategory> ScanAll()
        {
            var result = new List<JunkCategory>();

            var userTemp = new JunkCategory { Key = "user_temp", Name = I18n.T("junk_user_temp"), Paths = new List<string> { Path.GetTempPath() } };
            var winTemp = new JunkCategory { Key = "win_temp", Name = I18n.T("junk_win_temp"), Paths = new List<string> { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp") } };
            var prefetch = new JunkCategory { Key = "prefetch", Name = I18n.T("junk_prefetch"), Paths = new List<string> { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch") } };
            var winUpdate = new JunkCategory { Key = "win_update", Name = I18n.T("junk_win_update"), Paths = new List<string> { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SoftwareDistribution\Download") } };
            var thumbs = new JunkCategory { Key = "thumbnails", Name = I18n.T("junk_thumbnails"), Paths = new List<string> { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Explorer") } };

            var chromeCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data\Default\Cache");
            var edgeCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data\Default\Cache");
            var browserCache = new JunkCategory { Key = "browser_cache", Name = I18n.T("junk_browser_cache"), Paths = new List<string> { chromeCache, edgeCache } };
            var ffRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Mozilla\Firefox\Profiles");
            if (Directory.Exists(ffRoot))
            {
                foreach (var prof in Directory.GetDirectories(ffRoot))
                {
                    var cache2 = Path.Combine(prof, "cache2");
                    if (Directory.Exists(cache2)) browserCache.Paths.Add(cache2);
                }
            }

            var recycleBin = new JunkCategory { Key = "recycle_bin", Name = I18n.T("junk_recycle_bin"), Paths = new List<string>(), IsRecycleBin = true };

            foreach (var cat in new[] { userTemp, winTemp, prefetch, winUpdate, thumbs, browserCache })
            {
                cat.SizeBytes = cat.Paths.Sum(p => GetDirSize(p));
            }
            recycleBin.SizeBytes = GetRecycleBinSize();

            result.Add(recycleBin);
            result.Add(userTemp);
            result.Add(browserCache);
            result.Add(winUpdate);
            result.Add(winTemp);
            result.Add(thumbs);
            result.Add(prefetch);
            return result.Where(c => c.SizeBytes > 0 || c.IsRecycleBin).ToList();
        }

        private static long GetRecycleBinSize()
        {
            long total = 0;
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    var rb = Path.Combine(drive.Name, "$Recycle.Bin");
                    if (Directory.Exists(rb)) total += GetDirSize(rb);
                }
            }
            catch { }
            return total;
        }

        // batchId != null -> קבצים עוברים ל"הסגר" (Quarantine) לשבוע במקום
        // מחיקה מיידית, כדי לאפשר "בטל ניקוי אחרון". סל המיחזור תמיד נמחק
        // ישירות (הוא כבר מהווה מנגנון "פח" בפני עצמו).
        public static long Clean(JunkCategory category, string batchId = null)
        {
            long freed = 0;
            if (category.IsRecycleBin)
            {
                freed = category.SizeBytes;
                try { SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND); }
                catch (Exception ex) { Logger.Log("שגיאה בריקון סל המיחזור: " + ex.Message); }
                return freed;
            }
            foreach (var path in category.Paths)
            {
                if (!Directory.Exists(path)) continue;
                var files = SafeEnumerateFiles(path).ToList();
                if (batchId != null)
                {
                    // אצווה אחת לכל הקבצים בתיקייה - טוענת/שומרת את מניפסט ההסגר
                    // פעם אחת במקום פעם לכל קובץ. זה מה שהפך ניקוי של מאות/אלפי
                    // קבצים לאיטי מאוד ברגע שהמניפסט הצטבר לאורך זמן.
                    freed += Quarantine.QuarantineItems(files, batchId);
                }
                else
                {
                    foreach (var file in files)
                    {
                        try
                        {
                            var len = new FileInfo(file).Length;
                            File.Delete(file);
                            freed += len;
                        }
                        catch { }
                    }
                }
                foreach (var dir in SafeEnumerateDirectories(path).OrderByDescending(d => d.Length))
                {
                    try { if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir); } catch { }
                }
            }
            Logger.Log("נוקתה קטגוריית זבל: " + category.Name + " (" + FormatSize(freed) + ")" + (batchId != null ? " [quarantined]" : ""));
            return freed;
        }

        private static IEnumerable<string> SafeEnumerateFiles(string path)
        {
            try { return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).ToList(); }
            catch { return new List<string>(); }
        }

        private static IEnumerable<string> SafeEnumerateDirectories(string path)
        {
            try { return Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).ToList(); }
            catch { return new List<string>(); }
        }
    }
}
