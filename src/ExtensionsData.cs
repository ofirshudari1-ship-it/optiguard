using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace UninstallerPro
{
    public static class ExtensionsData
    {
        private static readonly HashSet<string> ChromeComponentIds = new HashSet<string>(new[]
        {
            "nmmhkkegccagdldgiimedpiccmgmieda","ghbmnnjooekpmoecnnnilnnbdlolhkhi",
            "pkedcjkdefgpdelpbcmbmeomcjbeemfm","mhjfbmdgcfjbbpaeojofohoefgiehjai","oimompecagnajdejgnnjijobebaeigek"
        });

        // Keys are the raw Chrome/Firefox manifest permission identifiers (fixed,
        // not localized); values are I18n lookups so the human-readable label
        // shown in the Extensions grid follows I18n.CurrentLang instead of being
        // baked into one language.
        private static Dictionary<string, string> HighRiskPermissions
        {
            get
            {
                return new Dictionary<string, string>
                {
                    { "<all_urls>", I18n.T("ext_perm_all_sites") }, { "*://*/*", I18n.T("ext_perm_all_sites") }, { "http://*/*", I18n.T("ext_perm_all_sites_http") }, { "https://*/*", I18n.T("ext_perm_all_sites_https") },
                    { "tabs", I18n.T("ext_perm_tabs") }, { "history", I18n.T("ext_perm_history") }, { "cookies", I18n.T("ext_perm_cookies") },
                    { "webRequest", I18n.T("ext_perm_webrequest") }, { "webRequestBlocking", I18n.T("ext_perm_webrequest_blocking") },
                    { "clipboardRead", I18n.T("ext_perm_clipboard_read") }, { "geolocation", I18n.T("ext_perm_geolocation") }, { "management", I18n.T("ext_perm_management") },
                    { "proxy", I18n.T("ext_perm_proxy") }, { "debugger", I18n.T("ext_perm_debugger") }, { "nativeMessaging", I18n.T("ext_perm_native_messaging") },
                    { "browsingData", I18n.T("ext_perm_browsing_data") }, { "privacy", I18n.T("ext_perm_privacy") }, { "downloads", I18n.T("ext_perm_downloads") },
                };
            }
        }

        private static Dictionary<string, string> MediumRiskPermissions
        {
            get
            {
                return new Dictionary<string, string>
                {
                    { "bookmarks", I18n.T("ext_perm_bookmarks") }, { "topSites", I18n.T("ext_perm_top_sites") }, { "browsingData", I18n.T("ext_perm_browsing_data_alt") },
                    { "contentSettings", I18n.T("ext_perm_content_settings") }, { "identity", I18n.T("ext_perm_identity") }, { "storage", I18n.T("ext_perm_storage") },
                };
            }
        }

        private static void ComputeRisk(BrowserExtension ext, Dictionary<string, object> manifest)
        {
            var reasons = new List<string>();
            var allPerms = new List<string>();
            AddPermList(manifest, "permissions", allPerms);
            AddPermList(manifest, "host_permissions", allPerms);
            AddPermList(manifest, "optional_permissions", allPerms);
            AddPermList(manifest, "optional_host_permissions", allPerms);

            bool high = false, medium = false;
            foreach (var p in allPerms)
            {
                string label;
                if (HighRiskPermissions.TryGetValue(p, out label)) { high = true; if (!reasons.Contains(label)) reasons.Add(label); }
                else if (p.StartsWith("http") && p.EndsWith("/*")) { high = true; var siteAccess = I18n.T("ext_perm_site_access"); if (!reasons.Contains(siteAccess)) reasons.Add(siteAccess); }
                else if (MediumRiskPermissions.TryGetValue(p, out label)) { medium = true; if (!reasons.Contains(label)) reasons.Add(label); }
            }
            ext.RiskLevel = high ? "high" : (medium ? "medium" : "low");
            ext.RiskReasons = reasons;
        }

        private static void AddPermList(Dictionary<string, object> manifest, string key, List<string> target)
        {
            if (!manifest.ContainsKey(key)) return;
            var arr = manifest[key] as ArrayList;
            if (arr == null) return;
            foreach (var o in arr)
            {
                var s = o as string;
                if (!string.IsNullOrEmpty(s)) target.Add(s);
            }
        }

        private static string ResolveExtensionName(Dictionary<string, object> manifest, string extDir)
        {
            var name = manifest.ContainsKey("name") ? manifest["name"] as string : null;
            if (name == null) return I18n.T("ext_no_name");
            var m = Regex.Match(name, "^__MSG_(.+)__$");
            if (!m.Success) return name;
            var key = m.Groups[1].Value;
            var locale = manifest.ContainsKey("default_locale") ? manifest["default_locale"] as string : "en";
            var msgPath = Path.Combine(extDir, "_locales", locale ?? "en", "messages.json");
            if (!File.Exists(msgPath))
            {
                var localesDir = Path.Combine(extDir, "_locales");
                if (Directory.Exists(localesDir))
                {
                    var any = Directory.GetDirectories(localesDir).FirstOrDefault();
                    if (any != null) msgPath = Path.Combine(any, "messages.json");
                }
            }
            if (File.Exists(msgPath))
            {
                try
                {
                    var serializer = new JavaScriptSerializer();
                    var msgs = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(msgPath));
                    var propKey = msgs.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
                    if (propKey != null)
                    {
                        var entry = msgs[propKey] as Dictionary<string, object>;
                        if (entry != null && entry.ContainsKey("message")) return entry["message"] as string;
                    }
                }
                catch { }
            }
            return key;
        }

        public static List<BrowserExtension> GetChromiumExtensions(string basePath, string browserName)
        {
            var result = new List<BrowserExtension>();
            if (!Directory.Exists(basePath)) return result;
            var serializer = new JavaScriptSerializer();
            var profileDirs = Directory.GetDirectories(basePath)
                .Where(p => { var n = Path.GetFileName(p); return n == "Default" || Regex.IsMatch(n, @"^Profile \d+$"); });
            foreach (var prof in profileDirs)
            {
                var extRoot = Path.Combine(prof, "Extensions");
                if (!Directory.Exists(extRoot)) continue;
                foreach (var idDir in Directory.GetDirectories(extRoot))
                {
                    var id = Path.GetFileName(idDir);
                    if (ChromeComponentIds.Contains(id)) continue;
                    var verDir = Directory.GetDirectories(idDir).OrderByDescending(d => d).FirstOrDefault();
                    if (verDir == null) continue;
                    var manifestPath = Path.Combine(verDir, "manifest.json");
                    if (!File.Exists(manifestPath)) continue;
                    try
                    {
                        var manifest = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(manifestPath));
                        var name = ResolveExtensionName(manifest, verDir);
                        var version = manifest.ContainsKey("version") ? manifest["version"] as string : null;
                        var ext = new BrowserExtension { Browser = browserName, Profile = Path.GetFileName(prof), Id = id, Name = name, Version = version, Path = idDir };
                        ComputeRisk(ext, manifest);
                        result.Add(ext);
                    }
                    catch { }
                }
            }
            return result;
        }

        public static List<BrowserExtension> GetFirefoxExtensions()
        {
            var result = new List<BrowserExtension>();
            var ffRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Mozilla\Firefox");
            var iniPath = Path.Combine(ffRoot, "profiles.ini");
            if (!File.Exists(iniPath)) return result;
            var paths = File.ReadAllLines(iniPath).Where(l => l.StartsWith("Path=")).Select(l => l.Substring(5));
            var serializer = new JavaScriptSerializer();
            foreach (var rel in paths)
            {
                var profDir = Path.IsPathRooted(rel) ? rel : Path.Combine(ffRoot, rel);
                var extJson = Path.Combine(profDir, "extensions.json");
                if (!File.Exists(extJson)) continue;
                try
                {
                    var data = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(extJson));
                    if (!data.ContainsKey("addons")) continue;
                    var addons = data["addons"] as ArrayList;
                    if (addons == null) continue;
                    foreach (var addonObj in addons)
                    {
                        var addon = addonObj as Dictionary<string, object>;
                        if (addon == null) continue;
                        var type = addon.ContainsKey("type") ? addon["type"] as string : null;
                        if (type != "extension") continue;
                        var location = addon.ContainsKey("location") ? addon["location"] as string : null;
                        if (location == "app-builtin" || location == "app-system-defaults") continue;
                        string name = addon.ContainsKey("id") ? addon["id"] as string : null;
                        if (addon.ContainsKey("defaultLocale"))
                        {
                            var dl = addon["defaultLocale"] as Dictionary<string, object>;
                            if (dl != null && dl.ContainsKey("name") && dl["name"] != null) name = dl["name"] as string;
                        }
                        result.Add(new BrowserExtension
                        {
                            Browser = "Firefox",
                            Profile = Path.GetFileName(profDir.TrimEnd('\\')),
                            Id = addon.ContainsKey("id") ? addon["id"] as string : null,
                            Name = name,
                            Version = addon.ContainsKey("version") ? addon["version"] as string : null,
                            Path = addon.ContainsKey("path") ? addon["path"] as string : null,
                            ExtensionsJsonPath = extJson,
                            RiskLevel = "unknown"
                        });
                    }
                }
                catch { }
            }
            return result;
        }

        public static List<BrowserExtension> GetAllExtensions()
        {
            var all = new List<BrowserExtension>();
            all.AddRange(GetChromiumExtensions(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data"), "Chrome"));
            all.AddRange(GetChromiumExtensions(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data"), "Edge"));
            all.AddRange(GetFirefoxExtensions());

            // איחוד שורות כפולות: אותו תוסף (לפי דפדפן+מזהה+גרסה) המותקן במספר
            // פרופילים מוצג כשורה אחת עם רשימת הפרופילים, ומוסר מכולם יחד.
            var grouped = all
                .GroupBy(e => e.Browser + "|" + e.Id + "|" + (e.Version ?? ""))
                .Select(g =>
                {
                    var first = g.First();
                    return new BrowserExtension
                    {
                        Browser = first.Browser,
                        Profile = first.Profile,
                        Id = first.Id,
                        Name = first.Name,
                        Version = first.Version,
                        Path = first.Path,
                        ExtensionsJsonPath = first.ExtensionsJsonPath,
                        RiskLevel = first.RiskLevel,
                        RiskReasons = first.RiskReasons,
                        ProfileEntries = g.ToList()
                    };
                })
                .OrderBy(e => e.Browser).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            return grouped;
        }

        private static bool RemoveSingle(BrowserExtension ext)
        {
            try
            {
                if (ext.Browser == "Firefox")
                {
                    if (!string.IsNullOrEmpty(ext.Path) && (File.Exists(ext.Path) || Directory.Exists(ext.Path)))
                    {
                        if (Directory.Exists(ext.Path)) Directory.Delete(ext.Path, true); else File.Delete(ext.Path);
                    }
                    if (!string.IsNullOrEmpty(ext.ExtensionsJsonPath) && File.Exists(ext.ExtensionsJsonPath))
                    {
                        File.Copy(ext.ExtensionsJsonPath, ext.ExtensionsJsonPath + ".bak", true);
                        var serializer = new JavaScriptSerializer();
                        var data = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(ext.ExtensionsJsonPath));
                        var addons = data["addons"] as ArrayList;
                        if (addons != null)
                        {
                            var filtered = new ArrayList();
                            foreach (var a in addons)
                            {
                                var d = a as Dictionary<string, object>;
                                if (d != null && d.ContainsKey("id") && (d["id"] as string) == ext.Id) continue;
                                filtered.Add(a);
                            }
                            data["addons"] = filtered;
                        }
                        File.WriteAllText(ext.ExtensionsJsonPath, serializer.Serialize(data));
                    }
                }
                else
                {
                    if (Directory.Exists(ext.Path)) Directory.Delete(ext.Path, true);
                }
                Logger.Log("הוסר תוסף: " + ext.Browser + " / " + ext.Name + " (" + ext.Id + ") פרופיל " + ext.Profile);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log("שגיאה בהסרת תוסף " + ext.Id + " (פרופיל " + ext.Profile + "): " + ex.Message);
                return false;
            }
        }

        public static bool RemoveExtension(BrowserExtension ext)
        {
            var entries = (ext.ProfileEntries != null && ext.ProfileEntries.Count > 0) ? ext.ProfileEntries : new List<BrowserExtension> { ext };
            bool allOk = true;
            foreach (var e in entries) allOk = RemoveSingle(e) && allOk;
            return allOk;
        }
    }
}
