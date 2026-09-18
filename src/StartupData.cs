using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace UninstallerPro
{
    public static class StartupData
    {
        private const string BookmarkSubKeyPath = @"Software\UninstallerPro\DisabledStartup";
        private const string DisabledFolderName = "_DisabledByUninstallerPro";

        private static readonly Tuple<RegistryHive, string, string>[] RunKeys = new[]
        {
            Tuple.Create(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU Run"),
            Tuple.Create(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM Run"),
            Tuple.Create(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM Run (32-bit)"),
        };

        public static List<StartupItem> GetStartupItems()
        {
            var result = new List<StartupItem>();
            foreach (var rk in RunKeys)
            {
                RegistryKey baseKey;
                try { baseKey = RegistryUtil.OpenBase(rk.Item1); } catch { continue; }
                using (baseKey)
                using (var k = baseKey.OpenSubKey(rk.Item2))
                {
                    if (k == null) continue;
                    foreach (var valueName in k.GetValueNames())
                    {
                        if (string.IsNullOrEmpty(valueName)) continue;
                        result.Add(new StartupItem
                        {
                            Enabled = true, Type = StartupType.Registry, Location = rk.Item3,
                            RegHive = rk.Item1, RegSubKey = rk.Item2, ValueName = valueName,
                            Command = k.GetValue(valueName) as string
                        });
                    }
                }
            }

            foreach (var sf in new[]
            {
                Tuple.Create(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "תיקיית הפעלה (משתמש)"),
                Tuple.Create(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "תיקיית הפעלה (משותפת)")
            })
            {
                if (!Directory.Exists(sf.Item1)) continue;
                foreach (var file in Directory.GetFiles(sf.Item1))
                {
                    if (Path.GetFileName(file).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    result.Add(new StartupItem { Enabled = true, Type = StartupType.Folder, Location = sf.Item2, Command = file, FolderPath = file });
                }
            }

            try
            {
                using (var baseKey = RegistryUtil.OpenBase(RegistryHive.CurrentUser))
                using (var bmRoot = baseKey.OpenSubKey(BookmarkSubKeyPath))
                {
                    if (bmRoot != null)
                    {
                        foreach (var id in bmRoot.GetSubKeyNames())
                        {
                            using (var bm = bmRoot.OpenSubKey(id))
                            {
                                var typeStr = bm.GetValue("OrigType") as string;
                                result.Add(new StartupItem
                                {
                                    Enabled = false,
                                    Type = typeStr == "Folder" ? StartupType.Folder : StartupType.Registry,
                                    Location = (bm.GetValue("OrigLocation") as string) + " (מושבת)",
                                    RegHive = RegistryHive.CurrentUser,
                                    RegSubKey = bm.GetValue("OrigRegSubKey") as string,
                                    ValueName = bm.GetValue("OrigValueName") as string,
                                    Command = bm.GetValue("OrigCommand") as string,
                                    FolderPath = bm.GetValue("OrigFolderPath") as string,
                                    BookmarkSubKey = id
                                });
                            }
                        }
                    }
                }
            }
            catch { }

            return result.OrderBy(i => i.Type).ThenBy(i => i.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static void Disable(StartupItem item)
        {
            using (var baseKey = RegistryUtil.OpenBase(RegistryHive.CurrentUser))
            using (var bmRoot = baseKey.CreateSubKey(BookmarkSubKeyPath))
            {
                var id = Guid.NewGuid().ToString("N");
                using (var bm = bmRoot.CreateSubKey(id))
                {
                    bm.SetValue("OrigType", item.Type == StartupType.Folder ? "Folder" : "Registry");
                    bm.SetValue("OrigLocation", item.Location ?? "");
                    bm.SetValue("OrigRegSubKey", item.RegSubKey ?? "");
                    bm.SetValue("OrigValueName", item.ValueName ?? "");
                    bm.SetValue("OrigCommand", item.Command ?? "");
                    bm.SetValue("OrigFolderPath", item.FolderPath ?? "");
                }
            }

            if (item.Type == StartupType.Registry)
            {
                try
                {
                    using (var baseKey = RegistryUtil.OpenBase(item.RegHive))
                    using (var k = baseKey.OpenSubKey(item.RegSubKey, true))
                    {
                        if (k != null) k.DeleteValue(item.ValueName, false);
                    }
                }
                catch { }
            }
            else
            {
                var parent = Directory.GetParent(item.FolderPath).FullName;
                var disabledDir = Path.Combine(parent, DisabledFolderName);
                if (!Directory.Exists(disabledDir)) Directory.CreateDirectory(disabledDir);
                var dest = Path.Combine(disabledDir, Path.GetFileName(item.FolderPath));
                try { File.Move(item.FolderPath, dest); } catch { }
            }
            Logger.Log("הושבת פריט הפעלה אוטומטית: " + item.DisplayName);
        }

        public static void Enable(StartupItem item)
        {
            if (item.Type == StartupType.Registry)
            {
                try
                {
                    using (var baseKey = RegistryUtil.OpenBase(item.RegHive))
                    using (var k = baseKey.CreateSubKey(item.RegSubKey))
                    {
                        k.SetValue(item.ValueName, item.Command ?? "");
                    }
                }
                catch { }
            }
            else
            {
                var parent = Directory.GetParent(item.FolderPath).FullName;
                var disabledPath = Path.Combine(parent, DisabledFolderName, Path.GetFileName(item.FolderPath));
                if (File.Exists(disabledPath))
                {
                    try { File.Move(disabledPath, item.FolderPath); } catch { }
                }
            }
            RemoveBookmark(item);
            Logger.Log("הופעל מחדש פריט הפעלה אוטומטית: " + item.DisplayName);
        }

        public static void RemovePermanently(StartupItem item)
        {
            if (item.Enabled)
            {
                if (item.Type == StartupType.Registry)
                {
                    try
                    {
                        using (var baseKey = RegistryUtil.OpenBase(item.RegHive))
                        using (var k = baseKey.OpenSubKey(item.RegSubKey, true))
                        {
                            if (k != null) k.DeleteValue(item.ValueName, false);
                        }
                    }
                    catch { }
                }
                else if (File.Exists(item.FolderPath))
                {
                    try { File.Delete(item.FolderPath); } catch { }
                }
            }
            else
            {
                var parent = Directory.GetParent(item.FolderPath ?? "").FullName;
                var disabledPath = Path.Combine(parent, DisabledFolderName, Path.GetFileName(item.FolderPath ?? ""));
                if (File.Exists(disabledPath)) { try { File.Delete(disabledPath); } catch { } }
                RemoveBookmark(item);
            }
            Logger.Log("נמחק לצמיתות פריט הפעלה אוטומטית: " + item.DisplayName);
        }

        private static void RemoveBookmark(StartupItem item)
        {
            if (string.IsNullOrEmpty(item.BookmarkSubKey)) return;
            try
            {
                using (var baseKey = RegistryUtil.OpenBase(RegistryHive.CurrentUser))
                using (var bmRoot = baseKey.OpenSubKey(BookmarkSubKeyPath, true))
                {
                    if (bmRoot != null) bmRoot.DeleteSubKeyTree(item.BookmarkSubKey, false);
                }
            }
            catch { }
        }
    }
}
