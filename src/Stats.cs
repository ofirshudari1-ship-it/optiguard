using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace UninstallerPro
{
    // מונים מצטברים לכל חיי ההתקנה - מוצגים בדשבורד כדי להמחיש את הערך
    // שהכלי סיפק לאורך זמן. נשמר כ-JSON פשוט תחת AppData.
    public static class Stats
    {
        private static readonly string StatsFile = Path.Combine(AppPaths.DataDir, "stats.json");
        private static readonly object Lock = new object();

        public class Data
        {
            public int ProgramsUninstalled;
            public int ResidualItemsRemoved;
            public long JunkBytesFreed;
            public int JunkCleanupRuns;
            public int StartupItemsCleaned;
            public int UwpAppsRemoved;
            public int ExtensionsRemoved;
            public int GamesRemoved;
            public string FirstUseDate;
        }

        private static Data _cache;

        public static Data Load()
        {
            lock (Lock)
            {
                if (_cache != null) return _cache;
                try
                {
                    if (File.Exists(StatsFile))
                    {
                        var serializer = new JavaScriptSerializer();
                        var dict = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(StatsFile));
                        var d = new Data();
                        if (dict.ContainsKey("ProgramsUninstalled")) d.ProgramsUninstalled = Convert.ToInt32(dict["ProgramsUninstalled"]);
                        if (dict.ContainsKey("ResidualItemsRemoved")) d.ResidualItemsRemoved = Convert.ToInt32(dict["ResidualItemsRemoved"]);
                        if (dict.ContainsKey("JunkBytesFreed")) d.JunkBytesFreed = Convert.ToInt64(dict["JunkBytesFreed"]);
                        if (dict.ContainsKey("JunkCleanupRuns")) d.JunkCleanupRuns = Convert.ToInt32(dict["JunkCleanupRuns"]);
                        if (dict.ContainsKey("StartupItemsCleaned")) d.StartupItemsCleaned = Convert.ToInt32(dict["StartupItemsCleaned"]);
                        if (dict.ContainsKey("UwpAppsRemoved")) d.UwpAppsRemoved = Convert.ToInt32(dict["UwpAppsRemoved"]);
                        if (dict.ContainsKey("ExtensionsRemoved")) d.ExtensionsRemoved = Convert.ToInt32(dict["ExtensionsRemoved"]);
                        if (dict.ContainsKey("GamesRemoved")) d.GamesRemoved = Convert.ToInt32(dict["GamesRemoved"]);
                        if (dict.ContainsKey("FirstUseDate")) d.FirstUseDate = dict["FirstUseDate"] as string;
                        _cache = d;
                        return d;
                    }
                }
                catch { }
                _cache = new Data { FirstUseDate = DateTime.Now.ToString("yyyy-MM-dd") };
                Save(_cache);
                return _cache;
            }
        }

        public static void Save(Data d)
        {
            lock (Lock)
            {
                try
                {
                    AppPaths.EnsureDataDir();
                    var serializer = new JavaScriptSerializer();
                    var dict = new Dictionary<string, object>
                    {
                        { "ProgramsUninstalled", d.ProgramsUninstalled },
                        { "ResidualItemsRemoved", d.ResidualItemsRemoved },
                        { "JunkBytesFreed", d.JunkBytesFreed },
                        { "JunkCleanupRuns", d.JunkCleanupRuns },
                        { "StartupItemsCleaned", d.StartupItemsCleaned },
                        { "UwpAppsRemoved", d.UwpAppsRemoved },
                        { "ExtensionsRemoved", d.ExtensionsRemoved },
                        { "GamesRemoved", d.GamesRemoved },
                        { "FirstUseDate", d.FirstUseDate }
                    };
                    File.WriteAllText(StatsFile, serializer.Serialize(dict));
                    _cache = d;
                }
                catch { }
            }
        }

        public static void Add(Action<Data> mutate)
        {
            var d = Load();
            mutate(d);
            Save(d);
        }
    }
}
