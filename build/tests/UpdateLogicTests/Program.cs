using System;
using System.IO;
using UninstallerPro;

// Minimal assert-based runner (no external packages, works offline).
static class Program
{
    static int _pass, _fail;

    static void Check(bool cond, string name)
    {
        if (cond) _pass++; else { _fail++; Console.WriteLine("FAIL: " + name); }
    }

    static int Main()
    {
        // --- IsNewer ---
        Check(UpdatePolicy.IsNewer("4.22.0", "4.21.1"), "minor newer");
        Check(UpdatePolicy.IsNewer("4.21.2", "4.21.1"), "patch newer");
        Check(!UpdatePolicy.IsNewer("4.21.1", "4.21.1"), "equal not newer");
        Check(!UpdatePolicy.IsNewer("4.9.0", "4.21.1"), "4.9 < 4.21 (numeric, not lexical)");
        Check(UpdatePolicy.IsNewer("5", "4.21.1"), "single-part major");
        Check(!UpdatePolicy.IsNewer("", "4.21.1"), "empty latest");
        Check(!UpdatePolicy.IsNewer(null, "4.21.1"), "null latest");

        // --- ParseDigest ---
        string hex = new string('a', 64);
        Check(UpdatePolicy.ParseDigest("sha256:" + hex) == hex, "digest ok");
        Check(UpdatePolicy.ParseDigest("SHA256:" + hex.ToUpperInvariant()) == hex, "digest case-insensitive, lowercased");
        Check(UpdatePolicy.ParseDigest(null) == null, "digest null");
        Check(UpdatePolicy.ParseDigest("") == null, "digest empty");
        Check(UpdatePolicy.ParseDigest("sha1:" + hex) == null, "digest wrong algo");
        Check(UpdatePolicy.ParseDigest("sha256:abc") == null, "digest short");
        Check(UpdatePolicy.ParseDigest("sha256:" + new string('z', 64)) == null, "digest non-hex");

        // --- sha256 verify ---
        string tmp = Path.Combine(Path.GetTempPath(), "optiguard-updatetest-" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            File.WriteAllBytes(tmp, new byte[] { 1, 2, 3, 4, 5 });
            // sha256 of 01 02 03 04 05
            string known = "74f81fe167d99b4cb41d6d0ccda82278caee9f3e2f25d5e5a3936ff3dcec60d0";
            Check(UpdatePolicy.ComputeSha256Hex(tmp) == known, "sha256 known vector");
            string actual;
            Check(UpdatePolicy.Sha256Matches(tmp, known, out actual), "sha matches");
            Check(UpdatePolicy.Sha256Matches(tmp, known.ToUpperInvariant(), out actual), "sha matches case-insens");
            Check(!UpdatePolicy.Sha256Matches(tmp, new string('0', 64), out actual) && actual == known, "sha mismatch reported with actual");
            Check(UpdatePolicy.Sha256Matches(tmp, null, out actual) && actual == null, "missing digest never blocks");
            Check(UpdatePolicy.Sha256Matches(tmp, "", out actual), "empty digest never blocks");
        }
        finally { try { File.Delete(tmp); } catch { } }

        // --- schedule ---
        var now = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
        Check(UpdatePolicy.NextCheckDelay(null, now) == UpdatePolicy.StartupDelay, "first check = startup delay");
        Check(UpdatePolicy.NextCheckDelay(now, now) == TimeSpan.FromHours(6), "just checked -> 6h");
        Check(UpdatePolicy.NextCheckDelay(now.AddHours(-2), now) == TimeSpan.FromHours(4), "2h ago -> 4h left");
        Check(UpdatePolicy.NextCheckDelay(now.AddHours(-7), now) == TimeSpan.Zero, "overdue -> 0");
        Check(UpdatePolicy.NextCheckDelay(now.AddHours(3), now) == TimeSpan.FromHours(6), "clock went back -> capped at interval");
        Check(UpdatePolicy.ShouldCheckOnResume(null, now), "resume: never checked");
        Check(!UpdatePolicy.ShouldCheckOnResume(now.AddHours(-1), now), "resume: recent check, skip");
        Check(UpdatePolicy.ShouldCheckOnResume(now.AddHours(-6), now), "resume: 6h old, check");
        Check(UpdatePolicy.ShouldCheckOnResume(now.AddHours(1), now), "resume: clock back, check");

        // --- safe-moment gate ---
        var past = DateTime.MinValue;
        Check(UpdatePolicy.IsSafeToInstall(181, false, false, now, past), "idle 181s ok");
        Check(UpdatePolicy.IsSafeToInstall(180, false, false, now, past), "idle exactly 180s ok");
        Check(!UpdatePolicy.IsSafeToInstall(179, false, false, now, past), "idle 179s not ok");
        Check(!UpdatePolicy.IsSafeToInstall(0, false, false, now, past), "idle 0 (unknown) not ok");
        Check(!UpdatePolicy.IsSafeToInstall(999, true, false, now, past), "session ending blocks");
        Check(!UpdatePolicy.IsSafeToInstall(999, false, true, now, past), "install in progress blocks");
        Check(!UpdatePolicy.IsSafeToInstall(999, false, false, now, now.AddHours(1)), "retry block blocks");
        Check(UpdatePolicy.IsSafeToInstall(999, false, false, now, now.AddHours(-1)), "expired retry block ok");

        double idle = UpdatePolicy.GetIdleSeconds();
        Check(idle >= 0, "GetIdleSeconds non-negative");

        Console.WriteLine("UpdateLogicTests: " + _pass + " passed, " + _fail + " failed");
        return _fail == 0 ? 0 : 1;
    }
}
