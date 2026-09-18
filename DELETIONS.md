# DELETIONS.md — OptiGuard

Log of files removed or superseded during the STANDARDS.md compliance pass
(2026-09-14). Per the hard rule in STANDARDS.md, nothing is deleted without
being listed here first with a reason. Files *moved* (not deleted) are noted
for traceability but are not "deletions" — content is preserved at the new path.

## Deleted (superseded, content merged elsewhere)

| File | Reason | Where the content went |
|---|---|---|
| `docs/CHANGELOG.md` | Duplicate changelog (Hebrew, historical 3.x-4.5). Standard requires ONE `CHANGELOG.md` at project root. | Merged into root `CHANGELOG.md` (bilingual, Keep a Changelog format). |
| `docs/SPECIFICATION.md` | Standard requires `SPEC.md` at project root, not `docs/`. | Content moved to root `SPEC.md`. |

## Removed (dead settings, 2026-09-15)

Found while auditing `_AUDIT/STANDARDS.md` §2 ("remove dead code") against
actual usage: five `AppSettings` fields were persisted (saved/loaded to
`settings.json`) but never read by any behavior anywhere in the codebase, and
had no UI to even change them. Removed from `src/Settings.cs` and the
installer's initial-settings JSON in `Setup.cs`:

| Field | Reason removed |
|---|---|
| `MinimizeOnClose` | No tray-icon infrastructure exists, and `_AUDIT/STANDARDS.md` §12.1 explicitly lists OptiGuard as a one-shot tool where "X closes fully, no tray justification" — this setting couldn't be honored even if wired up. |
| `ShutdownDelaySeconds` | No corresponding "shut down the PC after cleanup" feature exists or was requested; looked like a speculative field with nothing behind it. |
| `ConfirmDestructiveActions` | Every destructive action already shows an unconditional `Dialogs.Confirm(...)` — the product's own SPEC/landing page present "always confirms before deleting" as a core promise, not a user-configurable toggle. Making confirmation optional would be a safety regression, not a feature. |
| `CreateRestorePointsBeforeCleanup` | Duplicate of `CreateRestorePoints`, which already exists, has UI, and is wired to `RestorePoint.Enabled`. |
| `EnableQuarantine` | Would require touching every quarantine call-site across Junk Cleaner, Registry Cleaner, Programs, Duplicate Finder etc. to support a real "skip quarantine" path — meaningful surgery across many features with no way to visually verify each path this session. Deferred rather than wired up half-confidently. |

`QuarantineRetentionDays` was in the same "dead setting" state (hardcoded `7`
was used instead of reading it) but was cheap and safe to actually wire up
instead of removing — done in the same pass (now a real dropdown in Settings,
and used in the startup purge call).

## Renamed build output (2026-09-15, v4.9.0 pass)

`Setup-OptiGuard.exe` (the old installer filename, no version number in it)
is not a "deletion" in the DELETIONS.md sense — it's disposable build output,
reproducible from source by running `build.ps1` — but it's flagged here
because the OLD file at the project root was removed and replaced by
`OptiGuard-Setup-4.9.0.exe`, matching `_AUDIT/STANDARDS.md` section 1's
required pattern `<ToolName>-Setup-<version>.exe` (OptiGuard was previously
out of compliance with its own audited standard here: `Setup-OptiGuard.exe`
had the tool name and the word "Setup" in the wrong order versus the
required pattern, and carried no version number at all, which is exactly
the "which installer is current?" ambiguity the hard versioning rule at the
top of STANDARDS.md exists to prevent). `build.ps1` now also auto-deletes
any stale `OptiGuard-Setup-*.exe` from a previous version on every run, so
two installers never sit side by side in the root again.

## Moved (not deletions — same content, new location per standard folder layout)

| From | To | Reason |
|---|---|---|
| `docs/landing-page.html` | `site/landing-page.html` | Standard layout: marketing page belongs in `site/`. |
| `tools/GenIcon.cs`, `tools/GenIcon.csproj` | `build/tools/GenIcon.cs`, `build/tools/GenIcon.csproj` | Dev-only tool, not part of the shipped product — belongs under `build/`, not visible in root-level `tools/`. |
| `IMPROVEMENT_REPORT.md` | `build/reports/IMPROVEMENT_REPORT.md` | One-off internal report, not part of the standard root file set (installer/CHANGELOG/SPEC/README/site/data only). |
| `SECURITY_AUDIT.md` | `build/reports/SECURITY_AUDIT.md` | Same as above — historical audit snapshot, kept for reference under `build/`. |
| `AppIcon.ico` | `assets/icons/AppIcon.ico` (root copy kept as-is for installer/csproj references — see note) | Standard requires brand assets under `assets/icons/`. |

**Note on `AppIcon.ico`:** both `Setup.csproj` and `src/OptiGuard.csproj` reference
`AppIcon.ico` by relative path for `ApplicationIcon`. Rather than risk breaking
the build by moving the only copy, a canonical copy was added at
`assets/icons/AppIcon.ico` for the brand-asset standard, and the root copy was
kept in place as the one the build actually consumes. This is flagged here so
it doesn't look like an accidental duplicate.
GenIcon.exe (old build output) was discarded along with tools/ removal — reproducible from build/tools/GenIcon.csproj, no approval needed per the standard's disposable-build-output exception.
