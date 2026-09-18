using System;
using System.Collections.Generic;
using System.IO;

namespace UninstallerPro
{
    public static class I18n
    {
        public const string English = "en";
        public const string Hebrew = "he";
        public static string CurrentLang = English;

        public static bool IsRtl { get { return CurrentLang == Hebrew; } }

        public static string T(string key)
        {
            Dictionary<string, string> dict;
            if (Strings.TryGetValue(CurrentLang, out dict) && dict.ContainsKey(key)) return dict[key];
            if (Strings.TryGetValue(English, out dict) && dict.ContainsKey(key)) return dict[key];
            return key;
        }

        // All UI text lives in locales/en.json and locales/he.json (shipped
        // next to the exe - see the csproj's Content items and Setup.cs,
        // which embeds+writes them alongside OptiGuard.exe on install). This
        // loader reads them once at first use. A missing/corrupt file falls
        // back to an empty dictionary rather than crashing the app - T()
        // already falls back to the raw key (and then to English) when a
        // key isn't found, so the app stays usable even in that situation.
        private static Dictionary<string, string> LoadLocale(string lang)
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "locales", lang + ".json");
                var json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                return serializer.Deserialize<Dictionary<string, string>>(json);
            }
            catch (Exception ex)
            {
                try { Logger.Log("Failed to load locale '" + lang + "': " + ex.Message); } catch { }
                return new Dictionary<string, string>();
            }
        }

        private static readonly Dictionary<string, Dictionary<string, string>> Strings = new Dictionary<string, Dictionary<string, string>>
        {
            { English, LoadLocale(English) },
            { Hebrew, LoadLocale(Hebrew) },
        };
    }
}
