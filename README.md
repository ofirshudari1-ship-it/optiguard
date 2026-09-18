# OptiGuard

**All-in-one PC cleanup, uninstaller, and diagnostics for Windows — without the fear-mongering.**

## What it does

Most "PC cleaner" tools scare you into upgrading by claiming to find thousands of fake errors, and generic registry cleaners can break working software. OptiGuard takes the opposite approach: it uninstalls programs completely (removing leftover files and registry entries a normal uninstall leaves behind), cleans junk files, finds duplicate files, analyzes disk space, and flags only registry entries it can actually prove are broken. It reads real status from Windows itself — Defender, Firewall, Windows Update, Device Manager — instead of faking its own results, and rolls it all up into a single PC health score with a direct "fix this" link for every issue found. Every risky action is protected by a 7-day recoverable quarantine and an automatic system restore point, so nothing is ever gone for good by accident.

## Download & install

**[Download the latest version](https://github.com/ofirshudari1-ship-it/optiguard/releases/latest)**

1. Download `OptiGuard-Setup-<version>.exe` from the link above.
2. Run the installer and choose your language (English or Hebrew).
3. Follow the setup wizard — the default options work for almost everyone.
4. On first launch, pick your language and theme once (you can change either later in Settings).
5. Done — start from the Dashboard for your PC health score and guided cleanup wizards.

**System requirements:** Windows 10 (20H2 or later) or Windows 11, with .NET Framework 4.8 (usually already installed; Windows Update installs it automatically if missing). OptiGuard requires **administrator rights** to run, since it needs to read and clean system-level registry entries and access protected folders — expect a UAC prompt on every launch.

## Key features

- **Full program uninstall** — normal, deep, or forced removal, including leftover `%APPDATA%` folders and registry entries a standard Windows uninstall leaves behind
- **"Safe to remove" confidence score** on the Programs list, based on real Windows Prefetch usage data — never guesses, labels unknown programs "unknown" rather than falsely calling them safe
- **Junk cleaner** with optional scheduled automatic cleanup (off by default, opt-in), all routed through the recoverable quarantine
- **Disk space analyzer** and **duplicate file finder** (real content comparison, not just name/size matching)
- **Browser extension manager** with a permission risk rating for Chrome, Edge, and Firefox extensions
- **Security Center** and **Privacy panel** reading real Windows Defender, Firewall, and Windows Update status, plus real Windows privacy toggles
- **Evidence-based registry cleaner** that only flags entries pointing to files that verifiably no longer exist, with an automatic backup before anything is removed
- **Phishing link checker**, disk health (S.M.A.R.T.) monitoring, and one-click quick fixes
- **PC health score** (0–100) built from real system data, with an activity timeline and direct fixes for every flagged issue
- Fully bilingual (English/Hebrew with full RTL) and includes a dedicated high-contrast accessibility theme

## Automatic updates

OptiGuard checks for new versions automatically and shows a simple, dismissible banner when one is available — no forced installs, no interrupting your work. You can always grab the newest release yourself from the [Releases page](https://github.com/ofirshudari1-ship-it/optiguard/releases).

## Privacy

OptiGuard is a local-first tool: it runs entirely on your own PC and never sends your files, registry contents, or personal data to any remote server. The only network activity is an optional, explicit check for new versions, which fetches nothing more than version information.
