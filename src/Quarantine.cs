using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace UninstallerPro
{
    // בטיחות-רשת לניקוי הזבל: קבצים לא נמחקים מיד לצמיתות אלא עוברים ל"הסגר"
    // מקומי (AppData) לשבוע. אפשר לבטל ניקוי אחרון בלחיצה אחת. מטפל בדיוק
    // בתלונה התיעודית הנפוצה ביותר על כלי ניקוי: "זה מחק לי משהו שהייתי צריך".
    public static class Quarantine
    {
        private static readonly string QDir = Path.Combine(AppPaths.DataDir, "Quarantine");
        private static readonly string ManifestPath = Path.Combine(QDir, "manifest.json");
        private static readonly object Lock = new object();

        public class Entry
        {
            public string Id;
            public string OriginalPath;
            public string QuarantinePath;
            public string BatchId;
            public string DeletedAt;
        }

        public static string NewBatchId() { return DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6); }

        // JavaScriptSerializer חוסם ב-2,097,152 תווים כברירת מחדל (Deserialize
        // וגם Serialize) - כשהמניפסט גדל מעבר לזה (שימוש אמיתי לאורך זמן),
        // Serialize זורק חריגה שנבלעת בשקט ו-SaveManifest לא כותב כלום. זה
        // גרם לקבצים שהועברו בפועל להסגר "להיעלם" בלי רישום - לא ניתנים
        // לשחזור ולעולם לא ינוקו. MaxJsonLength=int.MaxValue מסיר את התקרה.
        private static List<Entry> LoadManifest()
        {
            try
            {
                if (!File.Exists(ManifestPath)) return new List<Entry>();
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var raw = serializer.Deserialize<List<Dictionary<string, object>>>(File.ReadAllText(ManifestPath));
                return raw.Select(d => new Entry
                {
                    Id = d.ContainsKey("Id") ? d["Id"] as string : null,
                    OriginalPath = d.ContainsKey("OriginalPath") ? d["OriginalPath"] as string : null,
                    QuarantinePath = d.ContainsKey("QuarantinePath") ? d["QuarantinePath"] as string : null,
                    BatchId = d.ContainsKey("BatchId") ? d["BatchId"] as string : null,
                    DeletedAt = d.ContainsKey("DeletedAt") ? d["DeletedAt"] as string : null,
                }).ToList();
            }
            catch (Exception ex) { Logger.Log("Quarantine manifest load failed (data may be stale until fixed): " + ex.Message); return new List<Entry>(); }
        }

        private static void SaveManifest(List<Entry> entries)
        {
            try
            {
                Directory.CreateDirectory(QDir);
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var raw = entries.Select(e => new Dictionary<string, object>
                {
                    { "Id", e.Id }, { "OriginalPath", e.OriginalPath }, { "QuarantinePath", e.QuarantinePath },
                    { "BatchId", e.BatchId }, { "DeletedAt", e.DeletedAt }
                }).ToList();
                File.WriteAllText(ManifestPath, serializer.Serialize(raw));
            }
            catch (Exception ex) { Logger.Log("Quarantine manifest save FAILED - items just quarantined may be orphaned: " + ex.Message); }
        }

        // מעביר קובץ/תיקייה בודדים להסגר. לשימוש חד-פעמי בלבד - טוען ושומר
        // את כל המניפסט פעם אחת לכל קריאה, כך שקריאה בלולאה על מאות/אלפי
        // קבצים היא O(n²) ותהיה איטית ככל שהמניפסט גדל. לניקוי מרובה-קבצים
        // יש להשתמש ב-QuarantineItems (טוען/שומר פעם אחת לכל האצווה).
        public static long QuarantineItem(string originalPath, string batchId)
        {
            return QuarantineItems(new[] { originalPath }, batchId);
        }

        // גרסת-אצווה: טוענת את המניפסט פעם אחת, מעבירה את כל הפריטים להסגר,
        // ושומרת פעם אחת בסוף - במקום טעינה/שמירה מלאה של הקובץ (יכול לגדול
        // למגה-בייטים עם שימוש ממושך) לכל קובץ בודד. זה מה שהפך ניקוי של
        // מאות קבצים לאיטי מאוד ברגע שהמניפסט הצטבר.
        public static long QuarantineItems(IEnumerable<string> originalPaths, string batchId, Action<int, int> onProgress = null)
        {
            lock (Lock)
            {
                Directory.CreateDirectory(QDir);
                var entries = LoadManifest();
                var pathList = originalPaths as IList<string> ?? originalPaths.ToList();
                long totalSize = 0;
                bool changed = false;

                for (int i = 0; i < pathList.Count; i++)
                {
                    if (onProgress != null) onProgress(i, pathList.Count);
                    var originalPath = pathList[i];
                    try
                    {
                        var id = Guid.NewGuid().ToString("N");
                        bool isDir = Directory.Exists(originalPath);
                        if (!isDir && !File.Exists(originalPath)) continue;
                        long size = isDir ? DirSize(originalPath) : new FileInfo(originalPath).Length;
                        var dest = Path.Combine(QDir, id + (isDir ? "" : Path.GetExtension(originalPath)));

                        if (isDir)
                        {
                            // Directory.Move (בניגוד ל-File.Move) לא תומך במעבר בין כוננים -
                            // זורק IOException. אם מקור ויעד על אותו כונן זה rename מיידי;
                            // אחרת נופלים ל-CopyDir+Delete שכן תומך במעבר בין כוננים.
                            if (string.Equals(Path.GetPathRoot(originalPath), Path.GetPathRoot(dest), StringComparison.OrdinalIgnoreCase))
                                Directory.Move(originalPath, dest);
                            else
                            {
                                CopyDirectory(originalPath, dest);
                                Directory.Delete(originalPath, true);
                            }
                        }
                        else
                        {
                            File.Move(originalPath, dest);
                        }

                        entries.Add(new Entry { Id = id, OriginalPath = originalPath, QuarantinePath = dest, BatchId = batchId, DeletedAt = DateTime.Now.ToString("o") });
                        totalSize += size;
                        changed = true;
                    }
                    catch (Exception ex) { Logger.Log("Quarantine failed for " + originalPath + ": " + ex.Message); }
                }

                if (changed) SaveManifest(entries);
                return totalSize;
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            foreach (var sub in Directory.GetDirectories(sourceDir))
                CopyDirectory(sub, Path.Combine(destDir, Path.GetFileName(sub)));
        }

        private static long DirSize(string path)
        {
            long total = 0;
            try { foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) { try { total += new FileInfo(f).Length; } catch { } } }
            catch { }
            return total;
        }

        public static string GetLastBatchId()
        {
            var entries = LoadManifest();
            return entries.Count == 0 ? null : entries.OrderByDescending(e => e.DeletedAt).First().BatchId;
        }

        public class BatchSummary
        {
            public string BatchId;
            public DateTime When;
            public int Count;
        }

        // מסכם את מניפסט ההסגר לפי אצווה - לשימוש בציר הזמן/ההיסטוריה של הפעולות.
        public static List<BatchSummary> GetBatchSummaries()
        {
            var entries = LoadManifest();
            var result = new List<BatchSummary>();
            foreach (var g in entries.GroupBy(e => e.BatchId))
            {
                DateTime when;
                DateTime.TryParse(g.Max(e => e.DeletedAt), out when);
                result.Add(new BatchSummary { BatchId = g.Key, When = when, Count = g.Count() });
            }
            return result;
        }

        public static int RestoreBatch(string batchId)
        {
            lock (Lock)
            {
                if (string.IsNullOrEmpty(batchId)) return 0;
                var entries = LoadManifest();
                var toRestore = entries.Where(e => e.BatchId == batchId).ToList();
                int restored = 0;
                foreach (var e in toRestore)
                {
                    try
                    {
                        var parent = Path.GetDirectoryName(e.OriginalPath);
                        if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent)) Directory.CreateDirectory(parent);
                        if (Directory.Exists(e.QuarantinePath))
                        {
                            if (string.Equals(Path.GetPathRoot(e.QuarantinePath), Path.GetPathRoot(e.OriginalPath), StringComparison.OrdinalIgnoreCase))
                                Directory.Move(e.QuarantinePath, e.OriginalPath);
                            else
                            {
                                CopyDirectory(e.QuarantinePath, e.OriginalPath);
                                Directory.Delete(e.QuarantinePath, true);
                            }
                        }
                        else if (File.Exists(e.QuarantinePath)) File.Move(e.QuarantinePath, e.OriginalPath);
                        restored++;
                    }
                    catch (Exception ex) { Logger.Log("Restore failed for " + e.OriginalPath + ": " + ex.Message); }
                }
                entries.RemoveAll(e => e.BatchId == batchId);
                SaveManifest(entries);
                Logger.Log("Restored " + restored + " item(s) from quarantine batch " + batchId);
                return restored;
            }
        }

        // מוחק לצמיתות פריטים שישבו בהסגר יותר מ-N ימים. נקרא פעם בהפעלה.
        public static int PurgeOlderThan(int days)
        {
            lock (Lock)
            {
                var entries = LoadManifest();
                var cutoff = DateTime.Now.AddDays(-days);
                var keep = new List<Entry>();
                foreach (var e in entries)
                {
                    DateTime deletedAt;
                    bool expired = DateTime.TryParse(e.DeletedAt, out deletedAt) && deletedAt < cutoff;
                    if (expired)
                    {
                        try
                        {
                            if (Directory.Exists(e.QuarantinePath)) Directory.Delete(e.QuarantinePath, true);
                            else if (File.Exists(e.QuarantinePath)) File.Delete(e.QuarantinePath);
                        }
                        catch { }
                    }
                    else keep.Add(e);
                }
                if (keep.Count != entries.Count) SaveManifest(keep);
                return entries.Count - keep.Count;
            }
        }

        // מנקה פריטים שפיזית יושבים בתיקיית ההסגר אבל אין להם רשומה במניפסט -
        // "יתומים" שנוצרו על ידי הבאג ההיסטורי (SaveManifest נכשל בשקט כש-
        // JavaScriptSerializer חרג מ-MaxJsonLength). בלי רשומה במניפסט אין
        // דרך לדעת את הנתיב המקורי שלהם, ולכן לא ניתן לשחזר - רק לפנות את
        // השטח שהם תופסים לשווא. נקרא פעם בהפעלה, יחד עם PurgeOlderThan.
        public static long PurgeOrphans()
        {
            lock (Lock)
            {
                try
                {
                    if (!Directory.Exists(QDir)) return 0;
                    var entries = LoadManifest();
                    var knownIds = new HashSet<string>(entries.Select(e => e.Id), StringComparer.OrdinalIgnoreCase);
                    long freed = 0;
                    int removedCount = 0;
                    foreach (var item in Directory.GetFileSystemEntries(QDir))
                    {
                        if (string.Equals(Path.GetFileName(item), "manifest.json", StringComparison.OrdinalIgnoreCase)) continue;
                        var baseName = Path.GetFileNameWithoutExtension(item);
                        if (knownIds.Contains(baseName)) continue;
                        try
                        {
                            if (Directory.Exists(item)) { freed += DirSize(item); Directory.Delete(item, true); }
                            else { freed += new FileInfo(item).Length; File.Delete(item); }
                            removedCount++;
                        }
                        catch (Exception ex) { Logger.Log("Failed to purge orphaned quarantine item " + item + ": " + ex.Message); }
                    }
                    if (removedCount > 0) Logger.Log("Purged " + removedCount + " orphaned quarantine item(s), freed " + Math.Round(freed / 1024.0 / 1024.0, 1) + " MB");
                    return freed;
                }
                catch (Exception ex) { Logger.Log("PurgeOrphans failed: " + ex.Message); return 0; }
            }
        }
    }
}
