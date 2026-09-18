# OptiGuard

**All-In-One PC Care** — a Windows maintenance app: full program uninstall,
system cleanup, security & privacy center, and a smart PC health score.
Current version: **4.10**

תוכנת תחזוקת מחשב ל-Windows "הכל באחד": הסרת תוכנות מלאה, ניקוי מערכת, מרכז
אבטחה ופרטיות, וציון בריאות מחשב חכם. גרסה נוכחית: **4.10**

---

## What it does / מה התוכנה עושה

OptiGuard uninstalls programs completely (including leftover files and
registry entries), cleans junk files, finds duplicate files, analyzes disk
space, checks Windows Defender / Firewall / Windows Update status, cleans
evidence-based "ghost" registry entries, and scores your PC's health out of
100 — with a direct "fix this" link for every issue found. Every risky action
goes through a 7-day recoverable quarantine and an automatic restore point
first. No fear-mongering fake error counts, no upsells, no telemetry.

OptiGuard מסיר תוכנות במלואן (כולל שאריות קבצים ורישום), מנקה קבצי זבל, מוצא
קבצים כפולים, מנתח שטח דיסק, בודק סטטוס Windows Defender/חומת אש/Windows
Update, מנקה רשומות רישום "רפאים" מבוססות-ראיות, ונותן ציון בריאות מחשב מ-100
עם כפתור "תקן זאת" לכל בעיה. כל פעולה מסוכנת עוברת דרך הסגר בר-שחזור של 7 ימים
ונקודת שחזור אוטומטית קודם. בלי הפחדות, בלי הצעות שדרוג, בלי איסוף נתונים.

## Who it's for / למי זה מיועד

Windows users who want one trustworthy tool for PC maintenance instead of
juggling several apps — and want to actually understand what each cleanup
step does, not just click "Fix All" and hope for the best.

משתמשי Windows שרוצים כלי אמין אחד לתחזוקת המחשב במקום כמה תוכנות שונות -
ורוצים להבין מה כל שלב ניקוי עושה בפועל, לא רק ללחוץ "תקן הכל" ולקוות לטוב.

## System requirements / דרישות מערכת

- Windows 10 (20H2+) or Windows 11
- .NET Framework 4.8 (usually already installed; Windows Update installs it
  automatically if missing)
- Administrator rights (needed for registry and Program Files access — a UAC
  prompt appears on every launch)

- Windows 10 (‏20H2 ומעלה) או Windows 11
- ‏.NET Framework 4.8 (בדרך כלל כבר מותקן; Windows Update מתקין אוטומטית אם חסר)
- הרשאות מנהל (נדרש לגישה לרישום ול-Program Files - חלון UAC יופיע בכל הפעלה)

## Installing / התקנה

1. Run `OptiGuard-Setup-4.10.0.exe`.
2. Choose your language (English/Hebrew) on the first screen.
3. Follow the wizard — the default options work for almost everyone.
4. On first launch, pick your language and theme once; you can change either
   later from Settings.

1. הרץ את `OptiGuard-Setup-4.10.0.exe`.
2. בחר שפה (אנגלית/עברית) במסך הראשון.
3. עקוב אחר האשף - האפשרויות ברירת המחדל מתאימות לרוב המשתמשים.
4. בהפעלה הראשונה, בחר שפה וערכת נושא פעם אחת; ניתן לשנות מאוחר יותר בהגדרות.

If OptiGuard is already open when you run the installer, it will ask you to
close it first — this prevents a failed update.

אם OptiGuard כבר פתוחה כשמריצים את המתקין, הוא יבקש לסגור אותה קודם - זה מונע עדכון שנכשל.

## Using it / שימוש

Pick a page from the left-hand navigation. Start with **Dashboard** for an
overall health score and one-click guided wizards (general cleanup, security,
privacy, registry). Every list (Programs, Junk Cleaner, Registry Cleaner...)
lets you review exactly what's selected before anything is removed.

בחר עמוד מהניווט הצידי. התחל מ-**Dashboard** לציון בריאות כללי ואשפים מונחים
בלחיצה אחת (ניקוי כללי, אבטחה, פרטיות, רישום). כל רשימה מאפשרת לבדוק בדיוק
מה נבחר לפני שמשהו נמחק.

## Troubleshooting / פתרון תקלות

**"OptiGuard is currently running" during install/update** — close the app
first (check the system tray / taskbar), then retry.
**"OptiGuard רצה כרגע" בזמן התקנה/עדכון** — סגור את התוכנה קודם, ואז נסה שוב.

**Something crashed** — check the log file at
`%APPDATA%\UninstallerPro\Log_<date>_<time>.txt` (also linked directly in the
error dialog). It records what happened right before the error, with no
sensitive data.
**קרתה קריסה** — בדוק את קובץ הלוג ב-`%APPDATA%\UninstallerPro\Log_<תאריך>_<שעה>.txt`
(גם מקושר ישירות בחלון השגיאה). הוא מתעד מה קרה ממש לפני השגיאה, בלי מידע רגיש.

**Accidentally deleted something you needed** — check **Junk Cleaner →
Quarantine** (or the equivalent section) within 7 days; deleted files are
recoverable there before they're purged for good.
**מחקת בטעות משהו שהיית צריך** — בדוק בהסגר (Quarantine) בתוך 7 ימים; קבצים
שנמחקו ניתנים לשחזור שם לפני שהם נמחקים סופית.

---

## For developers / למפתחים

```
OptiGuard/
├── OptiGuard-Setup-4.10.0.exe   ← the final installer (the only file end users need)
├── Setup.cs / Setup.csproj  ← installer source
├── version.json          ← single source of truth for the version number
├── build.ps1             ← one command: builds the app, then the installer
├── CHANGELOG.md, SPEC.md, README.md, EULA.md
├── site/                 ← marketing landing page
├── assets/               ← brand assets (icons, BRAND.md)
├── src/                  ← main app source (C# / WPF, .NET Framework 4.8)
└── build/                ← dev tools (GenIcon), historical reports, Version.props
```

Rebuild everything with:

```powershell
.\build.ps1
```

This reads `version.json`, builds `src/OptiGuard.csproj`, then `Setup.csproj`
(which embeds the just-built exe), and copies the result to
`OptiGuard-Setup-4.10.0.exe` at the project root. To bump the version, edit
`version.json` only — it's injected everywhere else automatically.

**Technical note:** the app's internal C# namespace is still `UninstallerPro`
(historical name) — an invisible implementation detail that affects nothing
user-facing (folder name, build output, installer, what actually gets
installed are all `OptiGuard`). Renaming it across 30+ files is unnecessary
mechanical risk for no visible benefit.

## User data (not in this folder) / נתוני משתמש (לא בתיקייה הזו)

The app stores its data (settings, quarantine, logs, stats) in
`%APPDATA%\UninstallerPro` on the end user's machine, never inside the
project folder. This includes: `settings.json`, `stats.json`, `Quarantine/`
(with `manifest.json`), `RegistryBackups/`, and `Log_*.txt` files.
