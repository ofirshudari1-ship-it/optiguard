using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OptiGuardSetup
{
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(0xF1, 0xF5, 0xF9);
        public static readonly Color HeaderBg = Color.FromArgb(0x0F, 0x2E, 0x27);
        public static readonly Color HeaderText = Color.White;
        public static readonly Color HeaderSub = Color.FromArgb(0x8F, 0xBF, 0xB0);
        public static readonly Color Accent = Color.FromArgb(0x10, 0xB9, 0x81);
        public static readonly Color AccentHover = Color.FromArgb(0x05, 0x96, 0x69);
        public static readonly Color Panel2 = Color.FromArgb(0xE9, 0xEE, 0xF6);
        public static readonly Color Border = Color.FromArgb(0xE2, 0xE8, 0xF0);
        public static readonly Color Text = Color.FromArgb(0x1E, 0x29, 0x3B);
        public static readonly Color TextMuted = Color.FromArgb(0x64, 0x74, 0x8B);
    }

    // Installer text, translated per the language picked on the first screen.
    // Self-contained (not sharing the app's UninstallerPro.I18n - this is a
    // separate assembly/exe) but the same idea: T(key) looks up the current
    // language, falls back to English, then to the raw key.
    //
    // RTL note (updated 2026-09-15): every control on these pages is hand-
    // positioned with Point(x,y), not a flow/anchor layout, so text alignment
    // alone (the previous state here) left control ORDER looking LTR even
    // when the text read in Hebrew. Fixed by turning on SetupForm's
    // RightToLeftLayout in RebuildLocalizedPages() for Hebrew: this is the
    // real WinForms mechanism for this exact scenario - it applies the
    // WS_EX_LAYOUTRTL extended window style to the form's HWND, which
    // Windows then propagates automatically to every native child HWND
    // (Panel/Label/Button/TextBox/CheckBox/ProgressBar all have their own
    // HWND in WinForms), mirroring each control's X position around the
    // window's vertical center without touching a single Point(x,y) value
    // in this file. This is why it is low-risk to the working absolute-
    // position layout: the coordinates below stay exactly as authored, the
    // OS mirrors the rendered result at paint/composition time.
    // One known side effect of this technique, called out here rather than
    // silently assumed fine: WS_EX_LAYOUTRTL also mirrors bitmap content
    // drawn on a mirrored HWND, so the header logo (PictureBox, BuildHeader)
    // will render horizontally flipped in Hebrew mode. OptiGuard's brand
    // mark is a circular/roughly symmetric icon (see assets/BRAND.md), so a
    // horizontal flip should be visually indistinguishable in practice, but
    // this has NOT been confirmed on an actual screen this session - no live
    // Windows GUI was available. Flag for a follow-up visual check.
    static class SetupI18n
    {
        public static string Lang = "en";

        private static readonly Dictionary<string, Dictionary<string, string>> S = new Dictionary<string, Dictionary<string, string>>
        {
            { "en", new Dictionary<string, string>
                {
                    { "install_title", "Install " },
                    { "update_title", "Update " },
                    { "desc_same_version", "The installed version ({0}) is already up to date. You can reinstall anyway to repair files." },
                    { "btn_reinstall", "Reinstall" },
                    { "desc_update", "An existing installation was detected (version {0}). Setup will update it to version {1} in place - no need to uninstall first." },
                    { "btn_update", "Update" },
                    { "desc_legacy", "{0} (version {1}) will be installed. A previous version under the name {2} was found and will be replaced." },
                    { "desc_fresh", "{0} (version {1}) will be installed - an all-in-one PC care app: uninstall programs, remove leftovers, disk space analyzer, duplicate file finder, security & privacy center, registry cleanup, driver health check, and a smart PC health score with guided wizards." },
                    { "btn_install", "Install" },
                    { "lbl_located_at", "Located at:" },
                    { "lbl_will_install_to", "Will install to:" },
                    { "btn_browse", "Browse..." },
                    { "chk_desktop_shortcut", "Create a desktop shortcut" },
                    { "chk_startmenu_shortcut", "Create a Start Menu shortcut" },
                    { "note_admin", "This app requires administrator rights (registry and Program Files access) and will show a UAC prompt each time it starts." },
                    { "btn_cancel", "Cancel" },
                    { "lbl_installing", "Installing..." },
                    { "title_update_complete", "Update Complete" },
                    { "title_install_complete", "Installation Complete" },
                    { "desc_finish", "{0} version {1} is installed and ready to use. You can remove it any time from Windows 'Apps' settings." },
                    { "chk_launch_now", "Launch {0} now" },
                    { "btn_finish", "Finish" },
                    { "app_running_title", "OptiGuard Setup" },
                    { "app_running_msg", "OptiGuard is currently running.\n\nPlease close it before continuing the installation." },
                    { "browse_folder_title", "Choose install folder" },
                    { "footer_copyright", "© 2026 Ofir Shudari - All Rights Reserved" },
                    { "title_error", "Error" },
                    { "msg_setup_failed", "Setup failed:" },
                }
            },
            { "he", new Dictionary<string, string>
                {
                    { "install_title", "התקנת " },
                    { "update_title", "עדכון " },
                    { "desc_same_version", "הגרסה המותקנת ({0}) כבר עדכנית. ניתן להתקין מחדש בכל זאת כדי לתקן קבצים." },
                    { "btn_reinstall", "התקן מחדש" },
                    { "desc_update", "התגלתה התקנה קיימת (גרסה {0}). ההתקנה תעדכן אותה לגרסה {1} במקום - אין צורך להסיר קודם." },
                    { "btn_update", "עדכן" },
                    { "desc_legacy", "{0} (גרסה {1}) תותקן. גרסה קודמת בשם {2} נמצאה ותוחלף." },
                    { "desc_fresh", "{0} (גרסה {1}) תותקן - תוכנת טיפול מקיף במחשב: הסרת תוכנות, שאריות, ניתוח שטח דיסק, איתור כפילויות, מרכז אבטחה ופרטיות, ניקוי רישום, בדיקת דרייברים, וציון בריאות מחשב חכם עם אשפים מונחים." },
                    { "btn_install", "התקן" },
                    { "lbl_located_at", "ממוקם ב:" },
                    { "lbl_will_install_to", "יותקן אל:" },
                    { "btn_browse", "עיון..." },
                    { "chk_desktop_shortcut", "צור קיצור בשולחן העבודה" },
                    { "chk_startmenu_shortcut", "צור קיצור בתפריט Start" },
                    { "note_admin", "תוכנה זו דורשת הרשאות מנהל (גישה לרישום ול-Program Files) ותציג חלון UAC בכל הפעלה." },
                    { "btn_cancel", "ביטול" },
                    { "lbl_installing", "מתקין..." },
                    { "title_update_complete", "העדכון הושלם" },
                    { "title_install_complete", "ההתקנה הושלמה" },
                    { "desc_finish", "{0} גרסה {1} מותקנת ומוכנה לשימוש. ניתן להסיר בכל עת מ'אפליקציות' של Windows." },
                    { "chk_launch_now", "הפעל את {0} עכשיו" },
                    { "btn_finish", "סיום" },
                    { "app_running_title", "התקנת OptiGuard" },
                    { "app_running_msg", "OptiGuard רצה כרגע.\n\nיש לסגור אותה לפני שממשיכים בהתקנה." },
                    { "browse_folder_title", "בחר תיקיית התקנה" },
                    { "footer_copyright", "© 2026 אופיר שודרי - כל הזכויות שמורות" },
                    { "title_error", "שגיאה" },
                    { "msg_setup_failed", "ההתקנה נכשלה:" },
                }
            }
        };

        public static string T(string key)
        {
            Dictionary<string, string> dict;
            if (S.TryGetValue(Lang, out dict) && dict.ContainsKey(key)) return dict[key];
            if (S.TryGetValue("en", out dict) && dict.ContainsKey(key)) return dict[key];
            return key;
        }

        // MessageBox.Show does not pick up the form's RightToLeftLayout - it needs
        // its own MessageBoxOptions to mirror text and buttons in Hebrew. Every
        // user-facing dialog in Setup.cs should go through this helper (instead of
        // calling MessageBox.Show directly) so none of them regress to English-only,
        // LTR-rendered popups.
        public static DialogResult ShowMessageBox(IWin32Window owner, string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            var options = Lang == "he"
                ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
                : (MessageBoxOptions)0;
            return MessageBox.Show(owner, message, title, buttons, icon, MessageBoxDefaultButton.Button1, options);
        }
    }

    static class UiHelpers
    {
        public static void RoundCorners(Control c, int radius)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            var path = new GraphicsPath();
            int d = Math.Min(radius * 2, Math.Min(c.Width, c.Height));
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(c.Width - d, 0, d, d, 270, 90);
            path.AddArc(c.Width - d, c.Height - d, d, d, 0, 90);
            path.AddArc(0, c.Height - d, d, d, 90, 90);
            path.CloseFigure();
            c.Region = new Region(path);
        }

        public static Button MakeButton(string text, Color back, Color fore, int width)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = fore,
                Font = new Font("Segoe UI", 10f),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            var hover = ControlPaint.Dark(back, 0.08f);
            btn.MouseEnter += (s, e) => btn.BackColor = hover;
            btn.MouseLeave += (s, e) => btn.BackColor = back;
            btn.Resize += (s, e) => RoundCorners(btn, 8);
            RoundCorners(btn, 8);
            return btn;
        }

        public static Bitmap MakeLogo(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var brush = new SolidBrush(Theme.Accent))
                    g.FillEllipse(brush, 0, 0, size, size);
            }
            return bmp;
        }

        // מנסה לחלץ את סמל המותג המוטבע ב-exe עצמו (דרך /win32icon בקומפילציה);
        // אם לא נמצא (למשל בסביבת פיתוח), נופל חזרה לסמל הגנרי.
        public static Icon GetBrandIcon()
        {
            try
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                if (icon != null) return icon;
            }
            catch { }
            return Icon.FromHandle(MakeLogo(32).GetHicon());
        }
    }

    public class SetupForm : Form
    {
        public const string AppName = "OptiGuard";

        // Single source of truth is version.json -> injected into <Version> in
        // Setup.csproj at build time -> read back here at runtime (see
        // src/Program.cs for the same pattern in the main app).
        public static readonly string AppVersion =
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        public const string ExeFileName = "OptiGuard.exe";
        public const string ShortcutFileName = "OptiGuard.lnk";
        private const string InstallDirName = "OptiGuard";
        private const string UninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + AppName;

        // שמות קודמים של המוצר (עבר שינויי מיתוג) - מנוקים אוטומטית בהתקנה חדשה
        private static readonly Tuple<string, string>[] LegacyNames = new[]
        {
            Tuple.Create("UninstallerPro", "Uninstaller Pro.lnk"),
            Tuple.Create("PureSys", "PureSys.lnk"),
        };

        private Panel _pageLanguage, _pageWelcome, _pageProgress, _pageFinish;
        private Label _footer;
        private ProgressBar _progressBar;
        private Label _progressLabel;
        private CheckBox _chkDesktop, _chkStartMenu, _chkLaunch;
        private string _installDir;
        private bool _alreadyInstalled;
        private string _existingVersion;
        private List<Tuple<string, string>> _legacyFound = new List<Tuple<string, string>>();
        private string _selectedLanguage = "en";

        public SetupForm()
        {
            DetectExistingInstall();

            Text = (_alreadyInstalled ? SetupI18n.T("update_title") : SetupI18n.T("install_title")) + AppName;
            ClientSize = new Size(560, 460);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            Icon = UiHelpers.GetBrandIcon();
            Font = new Font("Segoe UI", 9.5f);

            var header = BuildHeader();
            Controls.Add(header);

            _footer = new Label { Text = SetupI18n.T("footer_copyright"), ForeColor = Theme.TextMuted, Dock = DockStyle.Bottom, TextAlign = ContentAlignment.MiddleCenter, Height = 22, Font = new Font("Segoe UI", 8f) };
            Controls.Add(_footer);

            _pageLanguage = BuildLanguagePage();
            Controls.Add(_pageLanguage);
            RebuildLocalizedPages();
            ShowPage(_pageLanguage);
        }

        // (Re)builds the pages whose text depends on the selected language, and
        // swaps them into Controls. Called once up front (English default) and
        // again after the user picks a language, since Welcome/Progress/Finish
        // are built once with strings baked in, not re-rendered live.
        private void RebuildLocalizedPages()
        {
            SetupI18n.Lang = _selectedLanguage;
            RightToLeft = _selectedLanguage == "he" ? RightToLeft.Yes : RightToLeft.No;
            // Pixel-level mirroring for the absolute Point(x,y) layout below - see the
            // RTL note on SetupI18n above for why this is the correct/low-risk fix.
            RightToLeftLayout = _selectedLanguage == "he";
            Text = (_alreadyInstalled ? SetupI18n.T("update_title") : SetupI18n.T("install_title")) + AppName;
            if (_footer != null) _footer.Text = SetupI18n.T("footer_copyright");

            if (_pageWelcome != null) Controls.Remove(_pageWelcome);
            if (_pageProgress != null) Controls.Remove(_pageProgress);
            if (_pageFinish != null) Controls.Remove(_pageFinish);

            _pageWelcome = BuildWelcomePage();
            _pageProgress = BuildProgressPage();
            _pageFinish = BuildFinishPage();
            Controls.Add(_pageWelcome);
            Controls.Add(_pageProgress);
            Controls.Add(_pageFinish);
            _pageWelcome.Visible = false;
            _pageProgress.Visible = false;
            _pageFinish.Visible = false;
        }

        private void DetectExistingInstall()
        {
            List<Tuple<string, string>> legacyFound;
            DetectExistingInstallStatic(out _installDir, out _existingVersion, out _alreadyInstalled, out legacyFound);
            _legacyFound = legacyFound;
        }

        // Static core shared by the interactive wizard (constructor, above) and
        // the unattended /SILENT path (RunSilentInstall, below) - neither one
        // needs a live Form instance to figure out where OptiGuard already
        // lives.
        private static void DetectExistingInstallStatic(out string installDir, out string existingVersion, out bool alreadyInstalled, out List<Tuple<string, string>> legacyFound)
        {
            installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), InstallDirName);
            existingVersion = null;
            alreadyInstalled = false;
            legacyFound = new List<Tuple<string, string>>();
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(UninstallKeyPath))
                {
                    if (key != null)
                    {
                        existingVersion = key.GetValue("DisplayVersion") as string;
                        var existingDir = key.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(existingDir)) installDir = existingDir;
                        alreadyInstalled = true;
                    }
                }
            }
            catch { }

            foreach (var legacy in LegacyNames)
            {
                try
                {
                    var legacyKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + legacy.Item1;
                    using (var legacyKey = Registry.LocalMachine.OpenSubKey(legacyKeyPath))
                    {
                        if (legacyKey != null) legacyFound.Add(legacy);
                    }
                }
                catch { }
            }
        }

        private void CleanupLegacyInstall()
        {
            CleanupLegacyInstallStatic(_legacyFound, _installDir);
        }

        private static void CleanupLegacyInstallStatic(List<Tuple<string, string>> legacyFound, string installDir)
        {
            foreach (var legacy in legacyFound)
            {
                try
                {
                    var legacyKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + legacy.Item1;
                    string legacyInstallDir = null;
                    using (var legacyKey = Registry.LocalMachine.OpenSubKey(legacyKeyPath))
                    {
                        if (legacyKey != null) legacyInstallDir = legacyKey.GetValue("InstallLocation") as string;
                    }

                    var desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), legacy.Item2);
                    if (File.Exists(desktop)) File.Delete(desktop);
                    var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), legacy.Item2);
                    if (File.Exists(startMenu)) File.Delete(startMenu);
                    Registry.LocalMachine.DeleteSubKeyTree(legacyKeyPath, false);
                    if (!string.IsNullOrEmpty(legacyInstallDir) && Directory.Exists(legacyInstallDir) && !string.Equals(legacyInstallDir, installDir, StringComparison.OrdinalIgnoreCase))
                    {
                        try { Directory.Delete(legacyInstallDir, true); } catch { }
                    }
                }
                catch { }
            }
        }

        private Control BuildHeader()
        {
            var panel = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = Theme.HeaderBg };
            var logo = new PictureBox { Image = UiHelpers.GetBrandIcon().ToBitmap(), Size = new Size(40, 40), Location = new Point(496, 17), SizeMode = PictureBoxSizeMode.Zoom };
            var title = new Label { Text = AppName, ForeColor = Theme.HeaderText, Font = new Font("Segoe UI", 15f, FontStyle.Bold), AutoSize = true, Location = new Point(20, 14) };
            var sub = new Label { Text = "All-In-One PC Care - Setup", ForeColor = Theme.HeaderSub, Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(20, 44) };
            panel.Controls.Add(logo);
            panel.Controls.Add(title);
            panel.Controls.Add(sub);
            return panel;
        }

        private Panel BuildLanguagePage()
        {
            var p = new Panel { Location = new Point(0, 74), Size = new Size(560, 460 - 74 - 22), BackColor = Theme.Bg };

            var title = new Label { Text = "Select Language", Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = Theme.Text, Location = new Point(24, 30), AutoSize = true };
            var desc = new Label { Text = "Choose your preferred language for the installer and application.", Location = new Point(24, 66), Size = new Size(510, 30), ForeColor = Theme.TextMuted };
            p.Controls.Add(title);
            p.Controls.Add(desc);

            var btnEnglish = UiHelpers.MakeButton("🇺🇸 English", Theme.Accent, Color.White, 150);
            btnEnglish.Location = new Point(24, 120);
            btnEnglish.Click += (s, e) => { _selectedLanguage = "en"; RebuildLocalizedPages(); ShowPage(_pageWelcome); };
            p.Controls.Add(btnEnglish);

            var btnHebrew = UiHelpers.MakeButton("🇮🇱 עברית", Theme.Accent, Color.White, 150);
            btnHebrew.Location = new Point(24, 170);
            btnHebrew.Click += (s, e) => { _selectedLanguage = "he"; RebuildLocalizedPages(); ShowPage(_pageWelcome); };
            p.Controls.Add(btnHebrew);

            return p;
        }

        private Panel BuildWelcomePage()
        {
            var p = new Panel { Location = new Point(0, 74), Size = new Size(560, 460 - 74 - 22), BackColor = Theme.Bg };
            var rtl = _selectedLanguage == "he";
            var textAlign = rtl ? HorizontalAlignment.Right : HorizontalAlignment.Left;

            string descText;
            string buttonText;
            bool sameVersion = _alreadyInstalled && _existingVersion == AppVersion;
            if (sameVersion)
            {
                descText = string.Format(SetupI18n.T("desc_same_version"), _existingVersion);
                buttonText = SetupI18n.T("btn_reinstall");
            }
            else if (_alreadyInstalled)
            {
                descText = string.Format(SetupI18n.T("desc_update"), _existingVersion ?? "?", AppVersion);
                buttonText = SetupI18n.T("btn_update");
            }
            else if (_legacyFound.Count > 0)
            {
                var oldNames = string.Join(", ", _legacyFound.Select(l => "'" + l.Item1 + "'"));
                descText = string.Format(SetupI18n.T("desc_legacy"), AppName, AppVersion, oldNames);
                buttonText = SetupI18n.T("btn_install");
            }
            else
            {
                descText = string.Format(SetupI18n.T("desc_fresh"), AppName, AppVersion);
                buttonText = SetupI18n.T("btn_install");
            }

            var desc = new Label { Text = descText, Location = new Point(24, 20), Size = new Size(510, 60), ForeColor = Theme.Text, TextAlign = rtl ? ContentAlignment.TopRight : ContentAlignment.TopLeft };
            p.Controls.Add(desc);

            var lblPath = new Label { Text = (_alreadyInstalled ? SetupI18n.T("lbl_located_at") : SetupI18n.T("lbl_will_install_to")), Location = new Point(24, 88), AutoSize = true, ForeColor = Theme.Text };
            p.Controls.Add(lblPath);
            var txtPath = new TextBox { Text = _installDir, Location = new Point(24, 111), Width = 420, ReadOnly = true, BackColor = Theme.Panel2, BorderStyle = BorderStyle.FixedSingle };
            p.Controls.Add(txtPath);
            if (!_alreadyInstalled)
            {
                var btnBrowse = UiHelpers.MakeButton(SetupI18n.T("btn_browse"), Theme.Panel2, Theme.Text, 90);
                btnBrowse.Location = new Point(444, 109);
                btnBrowse.Height = 26;
                btnBrowse.Click += (s, e) =>
                {
                    using (var dlg = new FolderBrowserDialog { Description = SetupI18n.T("browse_folder_title"), SelectedPath = _installDir })
                    {
                        if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
                        {
                            _installDir = Path.Combine(dlg.SelectedPath, InstallDirName);
                            txtPath.Text = _installDir;
                        }
                    }
                };
                p.Controls.Add(btnBrowse);
            }

            _chkDesktop = new CheckBox { Text = SetupI18n.T("chk_desktop_shortcut"), Checked = true, Location = new Point(24, 153), AutoSize = true, ForeColor = Theme.Text };
            _chkStartMenu = new CheckBox { Text = SetupI18n.T("chk_startmenu_shortcut"), Checked = true, Location = new Point(24, 181), AutoSize = true, ForeColor = Theme.Text };
            p.Controls.Add(_chkDesktop);
            p.Controls.Add(_chkStartMenu);

            var note = new Label
            {
                Text = SetupI18n.T("note_admin"),
                Location = new Point(24, 217), Size = new Size(510, 36), ForeColor = Theme.TextMuted, TextAlign = rtl ? ContentAlignment.TopRight : ContentAlignment.TopLeft
            };
            p.Controls.Add(note);

            var btnInstall = UiHelpers.MakeButton(buttonText, Theme.Accent, Color.White, 120);
            btnInstall.Location = new Point(24, 273);
            btnInstall.Click += async (s, e) => await DoInstall();
            p.Controls.Add(btnInstall);

            var btnCancel = UiHelpers.MakeButton(SetupI18n.T("btn_cancel"), Theme.Panel2, Theme.Text, 100);
            btnCancel.Location = new Point(154, 273);
            btnCancel.Click += (s, e) => Close();
            p.Controls.Add(btnCancel);

            return p;
        }

        private Panel BuildProgressPage()
        {
            var p = new Panel { Location = new Point(0, 74), Size = new Size(560, 460 - 74 - 22), BackColor = Theme.Bg };
            _progressLabel = new Label { Text = SetupI18n.T("lbl_installing"), Location = new Point(24, 120), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI", 10f) };
            _progressBar = new ProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Location = new Point(24, 150), Size = new Size(510, 22) };
            p.Controls.Add(_progressLabel);
            p.Controls.Add(_progressBar);
            return p;
        }

        private Panel BuildFinishPage()
        {
            var p = new Panel { Location = new Point(0, 74), Size = new Size(560, 460 - 74 - 22), BackColor = Theme.Bg };
            var rtl = _selectedLanguage == "he";
            var title = new Label { Text = _alreadyInstalled ? SetupI18n.T("title_update_complete") : SetupI18n.T("title_install_complete"), Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = Theme.Text, Location = new Point(24, 30), AutoSize = true };
            var desc = new Label { Text = string.Format(SetupI18n.T("desc_finish"), AppName, AppVersion), Location = new Point(24, 66), Size = new Size(510, 40), ForeColor = Theme.TextMuted, TextAlign = rtl ? ContentAlignment.TopRight : ContentAlignment.TopLeft };
            _chkLaunch = new CheckBox { Text = string.Format(SetupI18n.T("chk_launch_now"), AppName), Checked = true, Location = new Point(24, 116), AutoSize = true, ForeColor = Theme.Text };
            p.Controls.Add(title);
            p.Controls.Add(desc);
            p.Controls.Add(_chkLaunch);

            var btnFinish = UiHelpers.MakeButton(SetupI18n.T("btn_finish"), Theme.Accent, Color.White, 120);
            btnFinish.Location = new Point(24, 170);
            btnFinish.Click += (s, e) =>
            {
                if (_chkLaunch.Checked)
                {
                    try { Process.Start(Path.Combine(_installDir, ExeFileName)); } catch { }
                }
                Close();
            };
            p.Controls.Add(btnFinish);
            return p;
        }

        private void ShowPage(Panel page)
        {
            _pageLanguage.Visible = page == _pageLanguage;
            _pageWelcome.Visible = page == _pageWelcome;
            _pageProgress.Visible = page == _pageProgress;
            _pageFinish.Visible = page == _pageFinish;
        }

        /// <summary>
        /// Blocks (with a retry/cancel prompt) until OptiGuard isn't running, or the
        /// user cancels. Prevents the install from failing partway through because the
        /// running exe has the file locked - same pattern as SnapAI's installer.
        /// </summary>
        private bool EnsureAppNotRunning()
        {
            while (System.Diagnostics.Process.GetProcessesByName("OptiGuard").Length > 0)
            {
                var result = SetupI18n.ShowMessageBox(this,
                    SetupI18n.T("app_running_msg"),
                    SetupI18n.T("app_running_title"), MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);
                if (result != DialogResult.Retry)
                    return false;
            }
            return true;
        }

        private async System.Threading.Tasks.Task DoInstall()
        {
            if (!EnsureAppNotRunning())
                return;

            ShowPage(_pageProgress);
            try
            {
                string error = null;
                bool ok = false;
                await System.Threading.Tasks.Task.Run(() =>
                {
                    ok = PerformInstallCore(_installDir, _chkDesktop.Checked, _chkStartMenu.Checked, _legacyFound, out error);
                });
                if (!ok) throw new Exception(error ?? "unknown error");

                RegisterUninstallStatic(_installDir, Path.Combine(_installDir, ExeFileName), AppVersion, _selectedLanguage);
                ShowPage(_pageFinish);
            }
            catch (Exception ex)
            {
                // Lead-in line is translated (see title_error/msg_setup_failed in
                // SetupI18n); ex.Message itself is a raw .NET exception message and
                // stays as-is - there's no reliable way to translate arbitrary
                // exception text. ShowMessageBox mirrors text and buttons when
                // Hebrew is selected, matching RightToLeftLayout used for the rest
                // of the wizard (see the RTL note on SetupI18n).
                SetupI18n.ShowMessageBox(this, SetupI18n.T("msg_setup_failed") + "\n" + ex.Message, SetupI18n.T("title_error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowPage(_pageWelcome);
            }
        }

        // Shared install core - copies the payload and (optionally) creates
        // shortcuts. No UI, no Form dependency, so it works from both the
        // wizard (via Task.Run above) and the unattended /SILENT path
        // (RunSilentInstall, below).
        private static bool PerformInstallCore(string installDir, bool createDesktopShortcut, bool createStartMenuShortcut, List<Tuple<string, string>> legacyFound, out string error)
        {
            error = null;
            try
            {
                CleanupLegacyInstallStatic(legacyFound, installDir);

                Directory.CreateDirectory(installDir);
                WriteResourceToFile("OptiGuardSetup.OptiGuard.exe", Path.Combine(installDir, ExeFileName));

                var localesDir = Path.Combine(installDir, "locales");
                Directory.CreateDirectory(localesDir);
                WriteResourceToFile("OptiGuardSetup.locales.en.json", Path.Combine(localesDir, "en.json"));
                WriteResourceToFile("OptiGuardSetup.locales.he.json", Path.Combine(localesDir, "he.json"));
                WriteResourceToFile("OptiGuardSetup.CHANGELOG.md", Path.Combine(installDir, "CHANGELOG.md"));
                WriteResourceToFile("OptiGuardSetup.EULA.md", Path.Combine(installDir, "EULA.md"));

                string exePath = Path.Combine(installDir, ExeFileName);

                if (createDesktopShortcut)
                {
                    CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutFileName), exePath, installDir);
                }
                if (createStartMenuShortcut)
                {
                    string startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                    Directory.CreateDirectory(startMenuDir);
                    CreateShortcut(Path.Combine(startMenuDir, ShortcutFileName), exePath, installDir);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // --- Unattended install path (/SILENT, /VERYSILENT) -----------------
        //
        // Runs the exact same file-copy/registration core as the interactive
        // wizard, with zero dialogs: no language/welcome/progress/finish
        // pages, no MessageBox. Used both when a user runs
        // "OptiGuard-Setup-X.Y.Z.exe /SILENT" by hand and when the running app
        // downloads a new installer and launches it for a self-update (see
        // UpdateChecker.DownloadAndLaunchSilentInstall). The one thing this
        // cannot avoid is the UAC elevation prompt itself when not already
        // elevated (Program.Main re-launches with "runas") - that is an
        // OS-level prompt, not part of Setup's own UI, and there is no way to
        // suppress it from user code short of running as SYSTEM.
        //
        // Preserves user data: does not touch the app's settings.json (see
        // SaveAppSettings) and, on an in-place update, leaves whatever
        // shortcuts already exist untouched rather than recreating them.
        public static int RunSilentInstall(bool relaunch)
        {
            try
            {
                string installDir, existingVersion;
                bool alreadyInstalled;
                List<Tuple<string, string>> legacyFound;
                DetectExistingInstallStatic(out installDir, out existingVersion, out alreadyInstalled, out legacyFound);

                // Best-effort, non-interactive: ask the running app to close, then
                // force it if it doesn't, so the file copy below isn't blocked by a
                // file lock. Never shows the retry/cancel prompt EnsureAppNotRunning
                // uses in the interactive wizard.
                TryCloseRunningAppSilently();

                string error;
                // Only create shortcuts for a brand-new install; an in-place
                // update leaves the user's existing shortcuts (or lack thereof)
                // alone instead of re-creating something they may have removed.
                bool ok = PerformInstallCore(installDir, !alreadyInstalled, !alreadyInstalled, legacyFound, out error);
                if (!ok)
                {
                    LogSilentError("Silent install failed: " + error);
                    return 1;
                }

                RegisterUninstallStatic(installDir, Path.Combine(installDir, ExeFileName), AppVersion, null);

                // Self-update path (OptiGuard launched us with /SILENT
                // /RELAUNCH and then closed itself): bring the freshly updated
                // app back up so the update doesn't look like the app just
                // crashed and disappeared. Plain /SILENT (scripted installs)
                // does not start anything.
                if (relaunch)
                {
                    try { Process.Start(new ProcessStartInfo(Path.Combine(installDir, ExeFileName)) { UseShellExecute = true, WorkingDirectory = installDir }); }
                    catch (Exception ex) { LogSilentError("Relaunch after silent install failed: " + ex.Message); }
                }
                return 0;
            }
            catch (Exception ex)
            {
                LogSilentError("Silent install crashed: " + ex.Message);
                return 1;
            }
        }

        private static void TryCloseRunningAppSilently()
        {
            try
            {
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("OptiGuard"))
                {
                    try
                    {
                        proc.CloseMainWindow();
                        if (!proc.WaitForExit(5000))
                        {
                            proc.Kill();
                            proc.WaitForExit(3000);
                        }
                    }
                    catch { }
                    finally { proc.Dispose(); }
                }
            }
            catch { }
        }

        private static void LogSilentError(string message)
        {
            try
            {
                var logPath = Path.Combine(Path.GetTempPath(), "OptiGuard-SilentInstall.log");
                File.AppendAllText(logPath, DateTime.Now.ToString("u") + "  " + message + Environment.NewLine);
            }
            catch { }
        }

        // Writes to "<dest>.new" first and only swaps it into place once the
        // whole payload is on disk. Previously the existing OptiGuard.exe was
        // opened with FileMode.Create (truncated to 0 bytes) and streamed over
        // in place, so an update interrupted mid-copy (disk full, power loss,
        // AV lock) left a broken, half-written exe and no working OptiGuard at
        // all. Now a failed update leaves the previous version intact.
        private static void WriteResourceToFile(string resourceName, string destPath)
        {
            var asm = Assembly.GetExecutingAssembly();
            var tempPath = destPath + ".new";
            try
            {
                using (var stream = asm.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) throw new Exception("Resource not found: " + resourceName);
                    using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fs);
                        fs.Flush(true);
                    }
                }

                if (File.Exists(destPath))
                {
                    try
                    {
                        File.Replace(tempPath, destPath, null);
                    }
                    catch (IOException)
                    {
                        // File.Replace isn't supported on every file system /
                        // redirected folder - fall back to delete + move (still
                        // only after the complete new file exists).
                        File.Delete(destPath);
                        File.Move(tempPath, destPath);
                    }
                }
                else
                {
                    File.Move(tempPath, destPath);
                }
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetExe, string workingDir)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            Type scType = shortcut.GetType();
            scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetExe });
            scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
            scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { targetExe + ",0" });
            scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { SetupForm.AppName + " - All-In-One PC Care" });
            scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }

        private void RegisterUninstall(string installDir, string exePath)
        {
            RegisterUninstallStatic(installDir, exePath, AppVersion, _selectedLanguage);
        }

        // language == null means "unattended update, unknown selection" -
        // SaveAppSettings below only uses it for a brand-new settings.json,
        // and never overwrites one that already exists, so this is safe for
        // both interactive and silent callers.
        private static void RegisterUninstallStatic(string installDir, string exePath, string version, string language)
        {
            using (var key = Registry.LocalMachine.CreateSubKey(UninstallKeyPath))
            {
                key.SetValue("DisplayName", SetupForm.AppName);
                key.SetValue("Publisher", "Ofir Shudari");
                key.SetValue("DisplayVersion", version);
                key.SetValue("InstallLocation", installDir);
                key.SetValue("DisplayIcon", exePath);
                key.SetValue("UninstallString", "\"" + exePath + "\" --self-uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", 1024, RegistryValueKind.DWord);
            }
            SaveAppSettings(language);
        }

        // Only ever creates settings.json when one doesn't exist yet (fresh
        // install). An update - interactive or silent - must never clobber the
        // user's saved Theme/Language/notification/etc. preferences, so an
        // existing file is left completely untouched here.
        private static void SaveAppSettings(string language)
        {
            try
            {
                var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UninstallerPro");
                Directory.CreateDirectory(appDataPath);
                var settingsPath = Path.Combine(appDataPath, "settings.json");
                if (File.Exists(settingsPath)) return;
                // Lowercase "he"/"en" - the exact codes the app's I18n uses.
                // This used to write "HE"/"EN", which never matched I18n.Hebrew
                // ("he"), so picking Hebrew in the installer was silently lost
                // and the app (and its first-run onboarding) opened in English.
                // Only the installer's own choice (language) is seeded here;
                // everything else - notably AutoCheckUpdates, which this used
                // to force to false against the app's opt-out default of true,
                // and Theme, which the app now picks from the Windows
                // light/dark app mode - is left to the app's own defaults.
                var langCode = language == "he" ? "he" : "en";
                var settings = @"{""Language"":""" + langCode + @""",""FirstLaunchCompleted"":false}";
                File.WriteAllText(settingsPath, settings);
            }
            catch { }
        }
    }

    static class Program
    {
        // /SILENT and /VERYSILENT are treated identically: OptiGuard's setup
        // only ever has one screen's worth of choices (shortcuts), so there is
        // no separate "silent but still show a progress bar" tier to offer -
        // both switches mean "install with zero UI and exit with a code".
        private static bool IsSilentArg(string arg)
        {
            return string.Equals(arg, "/SILENT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "/VERYSILENT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "-SILENT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "-VERYSILENT", StringComparison.OrdinalIgnoreCase);
        }

        [STAThread]
        static void Main()
        {
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            bool silent = args.Any(IsSilentArg);

            if (!IsAdmin())
            {
                // Must still elevate - OptiGuard installs into Program Files and
                // writes HKLM - but nothing beyond the single unavoidable UAC
                // prompt should require interaction in silent mode. Passing the
                // /SILENT switch through to the elevated relaunch is what makes
                // that relaunch skip its own wizard UI in turn.
                var psi = new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath) { Verb = "runas" };
                psi.Arguments = string.Join(" ", args.Select(a => "\"" + a.Replace("\"", "\\\"") + "\""));
                try
                {
                    var proc = System.Diagnostics.Process.Start(psi);
                    if (silent && proc != null)
                    {
                        proc.WaitForExit();
                        Environment.ExitCode = proc.ExitCode;
                    }
                }
                catch
                {
                    // User declined the UAC prompt, or elevation otherwise
                    // failed - there is nothing else silent mode can do here.
                    if (silent) Environment.ExitCode = 1;
                }
                return;
            }

            if (silent)
            {
                bool relaunch = args.Any(a => string.Equals(a, "/RELAUNCH", StringComparison.OrdinalIgnoreCase)
                                           || string.Equals(a, "-RELAUNCH", StringComparison.OrdinalIgnoreCase));
                Environment.ExitCode = SetupForm.RunSilentInstall(relaunch);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }

        static bool IsAdmin()
        {
            var id = WindowsIdentity.GetCurrent();
            var p = new WindowsPrincipal(id);
            return p.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
