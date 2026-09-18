using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace UninstallerPro
{
    public static class GamesData
    {
        private static List<string> GetSteamLibraryPaths()
        {
            string steamPath = null;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (k != null) steamPath = k.GetValue("SteamPath") as string;
                }
            }
            catch { }
            if (string.IsNullOrEmpty(steamPath))
            {
                try
                {
                    using (var baseKey = RegistryUtil.OpenBase(RegistryHive.LocalMachine))
                    using (var k = baseKey.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                    {
                        if (k != null) steamPath = k.GetValue("InstallPath") as string;
                    }
                }
                catch { }
            }
            var libs = new List<string>();
            if (string.IsNullOrEmpty(steamPath)) return libs;
            steamPath = steamPath.Replace('/', '\\');
            if (!Directory.Exists(steamPath)) return libs;
            libs.Add(steamPath);
            var vdf = Path.Combine(steamPath, @"steamapps\libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                var text = File.ReadAllText(vdf);
                foreach (Match m in Regex.Matches(text, "\"path\"\\s*\"([^\"]*)\""))
                {
                    var p = m.Groups[1].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(p) && !libs.Contains(p, StringComparer.OrdinalIgnoreCase)) libs.Add(p);
                }
            }
            return libs;
        }

        public static List<GameInfo> GetSteamGames()
        {
            var games = new List<GameInfo>();
            foreach (var lib in GetSteamLibraryPaths())
            {
                var steamapps = Path.Combine(lib, "steamapps");
                if (!Directory.Exists(steamapps)) continue;
                foreach (var manifest in Directory.GetFiles(steamapps, "appmanifest_*.acf"))
                {
                    var text = File.ReadAllText(manifest);
                    var name = Regex.Match(text, "\"name\"\\s*\"([^\"]*)\"").Groups[1].Value;
                    var installDir = Regex.Match(text, "\"installdir\"\\s*\"([^\"]*)\"").Groups[1].Value;
                    var appid = Regex.Match(Path.GetFileName(manifest), @"appmanifest_(\d+)\.acf").Groups[1].Value;
                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(installDir))
                    {
                        games.Add(new GameInfo { Source = "Steam", Name = name, AppId = appid, InstallDir = Path.Combine(steamapps, "common", installDir), ManifestPath = manifest });
                    }
                }
            }
            return games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static List<GameInfo> GetEpicGames()
        {
            var games = new List<GameInfo>();
            var manifestDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Epic\EpicGamesLauncher\Data\Manifests");
            if (!Directory.Exists(manifestDir)) return games;
            var serializer = new JavaScriptSerializer();
            foreach (var file in Directory.GetFiles(manifestDir, "*.item"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var dict = serializer.Deserialize<Dictionary<string, object>>(json);
                    double? sizeMb = null;
                    if (dict.ContainsKey("InstallSize") && dict["InstallSize"] != null)
                    {
                        try { sizeMb = Math.Round(Convert.ToDouble(dict["InstallSize"]) / 1024.0 / 1024.0, 1); } catch { }
                    }
                    games.Add(new GameInfo
                    {
                        Source = "Epic Games",
                        Name = dict.ContainsKey("DisplayName") ? dict["DisplayName"] as string : null,
                        AppId = dict.ContainsKey("AppName") ? dict["AppName"] as string : null,
                        InstallDir = dict.ContainsKey("InstallLocation") ? dict["InstallLocation"] as string : null,
                        ManifestPath = file,
                        CatalogNamespace = dict.ContainsKey("CatalogNamespace") ? dict["CatalogNamespace"] as string : null,
                        CatalogItemId = dict.ContainsKey("CatalogItemId") ? dict["CatalogItemId"] as string : null,
                        SizeMB = sizeMb
                    });
                }
                catch { }
            }
            return games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static List<GameInfo> GetGogGames()
        {
            var games = new List<GameInfo>();
            try
            {
                using (var baseKey = RegistryUtil.OpenBase(RegistryHive.LocalMachine))
                using (var root = baseKey.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games"))
                {
                    if (root == null) return games;
                    foreach (var sub in root.GetSubKeyNames())
                    {
                        using (var k = root.OpenSubKey(sub))
                        {
                            var name = k.GetValue("gameName") as string;
                            if (string.IsNullOrEmpty(name)) continue;
                            games.Add(new GameInfo
                            {
                                Source = "GOG", Name = name, AppId = sub, InstallDir = k.GetValue("path") as string,
                                Hive = RegistryHive.LocalMachine, SubKeyPath = @"SOFTWARE\WOW6432Node\GOG.com\Games\" + sub
                            });
                        }
                    }
                }
            }
            catch { }
            return games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static List<GameInfo> GetUbisoftGames()
        {
            var games = new List<GameInfo>();
            try
            {
                using (var baseKey = RegistryUtil.OpenBase(RegistryHive.LocalMachine))
                using (var root = baseKey.OpenSubKey(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs"))
                {
                    if (root == null) return games;
                    foreach (var sub in root.GetSubKeyNames())
                    {
                        using (var k = root.OpenSubKey(sub))
                        {
                            var dir = k.GetValue("InstallDir") as string;
                            if (string.IsNullOrEmpty(dir)) continue;
                            games.Add(new GameInfo
                            {
                                Source = "Ubisoft Connect", Name = Path.GetFileName(dir.TrimEnd('\\')), AppId = sub, InstallDir = dir,
                                Hive = RegistryHive.LocalMachine, SubKeyPath = @"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs\" + sub
                            });
                        }
                    }
                }
            }
            catch { }
            return games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static List<GameInfo> GetAllGames()
        {
            var all = new List<GameInfo>();
            all.AddRange(GetSteamGames());
            all.AddRange(GetEpicGames());
            all.AddRange(GetGogGames());
            all.AddRange(GetUbisoftGames());
            return all.OrderBy(g => g.Source).ThenBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static void OpenOfficialUninstall(GameInfo game)
        {
            try
            {
                if (game.Source == "Steam")
                {
                    Process.Start(new ProcessStartInfo("steam://uninstall/" + game.AppId) { UseShellExecute = true });
                    Logger.Log("בקשת הסרה רשמית ל-Steam AppId " + game.AppId);
                }
                else if (game.Source == "Epic Games")
                {
                    try
                    {
                        var uri = "com.epicgames.launcher://apps/" + game.CatalogNamespace + "%3A" + game.CatalogItemId + "%3A" + game.AppId + "?action=uninstall";
                        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
                    }
                    catch
                    {
                        Process.Start(new ProcessStartInfo("com.epicgames.launcher://") { UseShellExecute = true });
                    }
                    Logger.Log("בקשת הסרה רשמית ל-Epic " + game.Name);
                }
            }
            catch (Exception ex) { Logger.Log("שגיאה בפתיחת הסרה רשמית: " + ex.Message); }
        }

        public static bool ForceRemove(GameInfo game)
        {
            try
            {
                if (!string.IsNullOrEmpty(game.InstallDir) && Directory.Exists(game.InstallDir) && !SafetyGuard.IsProtectedPath(game.InstallDir))
                {
                    Directory.Delete(game.InstallDir, true);
                    Logger.Log("נמחקה תיקיית משחק: " + game.InstallDir);
                }
                if (!string.IsNullOrEmpty(game.ManifestPath) && File.Exists(game.ManifestPath))
                {
                    File.Delete(game.ManifestPath);
                    Logger.Log("נמחק מניפסט: " + game.ManifestPath);
                }
                if (game.Hive.HasValue && !string.IsNullOrEmpty(game.SubKeyPath))
                {
                    RegistryUtil.DeleteSubKeyTree(game.Hive.Value, game.SubKeyPath);
                    Logger.Log("נמחק רישום משחק: " + game.SubKeyPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log("שגיאה בהסרת משחק בכפייה: " + ex.Message);
                return false;
            }
        }
    }
}
