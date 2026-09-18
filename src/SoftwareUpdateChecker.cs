using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace UninstallerPro
{
    public class UpdatableProgram
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string CurrentVersion { get; set; }
        public string AvailableVersion { get; set; }
        public string Source { get; set; }
    }

    // בודק עדכונים לתוכנות מותקנות דרך winget (Windows Package Manager) -
    // הקטלוג הרשמי והמתוחזק ביותר של מיקרוסופט, במקום לגרד את הרשת בעצמנו
    // (מה שכל כלי מתחרה בתחום הזה עושה, ולכן סובל מתוצאות שגויות, גרסאות
    // בטא שמזוהות כ"עדכון", וקישורי שותפים). מעדכן תוכנה אחת בכל פעם ולא
    // עם --all, כי winget upgrade --all ידוע כבעייתי בטיפול בהרשאות מנהל.
    public static class SoftwareUpdateChecker
    {
        private static Tuple<int, string> RunWinget(string args, int timeoutMs = 60000)
        {
            try
            {
                var psi = new ProcessStartInfo("winget.exe", args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                var stdout = new StringBuilder();
                using (var p = new Process { StartInfo = psi })
                {
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) Logger.Log("winget stderr: " + e.Data); };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    bool exited = p.WaitForExit(timeoutMs);
                    if (!exited) { try { p.Kill(); } catch { } return Tuple.Create(-1, "timeout"); }
                    return Tuple.Create(p.ExitCode, stdout.ToString());
                }
            }
            catch (Exception ex)
            {
                Logger.Log("winget.exe launch failed: " + ex.Message);
                return Tuple.Create(-1, ex.Message);
            }
        }

        public static bool IsWingetAvailable()
        {
            var result = RunWinget("--version", 8000);
            return result.Item1 == 0;
        }

        public static List<UpdatableProgram> GetAvailableUpdates()
        {
            var result = RunWinget("upgrade --include-unknown --accept-source-agreements", 90000);
            var list = new List<UpdatableProgram>();
            if (string.IsNullOrWhiteSpace(result.Item2)) return list;

            var lines = result.Item2.Replace("\r", "").Split('\n');
            int headerIdx = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("Name") && lines[i].Contains("Id") && lines[i].Contains("Version"))
                {
                    headerIdx = i;
                    break;
                }
            }
            if (headerIdx < 0 || headerIdx + 2 >= lines.Length) return list;

            var header = lines[headerIdx];
            int colName = header.IndexOf("Name", StringComparison.Ordinal);
            int colId = header.IndexOf("Id", colName + 1, StringComparison.Ordinal);
            int colVersion = header.IndexOf("Version", colId + 1, StringComparison.Ordinal);
            int colAvailable = header.IndexOf("Available", colVersion + 1, StringComparison.Ordinal);
            int colSource = header.IndexOf("Source", colAvailable + 1, StringComparison.Ordinal);
            if (colName < 0 || colId < 0 || colVersion < 0 || colAvailable < 0) return list;

            Func<string, int, int, string> slice = (line, start, end) =>
            {
                if (start >= line.Length) return "";
                var len = (end < 0 || end > line.Length) ? line.Length - start : end - start;
                if (len <= 0) return "";
                return line.Substring(start, len).Trim();
            };

            for (int i = headerIdx + 2; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.TrimStart().StartsWith("-")) continue;
                if (line.Contains("upgrades available") || line.Contains("No applicable")) break;
                if (line.Length <= colId) continue;

                var name = slice(line, colName, colId);
                var id = slice(line, colId, colVersion);
                var version = slice(line, colVersion, colAvailable);
                var available = colSource > 0 ? slice(line, colAvailable, colSource) : slice(line, colAvailable, -1);
                var source = colSource > 0 ? slice(line, colSource, -1) : "";

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id)) continue;
                // חבילות "unknown" ל-winget (ID = "unknown") לא ניתנות לעדכון בפועל - מדלגים
                if (id.Equals("unknown", StringComparison.OrdinalIgnoreCase)) continue;

                list.Add(new UpdatableProgram { Name = name, Id = id, CurrentVersion = version, AvailableVersion = available, Source = source });
            }
            return list;
        }

        public static bool UpdateOne(string id, out string output)
        {
            var result = RunWinget("upgrade --id \"" + id.Replace("\"", "") + "\" --silent --accept-package-agreements --accept-source-agreements -h", 300000);
            output = result.Item2;
            Logger.Log("winget upgrade " + id + " -> exit " + result.Item1);
            return result.Item1 == 0;
        }
    }
}
