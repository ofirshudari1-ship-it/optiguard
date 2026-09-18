# OptiGuard — Translation Glossary / מילון מונחים

Keep these consistent across `locales/en.json`, `locales/he.json`,
`Setup.cs`'s `SetupI18n`, and `site/landing-page.html`. When adding a new
string, check here first instead of picking a fresh translation.

| English | עברית | Notes |
|---|---|---|
| Quarantine | הסגר | Not "בידוד" — "הסגר" is the term used everywhere in-app already. |
| Restore point | נקודת שחזור | Windows' own term — matches Windows Settings' Hebrew UI. |
| Leftover(s) | שאריות | For uninstall residue (files/registry). |
| Ghost registry entry | רשומת רישום רפאים | "רפאים" (ghosts) — evidence-based, not "broken"/"שבורה". |
| Health score | ציון בריאות | Not "דירוג" (rating) — "ציון" (score) matches the 0-100 numeric framing. |
| Wizard (guided flow) | אשף | Standard Hebrew Windows term, not "מדריך" (guide/tutorial). |
| Dashboard | Dashboard (kept in English) | Not translated — matches how the nav label reads in the app itself. |
| Settings | הגדרות | |
| Security Center | מרכז אבטחה | |
| Junk Cleaner | מנקה זבל | "זבל" not "פסולת" — matches existing tab name. |
| Deep clean | ניקוי עמוק | For the uninstall mode, not "ניקוי יסודי". |
| Force remove | הסרה כפויה | |
| Onboarding / first-launch wizard | אשף הפעלה-ראשונה | |
| Skip (a wizard/onboarding step) | דלג | Not "עבור הלאה" (that's "Next"). |
| Next (wizard navigation) | הבא | |
| Back (wizard navigation) | הקודם | |
| Update (software update available) | עדכון | |
| Toast / banner notification | הודעה / באנר | "התראה" reserved for Windows-level security alerts (see Privacy tab), not passive UI toasts. |
| Uninstall (the whole app) | הסרת התקנה | Not "מחיקה" (delete) — matches Windows' own "הסרת התקנה" in Apps & Features. |
| Install directory / folder | תיקיית התקנה | |
| Browse (folder picker button) | עיון | |
| Administrator rights | הרשאות מנהל | |
| Backup (registry .reg file) | גיבוי | |
| Evidence-based | מבוסס-ראיות | Core positioning term — appears in SPEC.md, CHANGELOG.md, and the landing page; keep this exact phrasing, not "מבוסס נתונים". |
