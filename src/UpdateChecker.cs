using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Web.Script.Serialization;

namespace UninstallerPro
{
    public class UpdateInfo
    {
        public bool Available;
        public string LatestVersion;
        // Where to send the user to get the new version - the GitHub Release
        // page itself (html_url), never a direct .exe download we run
        // unattended. The user always sees the release notes before they
        // install anything.
        public string ReleaseUrl;
        public string Notes;
        public string Error;
    }

    // Checks GitHub Releases for a newer OptiGuard version. Public repo, so
    // this is a plain unauthenticated GET - no token, ever, shipped in the
    // app (see repo README / CLAUDE instructions: never embed a PAT in
    // client code). If the request fails for any reason (offline, GitHub
    // down, rate-limited, DNS), this fails silently: Check() returns
    // Available=false with an Error string that callers only ever log, never
    // surface to the user - a background update check must never interrupt
    // or alarm someone just because their network hiccuped.
    public static class UpdateChecker
    {
        private const string LatestReleaseApiUrl =
            "https://api.github.com/repos/ofirshudari1-ship-it/optiguard/releases/latest";

        public static UpdateInfo Check(string currentVersion)
        {
            var result = new UpdateInfo();
            try
            {
                using (var client = new WebClient())
                {
                    // GitHub's API rejects requests with no User-Agent (403).
                    client.Headers.Add("User-Agent", "OptiGuard-UpdateChecker");
                    client.Headers.Add("Accept", "application/vnd.github+json");
                    var json = client.DownloadString(LatestReleaseApiUrl);
                    var serializer = new JavaScriptSerializer();
                    var dict = serializer.Deserialize<Dictionary<string, object>>(json);

                    var tag = dict.ContainsKey("tag_name") ? dict["tag_name"] as string : null;
                    var htmlUrl = dict.ContainsKey("html_url") ? dict["html_url"] as string : null;
                    var notes = dict.ContainsKey("body") ? dict["body"] as string : null;

                    var latest = StripVPrefix(tag);
                    result.LatestVersion = latest;
                    result.ReleaseUrl = htmlUrl;
                    result.Notes = notes;
                    result.Available = !string.IsNullOrEmpty(latest) && IsNewer(latest, currentVersion);
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                Logger.Log("Update check failed: " + ex.Message);
            }
            return result;
        }

        private static string StripVPrefix(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return tag;
            return tag.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? tag.Substring(1) : tag;
        }

        private static bool IsNewer(string latest, string current)
        {
            Version vLatest, vCurrent;
            if (Version.TryParse(NormalizeVersion(latest), out vLatest) && Version.TryParse(NormalizeVersion(current), out vCurrent))
                return vLatest > vCurrent;
            return string.CompareOrdinal(latest, current) > 0;
        }

        private static string NormalizeVersion(string v)
        {
            if (string.IsNullOrEmpty(v)) return "0.0";
            var parts = v.Split('.');
            return parts.Length == 1 ? v + ".0" : v;
        }

        // Opens the GitHub release page in the user's default browser so they
        // can read the release notes and download the installer themselves -
        // deliberately not an unattended download-and-run, so a routine
        // update never launches an installer without the user looking at it.
        public static bool OpenReleasePage(string url, out string error)
        {
            error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(url)) { error = "no_url"; return false; }
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Logger.Log("Failed to open update release page: " + ex.Message);
                return false;
            }
        }
    }
}
