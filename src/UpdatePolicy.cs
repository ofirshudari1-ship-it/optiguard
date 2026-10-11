using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace UninstallerPro
{
    // Pure, UI-free logic of the self-update flow (4.22.0): version compare,
    // GitHub "digest" parsing, sha256 verification, the re-check schedule and
    // the "safe moment" gate. Kept free of WPF/Logger so build\tests can
    // compile and exercise it headlessly (no window, no installer, no network).
    public static class UpdatePolicy
    {
        // First background check after launch (unchanged from 4.21.x).
        public static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);
        // Re-check cadence for an app left running (same as the sibling tools).
        public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
        // After waking from sleep: re-check only if the last check is at least
        // this old, and wait a little for the network to come back first.
        public static readonly TimeSpan ResumeDelay = TimeSpan.FromSeconds(45);
        // An update found by a RE-check (not the startup one) installs only
        // once the user has been away from keyboard/mouse this long.
        public const double MinIdleSeconds = 180;
        // After a failed/declined (UAC) automatic install, do not retry by
        // itself for this long (the banner button stays available).
        public static readonly TimeSpan RetryBlock = TimeSpan.FromHours(24);

        // How long to wait before the next scheduled check.
        public static TimeSpan NextCheckDelay(DateTime? lastCheckUtc, DateTime nowUtc)
        {
            if (lastCheckUtc == null) return StartupDelay;
            var due = lastCheckUtc.Value + CheckInterval;
            var wait = due - nowUtc;
            if (wait < TimeSpan.Zero) return TimeSpan.Zero;
            if (wait > CheckInterval) return CheckInterval; // clock moved backwards
            return wait;
        }

        public static bool ShouldCheckOnResume(DateTime? lastCheckUtc, DateTime nowUtc)
        {
            if (lastCheckUtc == null) return true;
            var age = nowUtc - lastCheckUtc.Value;
            return age >= CheckInterval || age < TimeSpan.Zero;
        }

        public static bool IsSafeToInstall(double idleSeconds, bool sessionEnding, bool installInProgress,
            DateTime utcNow, DateTime blockedUntilUtc)
        {
            if (sessionEnding || installInProgress) return false;
            if (utcNow < blockedUntilUtc) return false;
            return idleSeconds >= MinIdleSeconds;
        }

        // "sha256:ABCD..." -> "abcd..."; anything else (empty, other algo,
        // wrong length, non-hex) -> null, meaning "no digest to verify".
        public static string ParseDigest(string digest)
        {
            if (string.IsNullOrEmpty(digest)) return null;
            const string prefix = "sha256:";
            if (!digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            string hex = digest.Substring(prefix.Length).Trim().ToLowerInvariant();
            if (hex.Length != 64) return null;
            foreach (char c in hex)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return null;
            return hex;
        }

        public static string ComputeSha256Hex(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(fs);
                var sb = new System.Text.StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // True when no digest is published (never blocks an update for a
        // missing field) or when the file's sha256 equals it.
        public static bool Sha256Matches(string path, string expectedHexOrNull, out string actualHex)
        {
            actualHex = null;
            if (string.IsNullOrEmpty(expectedHexOrNull)) return true;
            actualHex = ComputeSha256Hex(path);
            return string.Equals(actualHex, expectedHexOrNull, StringComparison.OrdinalIgnoreCase);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        // Seconds since the user's last keyboard/mouse input. On any failure
        // returns 0 ("not idle") - the safe default: never install by mistake.
        public static double GetIdleSeconds()
        {
            try
            {
                var info = new LASTINPUTINFO();
                info.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
                if (!GetLastInputInfo(ref info)) return 0;
                uint now = unchecked((uint)Environment.TickCount);
                return unchecked(now - info.dwTime) / 1000.0;
            }
            catch { return 0; }
        }

        public static bool IsNewer(string latest, string current)
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
    }
}
