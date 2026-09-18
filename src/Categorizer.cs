using System;
using System.Collections.Generic;
using System.Linq;

namespace UninstallerPro
{
    // סיווג תוכנות לקטגוריות לפי מילות מפתח בשם/יצרן - היוריסטיקה פשוטה,
    // לא מושלמת, אך מספיקה כדי לאפשר סינון יעיל ברשימה הגדולה.
    // המפתחות הפנימיים קבועים (לא תלויי שפה) - התרגום מתבצע רק בתצוגה דרך I18n.
    public static class Categorizer
    {
        public const string Games = "cat_games";
        public const string Graphics = "cat_graphics";
        public const string Security = "cat_security";
        public const string Browsers = "cat_browsers";
        public const string Communication = "cat_communication";
        public const string Office = "cat_office";
        public const string Development = "cat_development";
        public const string Media = "cat_media";
        public const string SystemTools = "cat_systemtools";
        public const string Drivers = "cat_drivers";
        public const string CloudStorage = "cat_cloudstorage";
        public const string Other = "cat_other";

        private static readonly Tuple<string, string[]>[] Rules = new[]
        {
            Tuple.Create(Games, new[] { "steam", "epic games", "ubisoft", "rockstar", "electronic arts", " ea ", "battle.net", "blizzard", "riot games", "bethesda", "cd projekt", "square enix", "gog galaxy", "xbox", "playstation", "grand theft auto", "the outer worlds", "assassin", "forza", "borderlands", "mafia", "sniper elite", "callisto protocol", "ronin", "robocop", "fallujah", "avatar frontiers", "gridplayer", "grandrp", "rage multiplayer" }),
            Tuple.Create(Graphics, new[] { "adobe photoshop", "adobe premiere", "adobe illustrator", "adobe lightroom", "adobe express", "gimp", "capcut", "davinci resolve", "topaz", "corel", "affinity", "canva", "clipchamp", "paint.net", "krita" }),
            Tuple.Create(Security, new[] { "avast", "avg", "norton", "mcafee", "kaspersky", "bitdefender", "eset", "malwarebytes", "windows defender", "forticlient", "hotspot shield", "adguard", "vpn", "tailscale" }),
            Tuple.Create(Browsers, new[] { "google chrome", "mozilla firefox", "microsoft edge", "opera", "brave browser", "comet" }),
            Tuple.Create(Communication, new[] { "zoom", "skype", "microsoft teams", "whatsapp", "telegram", "discord", "messenger", "slack" }),
            Tuple.Create(Office, new[] { "microsoft 365", "microsoft office", "word", "excel", "powerpoint", "outlook", "onenote", "access", "publisher", "notion", "adobe acrobat", "pdf-xchange", "winrar", "7-zip", "power bi" }),
            Tuple.Create(Development, new[] { "visual studio", "git", "node.js", "python", "jetbrains", "docker", "github", "windows powershell", "console", "idle (python" }),
            Tuple.Create(Media, new[] { "vlc", "winamp", "spotify", "windows media player", "netflix", "gridplayer" }),
            Tuple.Create(CloudStorage, new[] { "onedrive", "google drive", "dropbox" }),
            Tuple.Create(Drivers, new[] { "realtek", "nvidia", "intel", "gigabyte", "asus", "logitech", "canon", "sandisk", "chipset", "audio driver", "graphics driver" }),
            Tuple.Create(SystemTools, new[] { "ccleaner", "treesize", "revo uninstaller", "my files cleaner", "powertoys", "cpu-z", "crystaldiskmark", "rufus", "driver booster", "iobit", "uninstaller", "cleaner", "defrag", "pc manager", "diagnostic" }),
        };

        // מחזיר מפתח פנימי יציב (לא מתורגם) - משמש לאחסון ולסינון.
        public static string Categorize(string displayName, string publisher)
        {
            var text = ((displayName ?? "") + " " + (publisher ?? "")).ToLowerInvariant();
            foreach (var rule in Rules)
            {
                foreach (var kw in rule.Item2)
                {
                    if (text.Contains(kw)) return rule.Item1;
                }
            }
            return Other;
        }

        // רשימת מפתחות פנימיים (לא מתורגמים) - להצגה יש לעבור דרך I18n.T().
        public static List<string> AllCategories()
        {
            var list = Rules.Select(r => r.Item1).Distinct().ToList();
            list.Add(Other);
            return list;
        }
    }

    // מזהה קבוצות תוכנות "מתחרות" (עושות אותו דבר) שמותקנות יותר מפעם אחת -
    // לדוגמה: כמה כלי דחיסה, כמה נגני מדיה, כמה כלי ניקוי. לא קובע מי "עדיף" -
    // מציג מידע (תאריך התקנה, גודל) שיעזור למשתמש להחליט בעצמו.
    public static class DuplicatePurposeFinder
    {
        private class Group
        {
            public string LabelKey;
            public string[] Keywords;
        }

        private static readonly Group[] Groups = new[]
        {
            new Group { LabelKey = "dup_group_archivers", Keywords = new[] { "winrar", "7-zip", "peazip", "winzip" } },
            new Group { LabelKey = "dup_group_pdf", Keywords = new[] { "adobe acrobat", "pdf-xchange", "foxit", "sumatra" } },
            new Group { LabelKey = "dup_group_antivirus", Keywords = new[] { "avast", "avg", "norton", "mcafee", "kaspersky", "bitdefender", "eset", "windows defender" } },
            new Group { LabelKey = "dup_group_cleaners", Keywords = new[] { "ccleaner", "my files cleaner", "iobit", "advanced systemcare" } },
            new Group { LabelKey = "dup_group_disk_analyzers", Keywords = new[] { "treesize" } },
            new Group { LabelKey = "dup_group_uninstallers", Keywords = new[] { "revo uninstaller", "puresys", "uninstaller pro", "iobit uninstaller" } },
            new Group { LabelKey = "dup_group_media_players", Keywords = new[] { "vlc", "winamp", "windows media player" } },
            new Group { LabelKey = "dup_group_browsers", Keywords = new[] { "google chrome", "mozilla firefox", "microsoft edge", "opera", "brave" } },
            new Group { LabelKey = "dup_group_vpn", Keywords = new[] { "forticlient", "hotspot shield", "tailscale" } },
            new Group { LabelKey = "dup_group_game_launchers", Keywords = new[] { "steam", "epic games launcher", "ubisoft connect", "battle.net", "gog galaxy", "rockstar games launcher" } },
        };

        public class OverlapResult
        {
            public string Label;
            public List<InstalledProgram> Matches;
        }

        public static List<OverlapResult> Find(List<InstalledProgram> programs)
        {
            var results = new List<OverlapResult>();
            foreach (var g in Groups)
            {
                var matches = programs.Where(p =>
                {
                    var text = (p.DisplayName ?? "").ToLowerInvariant();
                    return g.Keywords.Any(k => text.Contains(k));
                }).ToList();
                if (matches.Count >= 2)
                {
                    results.Add(new OverlapResult { Label = I18n.T(g.LabelKey), Matches = matches });
                }
            }
            return results;
        }
    }
}
