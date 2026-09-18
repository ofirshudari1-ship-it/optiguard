using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;

namespace UninstallerPro
{
    public class DiskRow
    {
        public string Drive { get; set; }
        public string TotalText { get; set; }
        public string FreeText { get; set; }
        public string UsedPercentText { get; set; }
        public int UsedPercent { get; set; }
        public string Health { get; set; }
        public bool HealthOk { get; set; }
    }

    public static class DiskHealthData
    {
        public static List<DiskRow> GetDrives()
        {
            var healthByModel = new Dictionary<string, string>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Model, Status FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var model = mo["Model"] as string;
                        var status = mo["Status"] as string;
                        if (model != null) healthByModel[model] = status ?? "-";
                    }
                }
            }
            catch { }
            bool overallHealthOk = !healthByModel.Values.Any(v => v != "OK");
            var overallHealth = overallHealthOk ? I18n.T("disk_health_ok") : I18n.T("disk_health_warning");
            if (healthByModel.Count == 0) { overallHealth = I18n.T("disk_health_unknown"); overallHealthOk = true; }

            var rows = new List<DiskRow>();
            foreach (var d in DriveInfo.GetDrives())
            {
                if (!d.IsReady) continue;
                double totalGb = Math.Round(d.TotalSize / 1024.0 / 1024.0 / 1024.0, 1);
                double freeGb = Math.Round(d.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0, 1);
                int usedPct = totalGb > 0 ? (int)Math.Round((totalGb - freeGb) / totalGb * 100) : 0;
                rows.Add(new DiskRow
                {
                    Drive = d.Name + " (" + d.DriveFormat + ")",
                    TotalText = totalGb + " GB",
                    FreeText = freeGb + " GB",
                    UsedPercentText = usedPct + "%",
                    UsedPercent = usedPct,
                    Health = overallHealth,
                    HealthOk = overallHealthOk
                });
            }
            return rows;
        }

        public static string ScanDrive(string driveLetter)
        {
            try
            {
                var psi = new ProcessStartInfo("chkdsk.exe", driveLetter + " /scan")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.Default
                };
                var stdout = new StringBuilder();
                using (var p = new Process { StartInfo = psi })
                {
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    p.WaitForExit(120000);
                }
                Logger.Log("הרצת chkdsk /scan על " + driveLetter);
                return stdout.ToString();
            }
            catch (Exception ex)
            {
                return I18n.T("disk_scan_error") + ": " + ex.Message;
            }
        }
    }
}
