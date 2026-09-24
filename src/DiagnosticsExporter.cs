using System;
using System.IO;
using System.IO.Compression;
using System.Security.Principal;
using System.Text;

namespace UninstallerPro
{
    // Settings > About > "Export Diagnostics" (added 4.17.0). Bundles the
    // handful of files a support request actually needs into one .zip:
    // app log(s), the shipped version.json, settings.json, and a small
    // system-info.txt - nothing else. No registry dumps, no full file
    // listings, no telemetry.
    public static class DiagnosticsExporter
    {
        public static string DefaultFileName()
        {
            return string.Format("OptiGuard-Diagnostics-{0}-{1}.zip", Program.AppVersion, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        }

        // Builds the zip at destZipPath (overwritten if it already exists).
        // Every piece is collected best-effort (try/catch per item) so one
        // missing/locked file (e.g. the active log while a delete is in
        // progress) never blocks the whole export.
        public static void Export(string destZipPath)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "OptiGuard-Diagnostics-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                CopyLogs(tempDir);
                CopyVersionJson(tempDir);
                CopySettingsJson(tempDir);
                WriteSystemInfo(tempDir);

                if (File.Exists(destZipPath)) File.Delete(destZipPath);
                ZipFile.CreateFromDirectory(tempDir, destZipPath, CompressionLevel.Optimal, false);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        private static void CopyLogs(string tempDir)
        {
            try
            {
                if (!Directory.Exists(AppPaths.DataDir)) return;
                var logsDir = Path.Combine(tempDir, "logs");
                Directory.CreateDirectory(logsDir);
                foreach (var f in Directory.GetFiles(AppPaths.DataDir, "Log_*.txt"))
                {
                    try { File.Copy(f, Path.Combine(logsDir, Path.GetFileName(f)), true); } catch { }
                }
            }
            catch { }
        }

        // version.json is written into the install directory by the installer
        // (Setup.cs WriteResourceToFile, same pattern as CHANGELOG.md/EULA.md
        // already used by "View Changelog"/"View License"). If it's missing -
        // e.g. a dev build launched straight from bin\ without going through
        // Setup - it's simply skipped rather than failing the whole export.
        private static void CopyVersionJson(string tempDir)
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.json");
                if (File.Exists(path)) File.Copy(path, Path.Combine(tempDir, "version.json"), true);
            }
            catch { }
        }

        // AppSettings (Settings.cs) has no API-key/token/password/credential-
        // shaped fields as of this writing - it's theme/language/toggle/window-
        // position data. Included as-is. If a future setting ever looks like a
        // credential, redact it here before adding new fields to Settings.cs.
        private static void CopySettingsJson(string tempDir)
        {
            try
            {
                if (File.Exists(AppPaths.SettingsFile)) File.Copy(AppPaths.SettingsFile, Path.Combine(tempDir, "settings.json"), true);
            }
            catch { }
        }

        private static void WriteSystemInfo(string tempDir)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("OptiGuard Diagnostics");
                sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine();
                sb.AppendLine("OptiGuard version: " + Program.AppVersion);
                sb.AppendLine("OS version: " + Environment.OSVersion.VersionString + " (" + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit") + ")");
                sb.AppendLine(".NET version: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
                sb.AppendLine("Install path: " + AppDomain.CurrentDomain.BaseDirectory);
                sb.AppendLine("Running elevated: " + (IsElevated() ? "Yes" : "No"));
                File.WriteAllText(Path.Combine(tempDir, "system-info.txt"), sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static bool IsElevated()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }
    }
}
