using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UninstallerPro
{
    public class SpaceEntry
    {
        public string Name;
        public string Path;
        public long SizeBytes;
        public bool IsDirectory;
        public double PercentOfParent;

        public string SizeText { get { return JunkCleanerData.FormatSize(SizeBytes); } }
        public string PercentText { get { return Math.Round(PercentOfParent, 1) + "%"; } }
        public string Icon { get { return IsDirectory ? "📁" : "📄"; } }
    }

    // מנתח שטח דיסק: "מה תופס לי מקום" בפירוק לפי תיקיות/קבצים, עם אפשרות
    // לצלול פנימה. לא treemap גרפי מורכב - רשימה ממוינת עם פס-אחוז יחסי,
    // הרבה יותר קל לתחזוקה ומהיר לבנות, ונותן בדיוק את אותה תשובה שהמשתמש מחפש.
    public static class DiskSpaceAnalyzerData
    {
        public static List<SpaceEntry> AnalyzeFolder(string rootPath)
        {
            var result = new List<SpaceEntry>();
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) return result;

            try
            {
                foreach (var dir in Directory.GetDirectories(rootPath))
                {
                    long size = 0;
                    try { size = DirSize(dir); } catch { }
                    result.Add(new SpaceEntry { Name = Path.GetFileName(dir), Path = dir, SizeBytes = size, IsDirectory = true });
                }
            }
            catch { }

            try
            {
                foreach (var file in Directory.GetFiles(rootPath))
                {
                    long size = 0;
                    try { size = new FileInfo(file).Length; } catch { }
                    result.Add(new SpaceEntry { Name = Path.GetFileName(file), Path = file, SizeBytes = size, IsDirectory = false });
                }
            }
            catch { }

            long total = result.Sum(e => e.SizeBytes);
            foreach (var e in result) e.PercentOfParent = total > 0 ? (e.SizeBytes * 100.0 / total) : 0;

            return result.OrderByDescending(e => e.SizeBytes).ToList();
        }

        private static long DirSize(string path)
        {
            long total = 0;
            try
            {
                foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(f).Length; } catch { }
                }
            }
            catch { }
            return total;
        }

        public static List<SpaceEntry> GetLargestFiles(string rootPath, int count, Action<string> onProgress = null)
        {
            var result = new List<SpaceEntry>();
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) return result;
            int scanned = 0;
            try
            {
                foreach (var f in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
                {
                    scanned++;
                    if (onProgress != null && scanned % 500 == 0) onProgress(scanned.ToString());
                    long size;
                    try { size = new FileInfo(f).Length; } catch { continue; }
                    result.Add(new SpaceEntry { Name = Path.GetFileName(f), Path = f, SizeBytes = size, IsDirectory = false });
                }
            }
            catch { }
            var top = result.OrderByDescending(e => e.SizeBytes).Take(count).ToList();
            long maxSize = top.Count > 0 ? top[0].SizeBytes : 0;
            foreach (var e in top) e.PercentOfParent = maxSize > 0 ? (e.SizeBytes * 100.0 / maxSize) : 0;
            return top;
        }
    }
}
