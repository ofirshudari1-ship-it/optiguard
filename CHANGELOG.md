# OptiGuard — Changelog / יומן שינויים

Format: [Keep a Changelog](https://keepachangelog.com/). English first, עברית below each entry.

---

## [4.10.1] — 2026-09-17

Installer visual-presentation polish pass. No app behavior changes.

Audited `Setup.cs` (the custom C#/WinForms installer — this project does not
use Inno Setup, unlike other tools in the family) against `assets/BRAND.md`:
the header bar, accent, and button colors already match the documented
palette exactly (`Theme.HeaderBg #0F2E27`, `Theme.Accent #10B981`), and the
installer's window/header icon already resolves to the real multi-resolution
branded `AppIcon.ico` (16–256px, green mark, transparent corners) via
`UiHelpers.GetBrandIcon()` — not a generic/placeholder icon. Also confirmed:
bilingual language picker (English/Hebrew) as the first screen, an
install-location Browse dialog, desktop/Start Menu shortcut checkboxes,
uninstaller registration in Add/Remove Programs (`RegisterUninstall`), and
explicit update-detection logic (`DetectExistingInstall`) that offers an
in-place "Update" flow when a prior install is found. No visual or feature
gaps required code changes; rebuilt to confirm the installer still produces
a clean `OptiGuard-Setup-4.10.1.exe`.

ביקורת עיצוב-התקנה בלבד, בלי שינוי התנהגות. `Setup.cs` (מתקין C#/WinForms
מותאם אישית — לא Inno Setup, בשונה משאר כלי המשפחה) הושווה מול
`assets/BRAND.md`: צבעי הכותרת/המבטא/הכפתורים כבר תואמים במדויק, והאייקון
בכותרת ההתקנה כבר משתמש ב-`AppIcon.ico` המותגי האמיתי (לא גנרי). אומתו גם
בורר שפה דו-לשוני, Browse למיקום התקנה, קיצורי דרך, רישום הסרה תקין,
וזיהוי התקנה קיימת עם זרימת "עדכון". לא נדרשו שינויי קוד - רק אימות ובנייה
מחדש.

## [4.10.0] — 2026-09-16

Product-improvement pass: competitor research (CCleaner, IObit Uninstaller,
Revo Uninstaller, BleachBit, Windows Storage Sense), two new features, and a
UI/performance review. No breaking changes.

### Competitor research summary / תקציר מחקר מתחרים
All the surveyed tools (CCleaner, IObit Uninstaller, Revo, BleachBit, Storage
Sense) already existed in OptiGuard's own feature set for duplicate/large-file
finding, browser-extension cleanup and startup management (see `SPEC.md`
section 4/6 - confirmed still current, not duplicated here). Two concrete gaps
were found that fit a lightweight local C# tool and don't duplicate existing
pages: (1) CCleaner and IObit Uninstaller both offer **scheduled/automatic
cleanup** - OptiGuard was manual-only; (2) several uninstallers surface some
notion of **"last used"/confidence** to help decide what's safe to remove -
OptiGuard had no equivalent. Explicitly NOT added: driver updaters (recognized
malware/PUP category, see SPEC.md 2.3), RAM cleaners / registry defrag
(pseudo-science on modern Windows, see SPEC.md 4) - researched and rejected on
purpose, consistent with the project's existing "no scareware" positioning.
כל הכלים שנבדקו (CCleaner, IObit Uninstaller, Revo, BleachBit, Storage Sense)
כבר היו מכוסים ב-OptiGuard (כפילויות, קבצים גדולים, תוספי דפדפן, ניהול הפעלה).
נמצאו שני חוסרים אמיתיים שמתאימים לכלי C# קליל: ניקוי מתוזמן, וציון ביטחון
"בטוח להסרה". נבדק ונדחה במכוון: מעדכני דרייברים (קטגוריית malware מוכרת),
ניקוי RAM/דפרגמנטציית רישום (פסאודו-מדע ב-Windows מודרני).

### Added / נוספו
- **"Safe to remove" confidence score on the Programs page**
  (`src/SafeRemovalData.cs`, wired into `ProgramsData.GetInstalledPrograms()`
  in `src/ProgramsData.cs`, two new columns "Last Used"/"Safe to Remove" in
  `src/MainWindow.cs` `BuildProgramsTab`, also added to the CSV export). Fully
  local heuristic: cross-references each program's candidate `.exe` name(s)
  against Windows Prefetch (`%WINDIR%\Prefetch\*.pf`) `LastWriteTime`, which
  Windows updates on every process launch and - unlike file
  `LastAccessTime` - keeps working even though NTFS last-access-time updates
  are disabled by default since Vista. No usage evidence found → labeled
  "unknown", never silently treated as safe. This is a confidence signal
  only; it changes nothing about how uninstall itself works, and every
  removal still goes through the existing explicit confirmation dialogs.
  ציון ביטחון "בטוח להסרה" בעמוד תוכנות - היוריסטיקה מקומית לגמרי המבוססת על
  קבצי Prefetch של Windows (שממשיכים לעבוד גם כש-NTFS last-access-time כבוי,
  ברירת המחדל מאז Vista). ללא ראיות שימוש → מסומן "לא ידוע", לעולם לא "בטוח"
  באופן שקט. רק אות ביטחון - לא משנה דבר בתהליך ההסרה עצמו.
- **Scheduled automatic cleanup** (`src/ScheduledCleanupData.cs`, settings UI
  in `src/MainWindow.cs` `BuildSettingsTab`, headless `--auto-clean` entry
  point in `src/Program.cs`, new `AppSettings.ScheduledCleanupEnabled`/
  `ScheduledCleanupFrequency` fields in `src/Settings.cs`). Off by default;
  when enabled, registers a Windows Task Scheduler task (`schtasks.exe`, no
  new dependency) that launches `OptiGuard.exe --auto-clean` Daily/Weekly/
  Monthly at 03:00. The unattended run reuses the exact same junk-cleaning
  code as the manual Junk Cleaner tab and the same `Quarantine` batch
  mechanism (7-day undo), **except it never empties the Recycle Bin**
  (an instant, non-reversible action - fine for an interactive click, not
  fine unattended). Skips the run entirely if OptiGuard is already open
  (single-instance mutex), and shows a one-time Toast next time the app is
  opened normally summarizing what an automatic run freed - nothing happens
  silently with no record. The scheduled task is also removed automatically
  during "Uninstall" (`Program.SelfUninstall`), matching the existing
  clean-uninstall standard for registry keys/shortcuts.
  ניקוי אוטומטי מתוזמן - כבוי כברירת מחדל; כשמופעל, נרשמת משימה במתזמן
  המשימות של Windows שמריצה ניקוי זבל ללא השגחה (יומי/שבועי/חודשי), באותו
  קוד ניקוי בדיוק כמו הטאב הידני, דרך אותו מנגנון הסגר (ביטול תוך 7 ימים) -
  אך לעולם לא מרוקנת את סל המיחזור אוטומטית. מדלגת אם OptiGuard כבר פתוח,
  ומציגה הודעת Toast חד-פעמית בפעם הבאה שהאפליקציה נפתחת. המשימה גם מוסרת
  אוטומטית בהסרת התוכנה.

### Audited, no changes needed / נבדק, לא נדרש שינוי
- Reviewed the scan/cleanup hot paths for the performance sweep requested this
  round (`JunkCleanerData.Clean`'s single-manifest-load-per-batch quarantine
  path, `ProgramsData.FindResidualItems`'s registry/filesystem scans,
  `DuplicateFileFinderData`'s SHA-1 comparison) - no slow/fragile pattern
  found beyond what prior audits already covered; the new Prefetch-based
  confidence scan reads its index once per app session (cached, not
  per-program) specifically to avoid adding a new slow path to the Programs
  list load.

## [4.9.0] — 2026-09-15

### Fixed / תוקנו
- **Installer filename was out of compliance with the project's own naming
  standard:** it built as `Setup-OptiGuard.exe` (tool name/"Setup" in the
  wrong order, no version number), while `_AUDIT/STANDARDS.md` section 1
  requires `<ToolName>-Setup-<version>.exe`. Renamed the installer's
  `AssemblyName` to `OptiGuard-Setup` and `build.ps1` now copies it to the
  root as `OptiGuard-Setup-<version>.exe` (currently `OptiGuard-Setup-4.9.0.exe`),
  auto-deleting any stale installer from a previous version so the root
  never has two installers side by side. All docs/landing page links updated
  to match (`README.md`, `USER-GUIDE.md`, `RELEASE-CHECKLIST.md`,
  `build/sign.ps1`, `src/UpdateChecker.cs` comment, `site/landing-page.html`).
  שם קובץ המתקין לא עמד בתקן השם של הפרויקט עצמו - נבנה כ-`Setup-OptiGuard.exe`
  (סדר הפוך, בלי מספר גרסה) במקום `OptiGuard-Setup-<version>.exe` הנדרש.
  תוקן בכל הקבצים הרלוונטיים; `build.ps1` גם מנקה קובץ ישן אוטומטית.
- **Installer pixel-level RTL mirroring** (the one gap documented as open
  after the 2026-09-14 pass): the wizard's hand-positioned WinForms pages
  (every control placed via `Point(x,y)`) previously only right-aligned
  Hebrew text without mirroring control layout order. Fixed by turning on
  `Form.RightToLeftLayout` for Hebrew in `Setup.cs` — the standard WinForms
  mechanism for this exact case: it applies `WS_EX_LAYOUTRTL` to the form's
  window handle, which Windows propagates automatically to every native
  child control, mirroring the whole page without recomputing any
  coordinate in the file (see the updated code comment on `SetupI18n` in
  `Setup.cs` for the full explanation and one flagged caveat: the header
  logo image itself will also render horizontally mirrored under this
  technique — likely unnoticeable since the brand mark is roughly
  symmetric, but **not confirmed visually** — no live Windows GUI session
  was available this pass; only `dotnet build` was verified clean).
  מיפוי RTL ברמת פיקסל במתקין (הפער האחרון שתועד כפתוח) - תוקן באמצעות
  `Form.RightToLeftLayout`, מנגנון ה-WinForms הסטנדרטי בדיוק לתרחיש הזה.
  **לא אומת ויזואלית** - אין סביבת Windows גרפית זמינה בסבב הזה.

### Added / נוספו
- `EULA.md` at project root (bilingual, AS-IS/no-warranty personal-use
  license) per `_AUDIT/STANDARDS.md` section 11.11 ("freeware still needs a
  license agreement"). Installed alongside the app by the installer (same
  embedded-resource pattern as `CHANGELOG.md`) and reachable from
  Settings → About via a new "View License (EULA)" button next to the
  existing "View Changelog" button.
  `EULA.md` חדש בשורש הפרויקט (דו-לשוני, AS IS/ללא אחריות) - נגיש גם מתוך
  ההגדרות דרך כפתור "הצג רישיון שימוש" חדש.

### Audited, found already compliant / נבדק ונמצא תקין
- Re-verified against `_AUDIT/STANDARDS.md` in full (all 17 sections): build
  (`dotnet build`, 0 errors/warnings on both projects), zero secrets, single
  instance guard (`Mutex` + installer `EnsureAppNotRunning`), bilingual i18n
  round-trip (507+ keys, both `locales/*.json` still valid JSON), splash
  screen, 5-screen onboarding, window state persistence, non-blocking update
  banner, uninstall data-deletion prompt, `Toast` component (§15.2 spec:
  top corner, 340px, 5s auto-dismiss + manual close), DPI awareness manifest
  flag, and the dead-settings cleanup from 2026-09-15's earlier pass all
  still hold - no regressions found.
  אומת מחדש מול כל 17 הסעיפים של STANDARDS.md - שום נסיגה לא נמצאה בכל מה
  שתועד כמושלם בסבב הקודם.

---

## [4.8.0] — 2026-09-14

### Added / נוספו
- Splash screen on startup (`src/SplashWindow.cs`): logo, name, version,
  indeterminate progress bar, status line; borderless with rounded corners
  and a subtle fade-in; shows for a minimum of 800ms even on a fast machine
  (so it never just flickers) while `MainWindow` is constructed, then closes
  itself. Verified visually with a real running window, not just build/log
  checks.
  מסך פתיחה (Splash) בהפעלה: לוגו, שם, גרסה, פס התקדמות, שורת סטטוס; ללא
  גבול חלון עם פינות מעוגלות ואנימציית כניסה עדינה; מוצג לפחות 800 מילישניות
  גם במחשב מהיר, נסגר לבד. אומת ויזואלית עם חלון אמיתי פועל.
- Installer language selection screen (English/Hebrew), shown first, before Welcome.
  מסך בחירת שפה במתקין (אנגלית/עברית), מוצג ראשון, לפני מסך הברוכים הבאים.
- New settings: notifications toggle, auto-check-for-updates, shutdown delay,
  minimize-on-close, confirm-destructive-actions, quarantine enable/retention,
  restore-point-before-cleanup.
  הגדרות חדשות: התראות, בדיקת עדכונים אוטומטית, השהיית כיבוי, מזעור בסגירה,
  אישור לפעולות הרסניות, הפעלת/משך הסגר, נקודת שחזור לפני ניקוי.
- `version.json` as the single source of truth for the version number, injected
  into both `.csproj` files at build time via `build/Version.props` and read
  back at runtime from the assembly version (was hardcoded in 3 places before).
  `version.json` כמקור אמת יחיד למספר הגרסה, מוזרק לשני קבצי ה-`.csproj` בזמן
  בנייה, ונקרא בחזרה בזמן ריצה מגרסת ה-assembly (היה קבוע בקוד ב-3 מקומות קודם).
- `build.ps1` — one command builds the main app, then the installer (which
  embeds it), then copies the final `Setup-OptiGuard.exe` to the project root.
  `build.ps1` — פקודה אחת שבונה את האפליקציה, אז את המתקין (שמטמיע אותה),
  ומעתיקה את `Setup-OptiGuard.exe` הסופי לשורש הפרויקט.
- `SPEC.md`, `DELETIONS.md` at project root (were missing).
  `SPEC.md`, `DELETIONS.md` בשורש הפרויקט (לא היו קיימים).
- Full 5-screen onboarding wizard (`OnboardingWindow.cs`): welcome+language,
  theme, what-it-does, safety tip, finish+copyright — replaces the old
  single-screen language/theme dialog. Visible "Skip" on every screen, Esc
  also skips, whatever was picked before skipping is kept. Re-run anytime
  from Settings → "Show Welcome Guide Again".
  אשף הפעלה-ראשונה מלא בן 5 מסכים - מחליף את הדיאלוג הישן החד-מסכי.
  כפתור "דלג" גלוי בכל מסך, Esc מדלג, ניתן להריץ שוב מההגדרות.
- Window size/position/maximized state remembered across sessions
  (`Settings.cs`: `WindowWidth/Height/Left/Top/Maximized`), with the saved
  position validated against current monitor bounds on restore (falls back
  to centered if a second monitor was unplugged since last run).
  גודל/מיקום/מצב-מוגדל של החלון נשמר בין הפעלות, עם אימות מול גבולות המסך.
- **Fixed a real small-screen bug:** the window's default size (1260×840)
  could open partially below the taskbar on a 1366×768 laptop screen —
  default size now clamps to the actual work area.
  **תוקן באג אמיתי במסכים קטנים:** גודל ברירת המחדל של החלון יכול היה
  להיפתח חלקית מתחת לשורת המשימות במסך לפטופ - עכשיו מוגבל לשטח העבודה בפועל.
- Installer wizard text (Welcome/Progress/Finish/footer/window title) now
  actually translates when Hebrew is picked on the language screen — it
  previously only set the *app's* language, leaving the installer itself in
  English regardless of the choice. Right-aligns Hebrew text; full pixel-level
  RTL mirroring of the hand-positioned WinForms layout was judged too risky
  to the working installer for this pass and is documented as a known gap.
  טקסט המתקין עצמו מתורגם כשבוחרים עברית (בעבר רק שפת האפליקציה השתנתה).
- "Browse..." button in the installer to change the install folder (was
  read-only before, contradicting the standard's "changeable" requirement).
  כפתור "עיון..." במתקין לשינוי תיקיית ההתקנה (הייתה קריאה-בלבד קודם).
- Self-uninstall now asks once "also delete settings/quarantine/logs?"
  (default No) instead of silently always keeping them with no way to
  actually clean up via the normal uninstall flow.
  הסרת ההתקנה שואלת כעת פעם אחת "למחוק גם הגדרות/הסגר/לוגים?" (ברירת מחדל לא).
- Non-blocking update banner on the Dashboard — **fixes a real dead
  setting:** "auto-check for updates" existed as a checkbox in Settings and
  saved to disk, but nothing ever read it back to actually check anything.
  Now genuinely checks in the background (if enabled + a manifest URL is
  configured) and shows a dismissible banner, not a blocking dialog, per the
  shared standard's "persistent state = banner" rule.
  באנר עדכון לא-חוסם ב-Dashboard - **מתקן הגדרה מתה אמיתית:** "בדיקת עדכונים
  אוטומטית" הייתה קיימת בהגדרות ונשמרה, אבל שום דבר לא קרא אותה בפועל.
- Landing page (`site/landing-page.html`) is now fully bilingual with a
  working language toggle and complete RTL for Hebrew (was English-only,
  no toggle at all before) — matches the "same bilingual rule applies to the
  landing page" requirement. Added an FAQ section (5 real questions:
  cloud/data, admin rights, deleted files, registry safety, cost).
  דף הנחיתה דו-לשוני מלא עם מתג שפה עובד ו-RTL מלא לעברית + סעיף שאלות נפוצות.
- `build/sign.ps1` — documented code-signing template (thumbprint param,
  timestamping, signtool auto-discovery). Not run automatically by
  `build.ps1` and never touches certificates on its own — signing stays a
  manual, human-only step.
  `build/sign.ps1` - תבנית חתימת קוד מתועדת. לא רץ אוטומטית, שלב ידני בלבד.

### Changed / שונו
- All UI text (486 keys × 2 languages) moved out of `src/I18n.cs` into
  `locales/en.json` / `locales/he.json` — zero hardcoded strings left in the
  code. Extracted programmatically from the live dictionary (not
  hand-transcribed) to avoid transcription errors in Hebrew text, format
  placeholders (`{0}`, `{1:00}`), and apostrophes. Both files are shipped
  next to `OptiGuard.exe` (via the `.csproj`'s `Content` items for dev
  builds, and embedded + written out by `Setup.cs` for installs) and loaded
  once at first use; a missing/corrupt file falls back to an empty
  dictionary rather than crashing (`T()` already falls back to English, then
  to the raw key). Verified end-to-end: extraction round-trip, format
  placeholders, missing-key fallback, and byte-identical embedded-resource
  extraction all checked before removing the old embedded dictionary.
  כל טקסט הממשק (486 מפתחות × 2 שפות) הועבר מ-`src/I18n.cs` ל-
  `locales/en.json`/`locales/he.json` - אפס מחרוזות קשיחות בקוד. חולץ
  אוטומטית מהמילון החי (לא הועתק ידנית) כדי למנוע טעויות בטקסט עברי,
  placeholders של פורמט, וגרשיים. שני הקבצים מותקנים לצד `OptiGuard.exe`.
- Migrated both the main app and the installer from manual `csc.exe`
  compilation to SDK-style `.csproj` projects built with `dotnet build`
  (`src/OptiGuard.csproj`, `Setup.csproj`) — reproducible, no more "works on
  my machine" builds.
  שני הפרויקטים עברו מקומפילציה ידנית עם `csc.exe` לפרויקטי `.csproj` בסגנון
  SDK, נבנים עם `dotnet build` — בר-שחזור, לא עוד "עובד רק אצלי".
- Dashboard layout compacted (smaller padding/margins on the health-score
  card, stat cards, and wizard cards) so it fits on screen without scrolling
  at the default window size.
  פריסת ה-Dashboard דוחסה (padding/margins קטנים יותר בכרטיס בריאות המערכת,
  כרטיסי הסטטיסטיקה וכרטיסי האשפים) כך שהכל נכנס במסך בלי גלילה בגודל חלון ברירת המחדל.
- Root folder reorganized to match the shared cross-tool standard: dev-only
  tool (`GenIcon`) moved under `build/tools/`, one-off audit reports moved
  under `build/reports/`, marketing page moved to `site/`, spec/changelog
  consolidated at root instead of `docs/`.
  תיקיית השורש אורגנה מחדש לפי התקן המשותף: כלי הפיתוח (`GenIcon`) עבר ל-
  `build/tools/`, דוחות ביקורת חד-פעמיים עברו ל-`build/reports/`, דף השיווק
  עבר ל-`site/`, האפיון והיומן אוחדו בשורש במקום ב-`docs/`.

### Fixed / תוקנו
- **Critical:** app crashed on first launch with "Must disconnect specified
  child from current parent Visual before attaching to new parent Visual."
  Root cause: the Dashboard tab built a `StackPanel`, assigned it to a
  `ScrollViewer.Content`, then tried to re-parent the same instance again.
  Fixed by constructing the panel and viewer together, once.
  **קריטי:** קריסה בהפעלה ראשונה עם שגיאת "Must disconnect specified child...".
  הסיבה: טאב ה-Dashboard יצר `StackPanel`, שייך אותו ל-`ScrollViewer.Content`,
  ואז ניסה לשייך את אותו מופע שוב. תוקן על ידי בנייה משותפת חד-פעמית.
- **Critical:** app crashed on first launch with "Installation not recognized
  for user profile" (`התקנה לא הוכרה לפרופיל`). Root cause: the first-launch
  dialog called `Theme.Get(...)` before `Theme.Load()` had run. Fixed the
  initialization order (`Theme.Load` + `I18n.CurrentLang` now run before the
  dialog is shown).
  **קריטי:** קריסה בהפעלה ראשונה עם "התקנה לא הוכרה לפרופיל". הסיבה: דיאלוג
  ההפעלה-ראשונה קרא ל-`Theme.Get(...)` לפני ש-`Theme.Load()` רץ. סדר האתחול תוקן.
- **Critical build blocker:** `src/Program.cs` had C# 8+ syntax (`using var`,
  named arguments) mixed into a project still being compiled by the legacy
  `csc.exe` (C# 5 only) at the time — this made the project fail to compile
  at all. Fixed the syntax to be broadly compatible; the project has since
  moved to SDK-style `.csproj` with `LangVersion=latest` so this class of
  issue can't recur.
  **חוסם בנייה קריטי:** `src/Program.cs` הכיל תחביר C# 8+ שנוסף לפרויקט
  שעדיין הודר עם `csc.exe` הישן (C# 5 בלבד) - זה מנע כל קומפילציה. תוקן,
  והפרויקט עבר מאז ל-`.csproj` בסגנון SDK כך שהבעיה לא יכולה לחזור.
- No running-instance check existed anywhere — installing/updating while
  OptiGuard was open could fail partway through with a locked-file error.
  Added `EnsureAppNotRunning()` to `Setup.cs` (checks
  `Process.GetProcessesByName`, blocks with a retry/cancel prompt) before
  any files are written, and a named `Mutex` in `src/Program.cs` so a second
  launch of the app itself exits immediately instead of opening a second window.
  לא הייתה בדיקת מופע-רץ בשום מקום - התקנה/עדכון בזמן שהתוכנה פתוחה יכלו
  להיכשל עם שגיאת קובץ-נעול. נוספה בדיקה במתקין ו-Mutex באפליקציה עצמה.
- `Setup.csproj` (the new SDK-style installer project) had no
  `<EmbeddedResource>` entry for the main app's exe — it would have compiled
  successfully but produced a broken installer that fails at install time
  with "Resource not found". Added the embed + a build-order guard.
  ל-`Setup.csproj` החדש לא היה `<EmbeddedResource>` לקובץ האפליקציה - זה היה
  מקמפל בהצלחה אבל מייצר מתקין שבור שנכשל בזמן התקנה. נוסף הטמעה + שמירה על סדר בנייה.
- Landing page's version references and download link were stale/broken
  ("v4.5", `href="#"` placeholder instead of the real installer) — fixed to
  4.8 and a working relative link to `../Setup-OptiGuard.exe`.
  גרסה לא מעודכנת וקישור הורדה שבור בדף הנחיתה - תוקנו.

### Added / נוספו (continued)
- Reusable Toast notification component (`ToastWindow.cs`) per the shared
  standard's exact spec: top-right corner (top-left in RTL), fixed 340px
  width, auto-dismiss after 5s, always-available manual close, non-blocking.
  Wired to the one background operation that previously ran completely
  silently every launch (quarantine purge) — shows "Cleaned up N old items"
  only when something actually happened, gated by the (previously dead)
  "enable notifications" setting.
  רכיב Toast חוזר לפי המפרט המדויק של התקן; מחובר לניקוי הסגר ברקע שהיה שקט
  לגמרי קודם.
- Quarantine retention period is now a real dropdown in Settings (1/3/7/14/30
  days) that's actually used by the startup purge — was hardcoded to 7
  regardless of what `QuarantineRetentionDays` in settings.json said.
  משך שמירת ההסגר הוא כעת בורר אמיתי בהגדרות, בפועל בשימוש.

### Removed / הוסרו (continued)
- Five `AppSettings` fields that were saved/loaded but never read by any
  actual behavior, with no UI to change them either: `MinimizeOnClose`,
  `ShutdownDelaySeconds`, `ConfirmDestructiveActions`,
  `CreateRestorePointsBeforeCleanup` (duplicate of the already-working
  `CreateRestorePoints`), `EnableQuarantine`. Reasoning for each documented
  in `DELETIONS.md`.
  חמישה שדות הגדרות שנשמרו/נטענו אך מעולם לא נקראו על ידי שום התנהגות
  בפועל, וגם לא הייתה להם ממשק לשינוי. הנימוקים מתועדים ב-`DELETIONS.md`.

### Removed / הוסרו
- "Windows Apps" (UWP) and "Install Monitor" as separate nav tabs — low
  usage, added confusion. ~150 lines of now-dead code removed
  (`BuildUwpTab`, `RefreshUwp`, `BuildInstallMonitorTab`, the `UwpAppInfo`
  model, `UwpData.cs` entirely, the `FingerprintRow` row class, and the
  now-always-zero "Windows Apps Removed" dashboard stat). Install-fingerprint
  detection itself is unaffected — it still runs, just without its own page
  (see `SPEC.md` §6).
  "אפליקציות Windows" ו"מעקב התקנה" כטאבים עצמאיים - שימוש נמוך, בלבול. הוסרו
  כ-150 שורות קוד מת. זיהוי טביעת-אצבע להתקנה עצמו לא נפגע - הוא ממשיך לפעול,
  רק בלי עמוד נפרד.

---

## [4.5.0] and earlier — condensed history / היסטוריה מקוצרת

*(Full detail was in `docs/CHANGELOG.md`, merged here.)*

- **4.5 — Project cleanup:** removed a duplicate/old installer from the
  previous "PureSys" brand; rebuilt from scratch to confirm no leftovers;
  added full documentation (spec, changelog, landing page).
  **4.5 — סדר בפרויקט:** הוסר קובץ התקנה כפול מהמותג הקודם "PureSys", נבנה
  מחדש מאפס, נוספה תיעוד מלא.
- **4.4 — UI/UX pass based on research:** fixed "one primary action per
  screen" violation on the Programs page (2 green buttons → 1); destructive
  (red) buttons physically separated from safe actions; fixed a semantic
  color bug ("Update software" was red/dangerous, should be green); 16
  regression + 21 edge-case tests, all passing.
  **4.4 — סידור UI/UX לפי מחקר:** תוקנה הפרת "כפתור בולט אחד", כפתורים
  הרסניים הופרדו, תוקנה טעות סמנטית בצבע.
- **4.3 — Preventive hardening:** automatic cleanup of old log files and
  registry backups (30+ days) on startup, in the background.
  **4.3 — חיזוק מונע:** ניקוי אוטומטי של לוגים וגיבויים ישנים ברקע.
- **4.2 — Critical fix: quarantine data loss.** The quarantine manifest
  (JSON) exceeded `JavaScriptSerializer`'s hard 2MB ceiling — writes failed
  silently. Measured real-world impact: 4,378 files (5.5GB) stuck in
  quarantine, unrecoverable. Fixed by removing the ceiling and batching
  load/save once per operation instead of once per file — measured
  performance improvement from 69ms/file to 0.7ms/file (99x).
  **4.2 — תיקון קריטי: אובדן נתונים בהסגר.** מניפסט ההסגר חצה תקרת 2MB של
  `JavaScriptSerializer` - כתיבה נכשלה בשקט. השפעה נמדדת: 4,378 קבצים
  (5.5GB) תקועים בלתי ניתנים לשחזור. שופר פי 99 בביצועים.
- **4.1 — New tools from market research:** Disk Space Analyzer, Duplicate
  File Finder (SHA1 content comparison), driver health check (status only,
  **no** driver downloads — a recognized scareware category).
  **4.1 — כלים חדשים ממחקר שוק:** מנתח שטח דיסק, מוצא כפילויות, בדיקת בריאות דרייברים.
- **4.0 — Accessibility & design:** High Contrast theme; fixed hardcoded
  white text on accent backgrounds not responding to theme; expanded health
  score (ghost registry entries + risky browser extensions); fixed health
  score not auto-refreshing on dashboard navigation.
  **4.0 — נגישות ועיצוב:** ערכת ניגודיות גבוהה, הרחבת ציון הבריאות.
- **3.9 — "Intelligence" and smart navigation:** PC Health Score (0-100,
  real data-based); Activity Timeline page; Ctrl+K command palette.
  **3.9 — "בינה" וניווט חכם:** ציון בריאות מחשב, ציר זמן פעילות, פלטת פקודות.
- **3.8 — Fixed cleanup freeze:** the "choose what to delete" screen blocked
  the UI thread (restore point creation + deletion without `Task.Run`).
  Made fully async with a real progress bar on all deletion screens; restore
  point creation skipped if one was already made in the last 4 hours.
  **3.8 — תיקון תקיעה בניקוי:** הפך לאסינכרוני מלא + פס התקדמות אמיתי.
- **3.6–3.7 — Security, privacy, and smart center:** Security Center (real
  Defender/Firewall/Windows Update status); Privacy panel (7 real toggles);
  evidence-based registry cleaning; heuristic phishing checker; Smart /
  Security / Privacy / Registry wizards.
  **3.6–3.7 — אבטחה, פרטיות ומרכז החכמה.**
- **3.x and earlier — the core:** full program uninstall (normal/deep/force)
  including APPDATA and registry leftovers; dashboard with cumulative
  stats; Install Monitor for precise future removal; full bilingual support
  (Hebrew RTL / English) defaulting to English; grouped side navigation with
  transition animations; rebrand to **OptiGuard** (from PureSys / Uninstaller Pro).
  **3.x ומוקדם יותר — הליבה:** הסרת תוכנות מלאה, דשבורד, מעקב התקנה, תמיכה
  דו-לשונית, ניווט צידי, מיתוג מחדש ל-OptiGuard.
