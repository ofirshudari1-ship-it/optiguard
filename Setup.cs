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
            _installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), InstallDirName);
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(UninstallKeyPath))
                {
                    if (key != null)
                    {
                        _existingVersion = key.GetValue("DisplayVersion") as string;
                        var existingDir = key.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(existingDir)) _installDir = existingDir;
                        _alreadyInstalled = true;
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
                        if (legacyKey != null) _legacyFound.Add(legacy);
                    }
                }
                catch { }
            }
        }

        private void CleanupLegacyInstall()
        {
            foreach (var legacy in _legacyFound)
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
                    if (!string.IsNullOrEmpty(legacyInstallDir) && Directory.Exists(legacyInstallDir) && !string.Equals(legacyInstallDir, _installDir, StringComparison.OrdinalIgnoreCase))
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
                var result = MessageBox.Show(this,
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
                await System.Threading.Tasks.Task.Run(() =>
                {
                    CleanupLegacyInstall();

                    Directory.CreateDirectory(_installDir);
                    WriteResourceToFile("OptiGuardSetup.OptiGuard.exe", Path.Combine(_installDir, ExeFileName));

                    var localesDir = Path.Combine(_installDir, "locales");
                    Directory.CreateDirectory(localesDir);
                    WriteResourceToFile("OptiGuardSetup.locales.en.json", Path.Combine(localesDir, "en.json"));
                    WriteResourceToFile("OptiGuardSetup.locales.he.json", Path.Combine(localesDir, "he.json"));
                    WriteResourceToFile("OptiGuardSetup.CHANGELOG.md", Path.Combine(_installDir, "CHANGELOG.md"));
                    WriteResourceToFile("OptiGuardSetup.EULA.md", Path.Combine(_installDir, "EULA.md"));

                    string exePath = Path.Combine(_installDir, ExeFileName);

                    if (_chkDesktop.Checked)
                    {
                        CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutFileName), exePath, _installDir);
                    }
                    if (_chkStartMenu.Checked)
                    {
                        string startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
                        Directory.CreateDirectory(startMenuDir);
                        CreateShortcut(Path.Combine(startMenuDir, ShortcutFileName), exePath, _installDir);
                    }
                });
                RegisterUninstall(_installDir, Path.Combine(_installDir, ExeFileName));
                ShowPage(_pageFinish);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Setup error:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowPage(_pageWelcome);
            }
        }

        private static void WriteResourceToFile(string resourceName, string destPath)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null) throw new Exception("Resource not found: " + resourceName);
                using (var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fs);
                }
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
            using (var key = Registry.LocalMachine.CreateSubKey(UninstallKeyPath))
            {
                key.SetValue("DisplayName", SetupForm.AppName);
                key.SetValue("Publisher", "Ofir Shudari");
                key.SetValue("DisplayVersion", SetupForm.AppVersion);
                key.SetValue("InstallLocation", installDir);
                key.SetValue("DisplayIcon", exePath);
                key.SetValue("UninstallString", "\"" + exePath + "\" --self-uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", 1024, RegistryValueKind.DWord);
            }
            SaveAppSettings(_selectedLanguage);
        }

        private void SaveAppSettings(string language)
        {
            try
            {
                var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UninstallerPro");
                Directory.CreateDirectory(appDataPath);
                var settingsPath = Path.Combine(appDataPath, "settings.json");
                var langCode = language == "he" ? "HE" : "EN";
                var settings = @"{""Theme"":""Light"",""Language"":""" + langCode + @""",""ShowSystemComponents"":false,""UpdateManifestUrl"":"""",""CreateRestorePoints"":true,""FirstLaunchCompleted"":false,""EnableNotifications"":true,""AutoCheckUpdates"":false,""QuarantineRetentionDays"":7}";
                File.WriteAllText(settingsPath, settings);
            }
            catch { }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            if (!IsAdmin())
            {
                var psi = new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath) { Verb = "runas" };
                try { System.Diagnostics.Process.Start(psi); } catch { }
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
