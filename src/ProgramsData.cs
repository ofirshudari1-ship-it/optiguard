using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace UninstallerPro
{
    public static class ProgramsData
    {
        private static readonly Tuple<RegistryHive, string>[] UninstallRoots = new[]
        {
            Tuple.Create(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            Tuple.Create(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            Tuple.Create(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        };

        public static List<InstalledProgram> GetInstalledPrograms()
        {
            var result = new List<InstalledProgram>();
            foreach (var root in UninstallRoots)
            {
                RegistryKey baseKey;
                try { baseKey = RegistryUtil.OpenBase(root.Item1); } catch { continue; }
                using (baseKey)
                using (var uninstallKey = baseKey.OpenSubKey(root.Item2))
                {
                    if (uninstallKey == null) continue;
                    foreach (var subName in uninstallKey.GetSubKeyNames())
                    {
                        using (var sub = uninstallKey.OpenSubKey(subName))
                        {
                            if (sub == null) continue;
                            var displayName = sub.GetValue("DisplayName") as string;
                            if (string.IsNullOrEmpty(displayName)) continue;
                            double? sizeMb = null;
                            var estSize = sub.GetValue("EstimatedSize");
                            if (estSize != null)
                            {
                                try { sizeMb = Math.Round(Convert.ToDouble(estSize) / 1024.0, 1); } catch { }
                            }
                            int sysComp = 0;
                            try { sysComp = Convert.ToInt32(sub.GetValue("SystemComponent") ?? 0); } catch { }
                            var publisher = sub.GetValue("Publisher") as string;
                            result.Add(new InstalledProgram
                            {
                                DisplayName = displayName,
                                Publisher = publisher,
                                Category = Categorizer.Categorize(displayName, publisher),
                                DisplayVersion = sub.GetValue("DisplayVersion") as string,
                                SizeMB = sizeMb,
                                InstallDate = sub.GetValue("InstallDate") as string,
                                UninstallString = sub.GetValue("UninstallString") as string,
                                QuietUninstallString = sub.GetValue("QuietUninstallString") as string,
                                InstallLocation = sub.GetValue("InstallLocation") as string,
                                Hive = root.Item1,
                                SubKeyPath = root.Item2 + "\\" + subName,
                                SystemComponent = sysComp != 0
                            });
                        }
                    }
                }
            }
            foreach (var p in result)
            {
                try { SafeRemovalData.Apply(p); } catch { p.SafeRemovalLevel = "unknown"; }
            }
            return result.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private static IEnumerable<Tuple<string, string>> ResidualScanRoots()
        {
            var localLow = Path.Combine(Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)).FullName, "LocalLow");
            var roots = new[]
            {
                Tuple.Create(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Program Files"),
                Tuple.Create(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Program Files (x86)"),
                Tuple.Create(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ProgramData"),
                Tuple.Create(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppData\\Roaming"),
                Tuple.Create(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppData\\Local"),
                Tuple.Create(localLow, "AppData\\LocalLow"),
                Tuple.Create(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs"), "Start Menu (User)"),
                Tuple.Create(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs"), "Start Menu (Common)"),
            };
            return roots.Where(r => !string.IsNullOrEmpty(r.Item1) && Directory.Exists(r.Item1));
        }

        public static List<ResidualItem> FindResidualItems(string displayName, string publisher, string installLocation)
        {
            var items = new List<ResidualItem>();
            var nameTokens = NameMatcher.GetTokens(displayName);
            var pubTokens = NameMatcher.GetTokens(publisher);

            if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation) && !SafetyGuard.IsProtectedPath(installLocation))
            {
                items.Add(new ResidualItem { Type = ResidualType.Folder, Path = installLocation, DisplayPath = installLocation, Reason = I18n.T("residual_reason_install_folder") });
            }

            foreach (var root in ResidualScanRoots())
            {
                string[] children;
                try { children = Directory.GetDirectories(root.Item1); } catch { children = new string[0]; }
                foreach (var c in children)
                {
                    if (SafetyGuard.IsProtectedPath(c)) continue;
                    var name = Path.GetFileName(c);
                    var m = NameMatcher.TestNameMatch(name, displayName, nameTokens);
                    if (m != null)
                    {
                        items.Add(new ResidualItem { Type = ResidualType.Folder, Path = c, DisplayPath = c, Reason = string.Format(I18n.T("residual_reason_under_root"), m, root.Item2) });
                        continue;
                    }
                    if (NameMatcher.TestPublisherMatch(name, pubTokens))
                    {
                        string[] grand;
                        try { grand = Directory.GetDirectories(c); } catch { grand = new string[0]; }
                        foreach (var g in grand)
                        {
                            if (SafetyGuard.IsProtectedPath(g)) continue;
                            var gName = Path.GetFileName(g);
                            var m2 = NameMatcher.TestNameMatch(gName, displayName, nameTokens);
                            if (m2 != null) items.Add(new ResidualItem { Type = ResidualType.Folder, Path = g, DisplayPath = g, Reason = string.Format(I18n.T("residual_reason_under_publisher"), m2, name, root.Item2) });
                        }
                    }
                }
                string[] shortcuts;
                try { shortcuts = Directory.GetFiles(root.Item1, "*.lnk"); } catch { shortcuts = new string[0]; }
                foreach (var s in shortcuts)
                {
                    var baseName = Path.GetFileNameWithoutExtension(s);
                    var m = NameMatcher.TestNameMatch(baseName, displayName, nameTokens);
                    if (m != null) items.Add(new ResidualItem { Type = ResidualType.Shortcut, Path = s, DisplayPath = s, Reason = string.Format(I18n.T("residual_reason_shortcut"), m) });
                }
            }

            foreach (var ur in UninstallRoots)
            {
                RegistryKey baseKey;
                try { baseKey = RegistryUtil.OpenBase(ur.Item1); } catch { continue; }
                using (baseKey)
                using (var uninstallKey = baseKey.OpenSubKey(ur.Item2))
                {
                    if (uninstallKey == null) continue;
                    foreach (var subName in uninstallKey.GetSubKeyNames())
                    {
                        using (var sub = uninstallKey.OpenSubKey(subName))
                        {
                            var dn = sub != null ? sub.GetValue("DisplayName") as string : null;
                            if (string.IsNullOrEmpty(dn)) continue;
                            var m = NameMatcher.TestNameMatch(dn, displayName, nameTokens);
                            if (m != null)
                            {
                                var fullPath = ur.Item2 + "\\" + subName;
                                items.Add(new ResidualItem { Type = ResidualType.Registry, Hive = ur.Item1, SubKeyPath = fullPath, Path = fullPath, DisplayPath = HiveName(ur.Item1) + "\\" + fullPath, Reason = string.Format(I18n.T("residual_reason_uninstall_entry"), m) });
                            }
                        }
                    }
                }
            }

            var softwareRoots = new[]
            {
                Tuple.Create(RegistryHive.CurrentUser, "SOFTWARE"),
                Tuple.Create(RegistryHive.LocalMachine, "SOFTWARE"),
                Tuple.Create(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node"),
            };
            foreach (var sr in softwareRoots)
            {
                RegistryKey baseKey;
                try { baseKey = RegistryUtil.OpenBase(sr.Item1); } catch { continue; }
                using (baseKey)
                using (var swKey = baseKey.OpenSubKey(sr.Item2))
                {
                    if (swKey == null) continue;
                    foreach (var keyName in swKey.GetSubKeyNames())
                    {
                        if (SafetyGuard.IsProtectedName(keyName)) continue;
                        var m = NameMatcher.TestNameMatch(keyName, displayName, nameTokens);
                        if (m != null)
                        {
                            var fullPath = sr.Item2 + "\\" + keyName;
                            items.Add(new ResidualItem { Type = ResidualType.Registry, Hive = sr.Item1, SubKeyPath = fullPath, Path = fullPath, DisplayPath = HiveName(sr.Item1) + "\\" + fullPath, Reason = string.Format(I18n.T("residual_reason_software_key"), m, HiveName(sr.Item1) + "\\" + sr.Item2) });
                        }
                    }
                }
            }

            return items
                .GroupBy(i => i.Type + "|" + i.Path)
                .Select(g => g.First())
                .OrderBy(i => i.Type)
                .ThenBy(i => i.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string HiveName(RegistryHive hive)
        {
            switch (hive)
            {
                case RegistryHive.LocalMachine: return "HKLM";
                case RegistryHive.CurrentUser: return "HKCU";
                default: return hive.ToString();
            }
        }

        public static bool RemoveResidualItem(ResidualItem item)
        {
            try
            {
                if (item.Type == ResidualType.Registry)
                {
                    if (RegistryUtil.SubKeyExists(item.Hive, item.SubKeyPath))
                    {
                        var ok = RegistryUtil.DeleteSubKeyTree(item.Hive, item.SubKeyPath);
                        if (ok) Logger.Log("נמחק מפתח רישום: " + item.DisplayPath);
                        return ok;
                    }
                }
                else
                {
                    if (SafetyGuard.IsProtectedPath(item.Path)) { Logger.Log("דילוג (נתיב מוגן): " + item.Path); return false; }
                    if (item.Type == ResidualType.Folder && Directory.Exists(item.Path))
                    {
                        Directory.Delete(item.Path, true);
                        Logger.Log("נמחקה תיקייה: " + item.Path);
                        SafetyGuard.RemoveEmptyParentChain(Directory.GetParent(item.Path).FullName);
                        return true;
                    }
                    if (item.Type == ResidualType.Shortcut && File.Exists(item.Path))
                    {
                        File.Delete(item.Path);
                        Logger.Log("נמחק קיצור דרך: " + item.Path);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("שגיאה במחיקת " + item.Path + ": " + ex.Message);
            }
            return false;
        }

        public static bool RunUninstallString(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return false;
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c " + cmd) { UseShellExecute = true };
                var p = Process.Start(psi);
                p.WaitForExit();
                Logger.Log("הרצת הסרה: " + cmd + " (קוד יציאה: " + p.ExitCode + ")");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log("שגיאה בהרצת הסרה: " + cmd + " -> " + ex.Message);
                return false;
            }
        }
    }
}
