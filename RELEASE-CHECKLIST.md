# OptiGuard — Release Checklist

Run through this before shipping a new version. Based on the
Definition-of-Done in `_AUDIT/STANDARDS.md` §10, scoped to what actually
applies to this project.

## Before building
- [ ] `version.json` bumped (semantic versioning: MAJOR.MINOR.PATCH)
- [ ] `CHANGELOG.md` has an entry for this version (Added/Changed/Fixed/Removed, EN+HE)

## Build
- [ ] `.\build.ps1` runs clean from a fresh clone (0 errors, 0 warnings)
- [ ] `OptiGuard-Setup-4.10.0.exe` lands at the project root — the only installer file
- [ ] No duplicate/old installer files anywhere in the project

## Install / update behavior
- [ ] Clean install to `C:\Program Files\OptiGuard` works
- [ ] Installing over an existing install updates in place — one entry in
      "Add or Remove Programs", not two
- [ ] **Installer detects OptiGuard already running** and asks to close it
      before touching files (`EnsureAppNotRunning` in `Setup.cs`)
- [ ] **App itself refuses a second instance** (named `Mutex` in `Program.cs`)
- [ ] Uninstall removes files, shortcuts, and the registry entry cleanly

## First run
- [ ] First-launch dialog (language + theme) shows once, never again after
- [ ] Dashboard fits on screen without scrolling at the default window size

## Language / RTL
- [ ] Installer language screen shows English + Hebrew, English is not forced
- [ ] App defaults to English on first run; switching to Hebrew in Settings
      flips layout to RTL correctly
- [ ] Spot-check a few screens in Hebrew for clipped or overlapping text

## Locales
- [ ] `locales/en.json` and `locales/he.json` have the same key count
      (`(Get-Content locales\en.json | ConvertFrom-Json).PSObject.Properties.Count`
      should equal the same for `he.json`)
- [ ] No new hardcoded UI strings were added directly in `.cs` files — check
      `git diff` for string literals inside `TextBlock`/`Label`/`MessageBox` calls

## Manual smoke test (real GUI, not just build)
- [ ] Launch the app, click through Dashboard → Programs → Settings at minimum
- [ ] Trigger one destructive action (e.g. Junk Cleaner) and confirm the
      quarantine/restore path works
- [ ] Check `%APPDATA%\UninstallerPro\Log_*.txt` for unexpected errors after
      the smoke test

## Docs
- [ ] `README.md`, `SPEC.md`, `USER-GUIDE.md` reflect any new/removed features
- [ ] `assets/BRAND.md` still matches `src/Theme.cs` if colors changed
