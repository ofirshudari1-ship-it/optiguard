using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace UninstallerPro
{
    public class DuplicateFileEntry
    {
        public string Path;
        public long SizeBytes;
        public DateTime LastModified;
        public bool IsSelected;
        public string SizeText { get { return JunkCleanerData.FormatSize(SizeBytes); } }
        public string LastModifiedText { get { return LastModified.ToString("dd/MM/yyyy"); } }
    }

    public class DuplicateGroup
    {
        public string Hash;
        public long SizeBytes;
        public List<DuplicateFileEntry> Files = new List<DuplicateFileEntry>();
        public string SizeText { get { return JunkCleanerData.FormatSize(SizeBytes); } }
        public long WastedBytes { get { return SizeBytes * (Files.Count - 1); } }
        public string WastedText { get { return JunkCleanerData.FormatSize(WastedBytes); } }
    }

    // מוצא כפילויות: משווה קודם לפי גודל קובץ (זול וזריז לסינון) ומחשב Hash
    // (SHA1) רק בתוך קבוצות עם אותו גודל בדיוק - כך שהשוואה של תיקייה שלמה
    // לא דורשת hashing של כל קובץ, רק את החשודים האמיתיים. ברירת המחדל
    // משאירה את הקובץ הכי ישן (הכי סביר שהוא ה"מקור") ומסמנת את השאר להסרה.
    public static class DuplicateFileFinderData
    {
        private const long MinFileSizeBytes = 4096; // קבצים זעירים לא שווים את הבדיקה - הרבה false positives (קבצי config ריקים וכו')

        public static List<DuplicateGroup> Scan(string rootPath, Action<int> onEnumerationProgress = null, Action<int, int> onHashProgress = null)
        {
            var bySize = new Dictionary<long, List<string>>();
            var allFiles = new List<string>();
            int enumerated = 0;

            try
            {
                foreach (var f in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
                {
                    if (SafetyGuard.IsProtectedPath(Path.GetDirectoryName(f))) continue;
                    allFiles.Add(f);
                    enumerated++;
                    if (onEnumerationProgress != null && enumerated % 200 == 0) onEnumerationProgress(enumerated);
                }
            }
            catch (Exception ex) { Logger.Log("Duplicate scan enumeration error: " + ex.Message); }

            foreach (var f in allFiles)
            {
                long size;
                try { size = new FileInfo(f).Length; } catch { continue; }
                if (size < MinFileSizeBytes) continue;
                List<string> list;
                if (!bySize.TryGetValue(size, out list)) { list = new List<string>(); bySize[size] = list; }
                list.Add(f);
            }

            var candidateGroups = bySize.Values.Where(l => l.Count > 1).ToList();
            var result = new List<DuplicateGroup>();
            int processed = 0;
            int totalCandidates = candidateGroups.Sum(g => g.Count);

            foreach (var sizeGroup in candidateGroups)
            {
                var byHash = new Dictionary<string, DuplicateGroup>();
                foreach (var path in sizeGroup)
                {
                    processed++;
                    if (onHashProgress != null && processed % 20 == 0) onHashProgress(processed, totalCandidates);
                    string hash;
                    try { hash = ComputeHash(path); }
                    catch (Exception ex) { Logger.Log("Hash failed for " + path + ": " + ex.Message); continue; }

                    DuplicateGroup group;
                    if (!byHash.TryGetValue(hash, out group))
                    {
                        group = new DuplicateGroup { Hash = hash, SizeBytes = new FileInfo(path).Length };
                        byHash[hash] = group;
                    }
                    DateTime modified;
                    try { modified = File.GetLastWriteTime(path); } catch { modified = DateTime.Now; }
                    group.Files.Add(new DuplicateFileEntry { Path = path, SizeBytes = group.SizeBytes, LastModified = modified });
                }

                foreach (var g in byHash.Values.Where(g => g.Files.Count > 1))
                {
                    // ברירת מחדל: השאר את הקובץ הישן ביותר, סמן את השאר להסרה
                    var ordered = g.Files.OrderBy(f => f.LastModified).ToList();
                    for (int i = 1; i < ordered.Count; i++) ordered[i].IsSelected = true;
                    g.Files = ordered;
                    result.Add(g);
                }
            }

            return result.OrderByDescending(g => g.WastedBytes).ToList();
        }

        private static string ComputeHash(string path)
        {
            using (var sha1 = SHA1.Create())
            using (var stream = File.OpenRead(path))
            {
                var hash = sha1.ComputeHash(stream);
                return BitConverter.ToString(hash);
            }
        }

        public static long RemoveSelected(List<DuplicateFileEntry> files, string batchId)
        {
            var validPaths = files
                .Where(f => !SafetyGuard.IsProtectedPath(Path.GetDirectoryName(f.Path)) && File.Exists(f.Path))
                .Select(f => f.Path)
                .ToList();
            // אצווה אחת - טוענת/שומרת את מניפסט ההסגר פעם אחת לכל הקבצים הנבחרים
            // במקום פעם לכל קובץ (איטי מאוד ברגע שהמניפסט גדל עם השימוש).
            return Quarantine.QuarantineItems(validPaths, batchId);
        }
    }
}
