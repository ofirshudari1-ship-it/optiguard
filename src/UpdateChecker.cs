using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

namespace UninstallerPro
{
    // The installer .exe attached to a GitHub Release, as needed for a
    // one-click/self-update download - separate from ReleaseUrl (the human-
    // readable release page) on UpdateInfo below.
    public class UpdateAsset
    {
        public string Name;
        public string DownloadUrl;
        public long Size;
    }

    public class UpdateInfo
    {
        public bool Available;
        public string LatestVersion;
        // Where to send the user to get the new version - the GitHub Release
        // page itself (html_url). Always the fallback path: if a one-click
        // silent update fails for any reason, this is what the caller shows
        // instead so the user is never left with no path forward.
        public string ReleaseUrl;
        public string Notes;
        public string Error;
        // The "OptiGuard-Setup-X.Y.Z.exe" release asset, when one is
        // published on the release. Null if the release has no matching
        // asset (e.g. a release published without a build attached) - in
        // that case a one-click update simply isn't offered and callers must
        // fall back to ReleaseUrl.
        public UpdateAsset Asset;
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
        // Returned in `error` when the user declines the Windows UAC prompt
        // for the downloaded installer - a deliberate choice, which callers
        // treat silently rather than as a failure.
        public const string ErrorCancelledByUser = "cancelled_by_user";

        // A download that makes no progress for this long is treated as dead
        // (Wi-Fi dropped, captive portal, half-open TCP connection). Without
        // this WebClient.DownloadFileAsync can wait forever and the progress
        // dialog never goes away.
        private static readonly TimeSpan DownloadStallTimeout = TimeSpan.FromSeconds(45);

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
                    result.Asset = FindInstallerAsset(dict);
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                Logger.Log("Update check failed: " + ex.Message);
            }
            return result;
        }

        // Picks the installer out of the release's "assets" array: the one
        // .exe attached to the release (there is only ever exactly one -
        // see RELEASE-CHECKLIST.md "exactly one OptiGuard-Setup-X.Y.Z.exe at
        // the root"). Returns null (not throws) for any shape mismatch, since
        // a missing/malformed asset list should degrade to "no one-click
        // update available", not break the whole version check.
        private static UpdateAsset FindInstallerAsset(Dictionary<string, object> releaseDict)
        {
            try
            {
                object assetsObj;
                if (!releaseDict.TryGetValue("assets", out assetsObj) || assetsObj == null) return null;
                var assets = assetsObj as System.Collections.IEnumerable;
                if (assets == null) return null;

                // Prefer the real installer ("OptiGuard-Setup-X.Y.Z.exe") so a
                // stray extra .exe attached to a release can never be picked
                // up and run as the "update"; any other .exe is only a fallback.
                UpdateAsset fallback = null;
                foreach (var item in assets)
                {
                    var assetDict = item as Dictionary<string, object>;
                    if (assetDict == null) continue;
                    var name = assetDict.ContainsKey("name") ? assetDict["name"] as string : null;
                    if (string.IsNullOrEmpty(name) || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

                    var url = assetDict.ContainsKey("browser_download_url") ? assetDict["browser_download_url"] as string : null;
                    if (string.IsNullOrEmpty(url)) continue;

                    long size = 0;
                    if (assetDict.ContainsKey("size") && assetDict["size"] != null)
                    {
                        try { size = Convert.ToInt64(assetDict["size"]); } catch { }
                    }

                    var asset = new UpdateAsset { Name = name, DownloadUrl = url, Size = size };
                    if (name.StartsWith("OptiGuard-Setup", StringComparison.OrdinalIgnoreCase)) return asset;
                    if (fallback == null) fallback = asset;
                }
                return fallback;
            }
            catch { }
            return null;
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
        // can read the release notes and download the installer themselves.
        // This is the universal fallback: used directly when a user hasn't
        // opted into one-click updates, and used by callers of
        // DownloadAndLaunchSilentInstall whenever that fails at any step, so
        // a failed automatic update never leaves the user stuck with no path
        // forward.
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

        // Reports 0-100 download progress. Invoked on a background thread -
        // callers marshal to the UI thread themselves if they touch WPF
        // elements from it.
        public delegate void DownloadProgressHandler(int percent);

        // The one-click / auto self-update path: downloads the installer
        // asset from the GitHub release to a temp location, verifies the
        // download completed by comparing its size against what the GitHub
        // API reported for that asset, and launches it with /SILENT so it
        // installs with no further UI. Does NOT exit the running app itself -
        // the caller must do that immediately after this returns true, since
        // Setup.exe needs to overwrite OptiGuard.exe and can't while it's
        // still running (see TryCloseRunningAppSilently in Setup.cs, which is
        // a second, best-effort line of defense but not a substitute for the
        // running instance getting out of the way on its own).
        //
        // On any failure (network, disk, size mismatch, failed launch) this
        // returns false with a human-readable `error` and does NOT touch the
        // running app - callers are expected to fall back to
        // OpenReleasePage(info.ReleaseUrl, ...) so the user still has a way
        // to get the update by hand.
        public static bool DownloadAndLaunchSilentInstall(UpdateInfo info, DownloadProgressHandler onProgress, out string error)
        {
            error = null;
            if (info == null || info.Asset == null || string.IsNullOrEmpty(info.Asset.DownloadUrl))
            {
                error = "no_installer_asset";
                return false;
            }

            string destPath;
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "OptiGuard", "updates");
                Directory.CreateDirectory(tempDir);
                string fileName = string.IsNullOrEmpty(info.Asset.Name) ? "OptiGuard-Setup-" + (info.LatestVersion ?? "update") + ".exe" : info.Asset.Name;
                destPath = Path.Combine(tempDir, fileName);
                if (File.Exists(destPath)) TryDeleteFile(destPath);
            }
            catch (Exception ex)
            {
                error = "disk: " + ex.Message;
                Logger.Log("Update download setup failed: " + ex.Message);
                return false;
            }

            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "OptiGuard-UpdateChecker");
                    var doneEvent = new System.Threading.ManualResetEventSlim(false);
                    Exception downloadError = null;
                    bool cancelled = false;
                    long lastProgressTicks = DateTime.UtcNow.Ticks;

                    client.DownloadProgressChanged += (s, e) =>
                    {
                        System.Threading.Interlocked.Exchange(ref lastProgressTicks, DateTime.UtcNow.Ticks);
                        if (onProgress != null) { try { onProgress(e.ProgressPercentage); } catch { } }
                    };
                    client.DownloadFileCompleted += (s, e) =>
                    {
                        if (e.Cancelled) cancelled = true;
                        else if (e.Error != null) downloadError = e.Error;
                        doneEvent.Set();
                    };

                    client.DownloadFileAsync(new Uri(info.Asset.DownloadUrl), destPath);

                    // Stall watchdog: wake up every second and give up if no
                    // bytes have arrived for DownloadStallTimeout.
                    bool stalled = false;
                    while (!doneEvent.Wait(1000))
                    {
                        var idle = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - System.Threading.Interlocked.Read(ref lastProgressTicks));
                        if (idle > DownloadStallTimeout)
                        {
                            stalled = true;
                            client.CancelAsync();
                            doneEvent.Wait(10000);
                            break;
                        }
                    }
                    // A cancelled/stalled download leaves a partial file behind
                    // and reports no Error - it must never fall through to the
                    // "launch it" step below (previously a cancelled download
                    // with no size reported by GitHub would have been launched).
                    if (stalled) throw new TimeoutException("no data received for " + (int)DownloadStallTimeout.TotalSeconds + "s");
                    if (cancelled) throw new OperationCanceledException("download cancelled");
                    if (downloadError != null) throw downloadError;
                }
            }
            catch (Exception ex)
            {
                error = "download: " + ex.Message;
                Logger.Log("Update download failed: " + ex.Message);
                TryDeleteFile(destPath);
                return false;
            }

            // Basic integrity check: the file we actually got must match the
            // size GitHub told us to expect for this asset. This does not
            // validate the content (the release has no published checksum/
            // signature to check against), but it catches a truncated,
            // interrupted, or otherwise short download instead of silently
            // launching a broken .exe.
            try
            {
                var actualSize = new FileInfo(destPath).Length;
                if (info.Asset.Size > 0 && actualSize != info.Asset.Size)
                {
                    error = string.Format("size_mismatch: expected {0} bytes, got {1}", info.Asset.Size, actualSize);
                    Logger.Log("Update download size mismatch: expected " + info.Asset.Size + ", got " + actualSize);
                    TryDeleteFile(destPath);
                    return false;
                }
                // Second, size-independent check: the file must actually be a
                // Windows executable (PE files start with "MZ"). Catches an
                // HTML error/login page served by a proxy or captive portal
                // in place of the installer, including when GitHub didn't
                // report a size to compare against.
                if (!LooksLikeWindowsExecutable(destPath))
                {
                    error = "not_an_installer: downloaded file is not a Windows executable";
                    Logger.Log("Update download rejected: file is not a PE executable (" + actualSize + " bytes)");
                    TryDeleteFile(destPath);
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = "verify: " + ex.Message;
                Logger.Log("Update download verification failed: " + ex.Message);
                TryDeleteFile(destPath);
                return false;
            }

            try
            {
                // /SILENT still triggers the OS-level UAC elevation prompt
                // (OptiGuard's installer requires admin rights) - that one
                // prompt is unavoidable and is not part of Setup's own UI.
                // Everything else about the install proceeds with no dialogs.
                // /RELAUNCH: tells the new Setup to start OptiGuard again once
                // the silent install succeeds, so a one-click update ends with
                // the app back on screen instead of simply vanishing.
                Process.Start(new ProcessStartInfo(destPath, "/SILENT /RELAUNCH") { UseShellExecute = true });
                return true;
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // ERROR_CANCELLED - the user chose "No" on the UAC prompt.
                Logger.Log("Silent installer launch cancelled at the UAC prompt.");
                TryDeleteFile(destPath);
                error = ErrorCancelledByUser;
                return false;
            }
            catch (Exception ex)
            {
                error = "launch: " + ex.Message;
                Logger.Log("Failed to launch silent installer: " + ex.Message);
                return false;
            }
        }

        private static bool LooksLikeWindowsExecutable(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    return fs.Length > 1024 && fs.ReadByte() == 'M' && fs.ReadByte() == 'Z';
                }
            }
            catch { return false; }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
