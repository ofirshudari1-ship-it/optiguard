using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;

namespace UninstallerPro
{
    public class InfoRow
    {
        public string Label { get; set; }
        public string Value { get; set; }
    }

    public static class SystemInfoData
    {
        private static string QueryFirst(string wql, string prop)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(wql))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var v = mo[prop];
                        if (v != null) return v.ToString().Trim();
                    }
                }
            }
            catch { }
            return null;
        }

        public static List<InfoRow> GetCpuInfo()
        {
            var rows = new List<InfoRow>();
            rows.Add(new InfoRow { Label = I18n.T("si_cpu_name"), Value = QueryFirst("SELECT Name FROM Win32_Processor", "Name") ?? "-" });
            rows.Add(new InfoRow { Label = I18n.T("si_cpu_cores"), Value = QueryFirst("SELECT NumberOfCores FROM Win32_Processor", "NumberOfCores") ?? "-" });
            rows.Add(new InfoRow { Label = I18n.T("si_cpu_threads"), Value = QueryFirst("SELECT NumberOfLogicalProcessors FROM Win32_Processor", "NumberOfLogicalProcessors") ?? "-" });
            var clock = QueryFirst("SELECT MaxClockSpeed FROM Win32_Processor", "MaxClockSpeed");
            rows.Add(new InfoRow { Label = I18n.T("si_cpu_clock"), Value = clock != null ? (Math.Round(Convert.ToDouble(clock) / 1000.0, 2) + " GHz") : "-" });
            return rows;
        }

        public static List<InfoRow> GetMemoryInfo()
        {
            var rows = new List<InfoRow>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        double totalKb = Convert.ToDouble(mo["TotalVisibleMemorySize"]);
                        double freeKb = Convert.ToDouble(mo["FreePhysicalMemory"]);
                        double totalGb = Math.Round(totalKb / 1024.0 / 1024.0, 1);
                        double usedGb = Math.Round((totalKb - freeKb) / 1024.0 / 1024.0, 1);
                        rows.Add(new InfoRow { Label = I18n.T("si_ram_total"), Value = totalGb + " GB" });
                        rows.Add(new InfoRow { Label = I18n.T("si_ram_used"), Value = usedGb + " GB (" + Math.Round(usedGb / totalGb * 100, 0) + "%)" });
                    }
                }
            }
            catch { rows.Add(new InfoRow { Label = I18n.T("si_ram_total"), Value = "-" }); }
            return rows;
        }

        public static List<InfoRow> GetGpuInfo()
        {
            var rows = new List<InfoRow>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var name = mo["Name"] as string;
                        if (string.IsNullOrEmpty(name)) continue;
                        double? ramGb = null;
                        try { var ram = Convert.ToUInt64(mo["AdapterRAM"]); if (ram > 0) ramGb = Math.Round(ram / 1024.0 / 1024.0 / 1024.0, 1); } catch { }
                        rows.Add(new InfoRow { Label = name, Value = ramGb.HasValue ? (ramGb.Value + " GB VRAM") : "-" });
                    }
                }
            }
            catch { }
            if (rows.Count == 0) rows.Add(new InfoRow { Label = I18n.T("si_gpu"), Value = "-" });
            return rows;
        }

        public static List<InfoRow> GetOsInfo()
        {
            var rows = new List<InfoRow>();
            rows.Add(new InfoRow { Label = I18n.T("si_os_name"), Value = QueryFirst("SELECT Caption FROM Win32_OperatingSystem", "Caption") ?? Environment.OSVersion.VersionString });
            rows.Add(new InfoRow { Label = I18n.T("si_os_version"), Value = QueryFirst("SELECT Version FROM Win32_OperatingSystem", "Version") ?? "-" });
            rows.Add(new InfoRow { Label = I18n.T("si_os_arch"), Value = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit" });
            rows.Add(new InfoRow { Label = I18n.T("si_os_computername"), Value = Environment.MachineName });
            rows.Add(new InfoRow { Label = I18n.T("si_os_user"), Value = Environment.UserName });
            return rows;
        }

        public static List<InfoRow> GetMotherboardInfo()
        {
            var rows = new List<InfoRow>();
            var manuf = QueryFirst("SELECT Manufacturer FROM Win32_BaseBoard", "Manufacturer");
            var product = QueryFirst("SELECT Product FROM Win32_BaseBoard", "Product");
            rows.Add(new InfoRow { Label = I18n.T("si_mb_model"), Value = ((manuf ?? "") + " " + (product ?? "")).Trim() });
            var bios = QueryFirst("SELECT SMBIOSBIOSVersion FROM Win32_BIOS", "SMBIOSBIOSVersion");
            rows.Add(new InfoRow { Label = I18n.T("si_mb_bios"), Value = bios ?? "-" });
            return rows;
        }

        public static List<InfoRow> GetDisksInfo()
        {
            var rows = new List<InfoRow>();
            foreach (var d in DriveInfo.GetDrives())
            {
                if (!d.IsReady) continue;
                double totalGb = Math.Round(d.TotalSize / 1024.0 / 1024.0 / 1024.0, 1);
                double freeGb = Math.Round(d.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0, 1);
                rows.Add(new InfoRow { Label = d.Name + " (" + d.DriveFormat + ")", Value = freeGb + " GB " + I18n.T("si_free_of") + " " + totalGb + " GB" });
            }
            return rows;
        }

        public static string BuildFullReport()
        {
            var sb = new System.Text.StringBuilder();
            Action<string, List<InfoRow>> AddSection = (title, rows) =>
            {
                sb.AppendLine("== " + title + " ==");
                foreach (var r in rows) sb.AppendLine(r.Label + ": " + r.Value);
                sb.AppendLine();
            };
            AddSection(I18n.T("si_section_os"), GetOsInfo());
            AddSection(I18n.T("si_section_cpu"), GetCpuInfo());
            AddSection(I18n.T("si_section_ram"), GetMemoryInfo());
            AddSection(I18n.T("si_section_gpu"), GetGpuInfo());
            AddSection(I18n.T("si_section_motherboard"), GetMotherboardInfo());
            AddSection(I18n.T("si_section_disks"), GetDisksInfo());
            return sb.ToString();
        }
    }
}
