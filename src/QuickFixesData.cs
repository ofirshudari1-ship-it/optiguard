using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace UninstallerPro
{
    public class FixAction
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public Action Run { get; set; }
    }

    public static class QuickFixesData
    {
        public static List<FixAction> GetActions()
        {
            return new List<FixAction>
            {
                new FixAction
                {
                    Key = "flush_dns", Name = I18n.T("fix_flush_dns"), Description = I18n.T("fix_flush_dns_desc"),
                    Run = () => RunHiddenCommand("ipconfig", "/flushdns")
                },
                new FixAction
                {
                    Key = "restart_explorer", Name = I18n.T("fix_restart_explorer"), Description = I18n.T("fix_restart_explorer_desc"),
                    Run = () =>
                    {
                        foreach (var p in Process.GetProcessesByName("explorer")) { try { p.Kill(); } catch { } }
                        System.Threading.Thread.Sleep(500);
                        Process.Start("explorer.exe");
                    }
                },
                new FixAction
                {
                    Key = "disk_cleanup", Name = I18n.T("fix_disk_cleanup"), Description = I18n.T("fix_disk_cleanup_desc"),
                    Run = () => Process.Start("cleanmgr.exe")
                },
                new FixAction
                {
                    Key = "sfc_scan", Name = I18n.T("fix_sfc_scan"), Description = I18n.T("fix_sfc_scan_desc"),
                    Run = () => Process.Start(new ProcessStartInfo("cmd.exe", "/k sfc /scannow") { UseShellExecute = true })
                },
                new FixAction
                {
                    Key = "winsock_reset", Name = I18n.T("fix_winsock_reset"), Description = I18n.T("fix_winsock_reset_desc"),
                    Run = () => Process.Start(new ProcessStartInfo("cmd.exe", "/k netsh winsock reset") { UseShellExecute = true })
                },
                new FixAction
                {
                    Key = "windows_update_troubleshooter", Name = I18n.T("fix_wu_troubleshooter"), Description = I18n.T("fix_wu_troubleshooter_desc"),
                    Run = () => Process.Start(new ProcessStartInfo("msdt.exe", "/id WindowsUpdateDiagnostic") { UseShellExecute = true })
                },
            };
        }

        private static void RunHiddenCommand(string exe, string args)
        {
            try
            {
                Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }).WaitForExit();
                Logger.Log("הורץ תיקון: " + exe + " " + args);
            }
            catch (Exception ex) { Logger.Log("שגיאה בהרצת תיקון " + exe + ": " + ex.Message); }
        }
    }
}
