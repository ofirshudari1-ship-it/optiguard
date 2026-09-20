# OptiGuard — User Guide / מדריך למשתמש

## Getting started / התחלה

1. Run `OptiGuard-Setup-4.10.0.exe`. Choose your language on the first screen — this
   sets the language for both the installer and the app.
   הרץ את `OptiGuard-Setup-4.10.0.exe`. בחר שפה במסך הראשון - זה קובע את השפה גם
   למתקין וגם לאפליקציה.
2. On first launch, pick a language and theme (Light / Dark / High Contrast).
   You won't see this dialog again — change either later from **Settings**.
   בהפעלה הראשונה, בחר שפה וערכת נושא. הדיאלוג לא יופיע שוב - ניתן לשנות
   מאוחר יותר ב-**הגדרות**.
3. You'll land on **Dashboard** — your PC health score and four guided
   wizards (general cleanup, security, privacy, registry).
   תגיע ל-**Dashboard** - ציון בריאות המחשב וארבעה אשפים מונחים.

## Dashboard

- **Health score (0-100):** based on real checks (junk buildup, startup
  clutter, risky extensions, ghost registry entries, disk health...). Click
  **"Fix this"** next to any listed issue to jump straight to the page that
  fixes it.
  **ציון בריאות (0-100):** מבוסס על בדיקות אמיתיות. לחץ **"תקן זאת"** ליד כל
  בעיה כדי לקפוץ ישר לעמוד שמתקן אותה.
- **Wizards:** one-click guided flows. Each shows exactly what it found
  before doing anything — nothing is deleted without your review.
  **אשפים:** תהליכים מונחים בלחיצה אחת. כל אשף מציג בדיוק מה נמצא לפני שהוא
  עושה משהו - שום דבר לא נמחק בלי שראית קודם.

## Removing programs / הסרת תוכנות

Go to **Programs**. Select one (or several, with checkboxes), then choose:
- **Uninstall** — runs the program's own uninstaller.
- **Deep clean** — also searches for and removes leftover files/registry
  entries the uninstaller left behind.
- **Force remove** — for programs whose uninstaller is broken or missing.

עבור ל-**תוכנות**. בחר אחת (או כמה), ואז בחר: **הסר** (מריץ את המסיר של
התוכנה), **ניקוי עמוק** (גם מחפש ומסיר שאריות), **הסרה כפויה** (לתוכנות
עם מסיר שבור).

Every removal that deletes files sends them to **quarantine** first (see
below) — nothing is gone for good immediately.

## Quarantine — undoing a mistake / הסגר - ביטול טעות

Deleted files aren't removed immediately — they sit in an internal
quarantine folder for **7 days**, fully recoverable, before being purged for
good. Look for a "restore" / "undo last cleanup" option wherever files were
just deleted.

קבצים שנמחקו לא נעלמים מיד - הם יושבים בהסגר פנימי ל-**7 ימים**, ניתנים
לשחזור מלא, לפני שהם נמחקים סופית. חפש אפשרות "שחזר"/"בטל ניקוי אחרון" בכל
מקום שמשהו נמחק זה עתה.

## Security & Privacy / אבטחה ופרטיות

- **Security Center** shows the *real* status of Windows Defender, Firewall,
  and Windows Update (not a fake parallel engine) and can trigger a real
  quick scan.
  **מרכז אבטחה** מציג את הסטטוס האמיתי של Defender/חומת אש/Windows Update.
- **Privacy** shows 7 real Windows privacy toggles (the same registry keys
  Windows Settings itself uses).
  **פרטיות** מציג 7 מתגי פרטיות אמיתיים.
- **Phishing Checker** analyzes suspicious text/links heuristically (IP-based
  URLs, punycode, brand-lookalike domains) — nothing is sent over the network.
  **בודק פישינג** מנתח טקסט/קישורים חשודים - שום דבר לא נשלח החוצה.

## Cleaning up / ניקוי

- **Junk Cleaner** — temp files, browser cache, recycle bin, etc. Everything
  goes through quarantine first.
- **Registry Cleaner** — only flags entries with concrete evidence their
  target no longer exists (not a blind full-registry scan). Creates a `.reg`
  backup automatically before removing anything.
- **Disk Space Analyzer** — visual breakdown of what's using space; drill
  into folders, find your 50 largest files.
- **Duplicate Finder** — compares files by content (SHA1 hash), not just
  name/size, so renamed duplicates are still found.

## Settings / הגדרות

Language, theme, notifications, auto-update checks, quarantine retention
(default 7 days), restore-point creation before risky cleanups, and log file
access — all in one place. Click **"Show advanced settings"** for power-user
options: the default Programs filter, log management, and — if you use
scheduled automatic cleanup — exactly which junk categories it's allowed to
touch and a size safety limit above which it skips deleting and just
notifies you instead.

שפה, ערכת נושא, התראות, בדיקת עדכונים אוטומטית, משך שמירת הסגר (ברירת מחדל 7
ימים), יצירת נקודת שחזור לפני ניקוי מסוכן, וגישה לקובצי לוג - הכל במקום אחד.
לחצו על **"הצג הגדרות מתקדמות"** לאפשרויות למשתמשים מתקדמים: סינון ברירת
המחדל של רשימת התוכנות, ניהול לוגים, ואם משתמשים בניקוי מתוזמן - בדיוק אילו
קטגוריות זבל הוא רשאי לגעת בהן וגבול בטיחות שמעליו הוא ידלג על מחיקה ורק
יתריע.

## Something went wrong? / משהו השתבש?

See the **Troubleshooting** section of `README.md` — covers the "already
running" installer message, crash logs, and quarantine recovery.

ראה את סעיף **פתרון תקלות** ב-`README.md`.
