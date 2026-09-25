using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace UninstallerPro
{
    public class KeyLabel
    {
        public string Key { get; set; }
        public string Label { get; set; }
    }

    public class UpdatableRow : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isChecked;
        public bool IsChecked { get { return _isChecked; } set { _isChecked = value; if (PropertyChanged != null) PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs("IsChecked")); } }
        public UpdatableProgram Program { get; set; }
        public string Name { get { return Program.Name; } }
        public string CurrentVersion { get { return Program.CurrentVersion; } }
        public string AvailableVersion { get { return Program.AvailableVersion; } }
        public string Source { get { return Program.Source; } }
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    public class MainWindow : Window
    {
        private AppSettings _settings;
        // Exposed so Program.cs can open the desktop widget (WidgetWindow.cs)
        // against the same settings instance the main window already loaded,
        // instead of re-reading settings.json a second time.
        public AppSettings Settings { get { return _settings; } }

        // Guards the background update check so it runs at most once per
        // process lifetime, even if the main window were ever rebuilt.
        // Static (not instance) on purpose - there is only ever one process.
        private static bool _updateCheckedThisSession = false;

        // Programs tab state
        private List<InstalledProgram> _allPrograms = new List<InstalledProgram>();
        private ListCollectionView _programsView;
        private DataGrid _gridPrograms;
        private CheckBox _chkShowSystem;
        private TextBox _txtSearch;
        private ComboBox _cmbCategory;

        // Games tab
        private List<GameInfo> _games = new List<GameInfo>();
        private ListCollectionView _gamesView;
        private DataGrid _gridGames;
        private ComboBox _cmbGamePlatform;

        // Extensions tab
        private List<BrowserExtension> _extensions = new List<BrowserExtension>();
        private ListCollectionView _extensionsView;
        private DataGrid _gridExt;
        private ComboBox _cmbExtBrowser;

        // Startup tab
        private List<StartupItem> _startupItems = new List<StartupItem>();
        private ListCollectionView _startupView;
        private DataGrid _gridStartup;
        private ComboBox _cmbStartupStatus;

        // Junk cleaner tab
        private List<JunkCategory> _junkCategories = new List<JunkCategory>();
        private ListBox _junkList;
        private TextBlock _junkStatusLabel;

        // Disk health tab
        private DataGrid _gridDisks;

        // Registry cleaner tab
        private List<GhostRegistryEntry> _ghostEntries = new List<GhostRegistryEntry>();
        private DataGrid _gridRegClean;

        // Disk space analyzer tab
        private List<SpaceEntry> _spaceEntries = new List<SpaceEntry>();
        private DataGrid _gridSpace;
        private ComboBox _cmbSpaceDrive;
        private TextBlock _spacePathLabel;
        private string _spaceCurrentPath;

        // Duplicate file finder tab
        private List<DuplicateGroup> _dupGroups = new List<DuplicateGroup>();
        private StackPanel _dupResultsPanel;
        private TextBox _txtDupFolder;

        private class NavEntry
        {
            public string Key;
            public string GroupKey;
            public string LabelKey;
            public Func<UIElement> Builder;
            public Action OnFirstShow;
            public Action OnEveryShow;
            public UIElement Content;
            public bool Built;
        }

        private static readonly Dictionary<string, string> NavIcons = new Dictionary<string, string>
        {
            { "dashboard", "🏠" }, { "timeline", "🕒" }, { "programs", "📦" }, { "games", "🎮" }, { "ext", "🧩" },
            { "startup", "🚀" }, { "junk", "🧹" }, { "disk", "💽" }, { "diskspace", "📊" }, { "duplicates", "🪞" }, { "fixes", "🔧" }, { "swupdates", "⬆" },
            { "sysinfo", "🖥" }, { "seccenter", "🛡" }, { "privacy", "🔏" },
            { "regclean", "🗂" }, { "phishing", "🎣" }, { "settings", "⚙" },
        };

        private List<NavEntry> _navEntries;
        private Dictionary<string, Border> _navButtons = new Dictionary<string, Border>();
        private Dictionary<string, TextBlock> _navLabels = new Dictionary<string, TextBlock>();
        private string _currentNavKey;
        private ContentControl _contentHost;
        private Action _refreshDashboard;
        private Action _refreshHealthScore;
        private Action _refreshTimeline;
        private DataGrid _gridTimeline;

        public MainWindow()
        {
            try { AppPaths.EnsureDataDir(); } catch { }
            _settings = AppSettings.Load();
            // "Follow Windows theme" (Settings > Appearance) re-checks the live
            // system theme on every launch, instead of the old behavior where
            // Theme was only ever seeded from the system once (first run /
            // onboarding) and then frozen forever after.
            if (_settings.FollowSystemTheme) _settings.Theme = UninstallerPro.Theme.DetectSystemTheme();
            Theme.Load(_settings.Theme);
            I18n.CurrentLang = _settings.Language;
            if (!_settings.FirstLaunchCompleted)
            {
                var onboarding = new OnboardingWindow();
                onboarding.ShowDialog();
                _settings.Language = onboarding.ResultLanguage;
                _settings.Theme = onboarding.ResultTheme;
                I18n.CurrentLang = _settings.Language;
                Theme.Load(_settings.Theme);
                _settings.FirstLaunchCompleted = true;
                _settings.Save();
            }
            RestorePoint.Enabled = _settings.CreateRestorePoints;
            // רץ ברקע ולא בבנאי עצמו - עם מניפסט הסגר גדול (שימוש ממושך) זה
            // יכול לקחת כמה שניות, ולא צריך לעכב את פתיחת החלון בשביל זה.
            var retentionDays = _settings.QuarantineRetentionDays > 0 ? _settings.QuarantineRetentionDays : 7;
            var notifyEnabled = _settings.EnableNotifications;
            var uiDispatcher = Dispatcher;
            Task.Run(() =>
            {
                int purgedCount = 0;
                try { purgedCount = Quarantine.PurgeOlderThan(retentionDays); } catch { }
                try { Quarantine.PurgeOrphans(); } catch { }
                try { Logger.PurgeOldLogs(30); } catch { }
                try { RegistryCleanerData.PurgeOldBackups(30); } catch { }

                if (notifyEnabled && purgedCount > 0)
                {
                    uiDispatcher.Invoke(() => Toast.Show(string.Format(I18n.T("toast_quarantine_purged"), purgedCount)));
                }

                // If the scheduled auto-clean task ran unattended since we last
                // opened the app, surface a one-time Toast about it now - same
                // "never do things silently" principle as the purge notice above.
                var pending = ScheduledCleanupData.TakePendingNotification();
                if (notifyEnabled && pending != null)
                {
                    if (pending.SkippedThreshold)
                    {
                        var foundText = JunkCleanerData.FormatSize(pending.FoundBytes);
                        uiDispatcher.Invoke(() => Toast.Show(string.Format(I18n.T("toast_auto_clean_skipped"), foundText, pending.RanAt.ToString("dd/MM/yyyy HH:mm"))));
                    }
                    else
                    {
                        var freedText = JunkCleanerData.FormatSize(pending.FreedBytes);
                        uiDispatcher.Invoke(() => Toast.Show(string.Format(I18n.T("toast_auto_clean_ran"), freedText, pending.RanAt.ToString("dd/MM/yyyy HH:mm"))));
                    }
                }
            });

            Title = I18n.T("app_name") + " - " + I18n.T("app_tagline");
            MinWidth = 960;
            MinHeight = 600;
            Background = Theme.Get("BgBrush");
            RestoreWindowState();
            Closing += (s, e) => SaveWindowState();
            FlowDirection = I18n.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            FontFamily = new FontFamily("Segoe UI");
            try
            {
                var exeIcon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(exeIcon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            }
            catch { }

            _navEntries = new List<NavEntry>
            {
                new NavEntry { Key = "dashboard", GroupKey = "nav_group_overview", LabelKey = "tab_dashboard", Builder = BuildDashboardTab, OnEveryShow = () => { if (_refreshDashboard != null) _refreshDashboard(); } },
                new NavEntry { Key = "timeline", GroupKey = "nav_group_overview", LabelKey = "timeline_title", Builder = BuildTimelineTab, OnEveryShow = () => { if (_refreshTimeline != null) _refreshTimeline(); } },
                new NavEntry { Key = "programs", GroupKey = "nav_group_uninstall", LabelKey = "tab_programs", Builder = BuildProgramsTab, OnFirstShow = RefreshPrograms },
                new NavEntry { Key = "games", GroupKey = "nav_group_uninstall", LabelKey = "tab_games", Builder = BuildGamesTab, OnFirstShow = RefreshGames },
                new NavEntry { Key = "ext", GroupKey = "nav_group_uninstall", LabelKey = "tab_extensions", Builder = BuildExtensionsTab, OnFirstShow = RefreshExtensions },
                new NavEntry { Key = "startup", GroupKey = "nav_group_maintenance", LabelKey = "tab_startup", Builder = BuildStartupTab, OnFirstShow = RefreshStartup },
                new NavEntry { Key = "junk", GroupKey = "nav_group_maintenance", LabelKey = "tab_junk_cleaner", Builder = BuildJunkCleanerTab },
                new NavEntry { Key = "disk", GroupKey = "nav_group_maintenance", LabelKey = "tab_disk_health", Builder = BuildDiskHealthTab },
                new NavEntry { Key = "diskspace", GroupKey = "nav_group_maintenance", LabelKey = "tab_disk_space_analyzer", Builder = BuildDiskSpaceAnalyzerTab },
                new NavEntry { Key = "duplicates", GroupKey = "nav_group_maintenance", LabelKey = "tab_duplicate_finder", Builder = BuildDuplicateFinderTab },
                new NavEntry { Key = "fixes", GroupKey = "nav_group_maintenance", LabelKey = "tab_quick_fixes", Builder = BuildQuickFixesTab },
                new NavEntry { Key = "swupdates", GroupKey = "nav_group_updates", LabelKey = "tab_software_updates", Builder = BuildSoftwareUpdatesTab },
                new NavEntry { Key = "sysinfo", GroupKey = "nav_group_system", LabelKey = "tab_system_info", Builder = BuildSystemInfoTab },
                new NavEntry { Key = "seccenter", GroupKey = "nav_group_security", LabelKey = "tab_security_center", Builder = BuildSecurityCenterTab },
                new NavEntry { Key = "privacy", GroupKey = "nav_group_security", LabelKey = "tab_privacy", Builder = BuildPrivacyTab },
                new NavEntry { Key = "regclean", GroupKey = "nav_group_security", LabelKey = "tab_registry_cleaner", Builder = BuildRegistryCleanerTab },
                new NavEntry { Key = "phishing", GroupKey = "nav_group_security", LabelKey = "tab_phishing_checker", Builder = BuildPhishingCheckerTab },
                new NavEntry { Key = "settings", GroupKey = null, LabelKey = "tab_settings", Builder = BuildSettingsTab },
            };

            var root = new DockPanel();
            Content = root;

            root.Children.Add(BuildHeader());
            root.Children.Add(BuildFooter());
            root.Children.Add(BuildUpdateBanner());

            var middle = new DockPanel();
            middle.Children.Add(BuildSidebar());

            var contentArea = new DockPanel();
            _contentHost = new ContentControl();
            contentArea.Children.Add(_contentHost);
            middle.Children.Add(contentArea);

            root.Children.Add(middle);

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    e.Handled = true;
                    ShowCommandPalette();
                }
            };

            NavigateTo("dashboard");
        }

        private class PaletteItem
        {
            public string Label;
            public string Group;
            public Action Activate;
        }

        // פלטת פקודות (Ctrl+K): קפיצה מהירה לכל עמוד או פעולה בלי לחפש בתפריט
        // הצדדי - נבנית מאותה רשימת _navEntries שכבר מזינה את הסיידבר, כך
        // שאין רשימה כפולה לתחזק.
        private void ShowCommandPalette()
        {
            var items = new List<PaletteItem>();
            foreach (var entry in _navEntries)
            {
                var capturedKey = entry.Key;
                items.Add(new PaletteItem { Label = I18n.T(entry.LabelKey), Group = I18n.T("cmdpalette_group_pages"), Activate = () => NavigateTo(capturedKey) });
            }
            items.Add(new PaletteItem { Label = I18n.T("action_run_smart_wizard"), Group = I18n.T("cmdpalette_group_actions"), Activate = () => NavigateTo("dashboard") });
            items.Add(new PaletteItem { Label = I18n.T("action_run_quick_scan"), Group = I18n.T("cmdpalette_group_actions"), Activate = () => NavigateTo("seccenter") });
            items.Add(new PaletteItem
            {
                Label = I18n.T("action_toggle_theme"), Group = I18n.T("cmdpalette_group_actions"),
                Activate = () =>
                {
                    _settings.Theme = _settings.Theme == "Dark" ? "Light" : "Dark";
                    _settings.Save();
                    Dialogs.Info(I18n.T("generic_done_title"), I18n.T("restart_note"));
                }
            });

            var w = new Window
            {
                Width = 580, Height = 440, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = Theme.Get("PanelBrush"),
                FlowDirection = I18n.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                FontFamily = new FontFamily("Segoe UI"), Owner = this,
                BorderBrush = Theme.Get("AccentBrush"), BorderThickness = new Thickness(2)
            };
            var root = new DockPanel { Margin = new Thickness(2) };
            w.Content = root;

            var txtSearch = new TextBox
            {
                FontSize = 16, Height = 44, Padding = new Thickness(12,10,12,10), BorderThickness = new Thickness(0,0,0,1),
                BorderBrush = Theme.Get("BorderColorBrush"), Background = Theme.Get("PanelBrush"), Foreground = Theme.Get("TextBrush"),
                ToolTip = I18n.T("cmdpalette_placeholder")
            };
            DockPanel.SetDock(txtSearch, Dock.Top);
            root.Children.Add(txtSearch);

            var noResultsLbl = new TextBlock { Text = I18n.T("cmdpalette_no_results"), Foreground = Theme.Get("TextMutedBrush"), Margin = new Thickness(16), Visibility = Visibility.Collapsed };
            root.Children.Add(noResultsLbl);

            var listBox = new ListBox { BorderThickness = new Thickness(0), Background = Theme.Get("PanelBrush") };
            var template = new DataTemplate();
            var rowFactory = new FrameworkElementFactory(typeof(DockPanel));
            rowFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(10,7,10,7));
            var groupFactory = new FrameworkElementFactory(typeof(TextBlock));
            groupFactory.SetBinding(TextBlock.TextProperty, new Binding("Group"));
            groupFactory.SetValue(TextBlock.ForegroundProperty, Theme.Get("TextMutedBrush"));
            groupFactory.SetValue(TextBlock.WidthProperty, 90.0);
            groupFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            groupFactory.SetValue(DockPanel.DockProperty, Dock.Left);
            var labelFactory = new FrameworkElementFactory(typeof(TextBlock));
            labelFactory.SetBinding(TextBlock.TextProperty, new Binding("Label"));
            labelFactory.SetValue(TextBlock.ForegroundProperty, Theme.Get("TextBrush"));
            labelFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
            rowFactory.AppendChild(groupFactory);
            rowFactory.AppendChild(labelFactory);
            template.VisualTree = rowFactory;
            listBox.ItemTemplate = template;
            root.Children.Add(listBox);

            Action<string> filterList = query =>
            {
                var q = (query ?? "").Trim();
                var filtered = string.IsNullOrEmpty(q) ? items : items.Where(i => i.Label.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                listBox.ItemsSource = filtered;
                if (filtered.Count > 0) listBox.SelectedIndex = 0;
                noResultsLbl.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            };
            filterList(null);

            Action activateSelected = () =>
            {
                var sel = listBox.SelectedItem as PaletteItem;
                if (sel != null) { w.Close(); sel.Activate(); }
            };

            txtSearch.TextChanged += (s, e) => filterList(txtSearch.Text);
            txtSearch.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Down) { if (listBox.SelectedIndex < listBox.Items.Count - 1) listBox.SelectedIndex++; e.Handled = true; }
                else if (e.Key == Key.Up) { if (listBox.SelectedIndex > 0) listBox.SelectedIndex--; e.Handled = true; }
                else if (e.Key == Key.Enter) { activateSelected(); e.Handled = true; }
                else if (e.Key == Key.Escape) { w.Close(); e.Handled = true; }
            };
            listBox.MouseDoubleClick += (s, e) => activateSelected();

            w.Loaded += (s, e) => txtSearch.Focus();
            w.ShowDialog();
        }

        // ---------------------------------------------------------------
        // Header + sidebar + global bar + footer
        // ---------------------------------------------------------------
        // Non-blocking update banner (was previously a dead setting: "auto-check
        // updates" saved to disk but nothing ever read it back). Per the shared
        // standard, an available update is a "persistent state until resolved" -
        // a dismissible banner, not a Toast that vanishes or a Dialog that blocks
        // startup. Only ever shown if the user opted in via Settings and
        // configured an update manifest URL; a failed/absent check stays silent.
        private UIElement BuildUpdateBanner()
        {
            var banner = new Border
            {
                Background = Theme.Get("AccentLightBrush"),
                BorderBrush = Theme.Get("AccentBrush"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(18, 10, 18, 10),
                Visibility = Visibility.Collapsed
            };
            DockPanel.SetDock(banner, Dock.Top);

            var row = new DockPanel();
            banner.Child = row;

            var btnDismiss = MakeButton("✕", "GhostButtonStyle", 32);
            btnDismiss.Height = 28;
            AutomationProperties.SetName(btnDismiss, I18n.T("btn_close"));
            DockPanel.SetDock(btnDismiss, Dock.Right);
            btnDismiss.Click += (s, e) => banner.Visibility = Visibility.Collapsed;
            row.Children.Add(btnDismiss);

            var btnDownload = MakeButton(I18n.T("btn_update_banner_download"), "AccentButtonStyle", 150);
            btnDownload.Height = 28;
            DockPanel.SetDock(btnDownload, Dock.Right);
            row.Children.Add(btnDownload);

            var text = new TextBlock { Foreground = Theme.Get("TextBrush"), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            row.Children.Add(text);

            if (_settings.AutoCheckUpdates && !_updateCheckedThisSession)
            {
                _updateCheckedThisSession = true;
                var dispatcher = Dispatcher;
                // Fire a few seconds after launch (never blocks startup) and
                // fail completely silently - a background version check must
                // never interrupt or alarm the user just because GitHub is
                // unreachable, rate-limited, or the machine is offline.
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5));
                        var info = UpdateChecker.Check(Program.AppVersion);
                        if (info != null && info.Available && string.IsNullOrEmpty(info.Error))
                        {
                            if (_settings.AutoInstallUpdates)
                            {
                                // Opt-in fully-automatic path: no click needed. Still
                                // shown via the same banner (transparency - the app is
                                // about to close itself), just skipped straight to the
                                // one-click flow instead of waiting on btnDownload.
                                dispatcher.Invoke(() =>
                                {
                                    text.Text = string.Format(I18n.T("update_banner_text"), info.LatestVersion);
                                    banner.Visibility = Visibility.Visible;
                                    btnDownload.Visibility = Visibility.Collapsed;
                                });
                                bool launched = await RunOneClickUpdateAsync(info);
                                if (launched) return;
                                // If the automatic install didn't go ahead (UAC
                                // declined, download failed) the app is still
                                // running - give the banner its button back so
                                // the user can retry by hand.
                                dispatcher.Invoke(() =>
                                {
                                    btnDownload.Visibility = Visibility.Visible;
                                    btnDownload.Click += async (s, e) => await RunOneClickUpdateAsync(info);
                                });
                            }
                            else
                            {
                                dispatcher.Invoke(() =>
                                {
                                    text.Text = string.Format(I18n.T("update_banner_text"), info.LatestVersion);
                                    banner.Visibility = Visibility.Visible;
                                    btnDownload.Click += async (s, e) =>
                                    {
                                        await RunOneClickUpdateAsync(info);
                                    };
                                });
                            }
                        }
                    }
                    catch { }
                });
            }

            return banner;
        }

        // One-click self-update: downloads the installer asset from the
        // GitHub release, verifies it, and launches it with /SILENT, then
        // closes OptiGuard so the install isn't blocked by the running exe.
        // Used by the Settings "Check for Updates" button, the update
        // banner's download button, and (when AutoInstallUpdates is on) the
        // fully-automatic startup path above - all three just need to hand
        // it an UpdateInfo with Available=true.
        //
        // Safe to call from any thread: every UI touch is explicitly
        // dispatched, since the automatic path calls this from the
        // background Task.Run in BuildUpdateBanner above.
        //
        // On any failure this falls back to the pre-4.11.2 behavior -
        // opening the GitHub release page in the browser - so a network
        // hiccup, a full disk, or a non-zero installer exit code never
        // leaves the user with no way to get the update at all.
        private async Task<bool> RunOneClickUpdateAsync(UpdateInfo info)
        {
            if (info == null) return false;

            if (info.Asset == null)
            {
                // Release has no installer asset attached (shouldn't normally
                // happen - RELEASE-CHECKLIST.md requires exactly one - but
                // don't assume it never will) - nothing to download.
                string noAssetError;
                UpdateChecker.OpenReleasePage(info.ReleaseUrl, out noAssetError);
                return false;
            }

            Window dlg = null;
            ProgressBar bar = null;
            TextBlock label = null;
            Dispatcher.Invoke(() =>
            {
                dlg = Dialogs.ShowProgressDialog(I18n.T("update_available_title"), I18n.T("update_downloading"), out bar, out label);
                try { dlg.Owner = this; } catch { }
            });

            string error = null;
            bool ok = await Task.Run(() => UpdateChecker.DownloadAndLaunchSilentInstall(info, percent =>
            {
                // Real percentage in text too, not only the bar (STANDARDS.md
                // 16.3: "Downloading update... 45%").
                Dispatcher.Invoke(() =>
                {
                    if (bar != null) bar.Value = percent;
                    if (label != null) label.Text = I18n.T("update_downloading") + " " + percent + "%";
                });
            }, out error));

            if (ok)
            {
                Dispatcher.Invoke(() => { if (label != null) label.Text = I18n.T("update_launching"); });
                await Task.Delay(1200);
                Dispatcher.Invoke(() => { try { dlg.Close(); } catch { } });
                Dispatcher.Invoke(() => Application.Current.Shutdown());
                return true;
            }

            Dispatcher.Invoke(() => { try { dlg.Close(); } catch { } });

            // The user said "No" to the Windows UAC prompt for the installer.
            // That's a deliberate choice, not a failure - don't answer it with
            // an error dialog plus a browser tab they didn't ask for. The
            // banner/Settings button stay available to try again later.
            if (error == UpdateChecker.ErrorCancelledByUser) return false;

            Dispatcher.Invoke(() => Dialogs.ShowError(I18n.T("generic_error_title"), string.Format(I18n.T("update_download_failed"), error)));

            string fallbackError;
            UpdateChecker.OpenReleasePage(info.ReleaseUrl, out fallbackError);
            return false;
        }

        private UIElement BuildHeader()
        {
            var header = new Border { Background = Theme.Get("HeaderBgBrush"), Height = 66 };
            DockPanel.SetDock(header, Dock.Top);
            var dock = new DockPanel { Margin = new Thickness(18,0,18,0), LastChildFill = false };
            header.Child = dock;

            var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(stack, Dock.Left);
            var logoImg = new System.Windows.Controls.Image { Width = 40, Height = 40, Source = Icon, Margin = new Thickness(0,0,12,0) };
            stack.Children.Add(logoImg);

            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock { Text = I18n.T("app_name"), Foreground = Theme.Get("HeaderTextBrush"), FontSize = 18, FontWeight = FontWeights.Bold });
            titleStack.Children.Add(new TextBlock { Text = I18n.T("app_tagline"), Foreground = Theme.Get("HeaderSubTextBrush"), FontSize = 11 });
            stack.Children.Add(titleStack);

            dock.Children.Add(stack);
            return header;
        }

        private UIElement BuildFooter()
        {
            var footer = new Border { Background = Theme.Get("BgBrush"), Height = 24 };
            DockPanel.SetDock(footer, Dock.Bottom);
            var footerDock = new DockPanel();
            var hint = new TextBlock { Text = I18n.T("cmdpalette_placeholder"), Foreground = Theme.Get("TextMutedBrush"), FontSize = 10.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14,0,14,0) };
            DockPanel.SetDock(hint, Dock.Right);
            footerDock.Children.Add(hint);
            var txt = new TextBlock { Text = I18n.T("copyright"), Foreground = Theme.Get("TextMutedBrush"), FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            footerDock.Children.Add(txt);
            footer.Child = footerDock;
            return footer;
        }

        private UIElement BuildSidebar()
        {
            var border = new Border { Background = Theme.Get("PanelBrush"), Width = 232 };
            border.BorderBrush = Theme.Get("BorderColorBrush");
            border.BorderThickness = new Thickness(0,0,1,0);
            DockPanel.SetDock(border, Dock.Left);

            var sidebarDock = new DockPanel();
            border.Child = sidebarDock;

            var settingsEntry = _navEntries.First(n => n.Key == "settings");
            var sep = new Border { Height = 1, Background = Theme.Get("BorderColorBrush"), Margin = new Thickness(14,6,14,10) };
            DockPanel.SetDock(sep, Dock.Bottom);
            var settingsBtn = BuildNavButton(settingsEntry);
            settingsBtn.Margin = new Thickness(10,0,10,10);
            DockPanel.SetDock(settingsBtn, Dock.Bottom);
            sidebarDock.Children.Add(settingsBtn);
            sidebarDock.Children.Add(sep);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack = new StackPanel { Margin = new Thickness(10,16,10,10) };
            scroll.Content = stack;

            string lastGroup = null;
            foreach (var entry in _navEntries.Where(n => n.GroupKey != null))
            {
                if (entry.GroupKey != lastGroup)
                {
                    var groupLbl = new TextBlock
                    {
                        Text = I18n.T(entry.GroupKey), FontSize = 11, FontWeight = FontWeights.Bold,
                        Foreground = Theme.Get("TextMutedBrush"), Margin = new Thickness(10, lastGroup == null ? 0 : 18, 10, 6)
                    };
                    stack.Children.Add(groupLbl);
                    lastGroup = entry.GroupKey;
                }
                stack.Children.Add(BuildNavButton(entry));
            }

            sidebarDock.Children.Add(scroll);
            return border;
        }

        private Border BuildNavButton(NavEntry entry)
        {
            var border = new Border
            {
                Padding = new Thickness(14,10,14,10), Margin = new Thickness(0,2,0,2), CornerRadius = new CornerRadius(8),
                Cursor = Cursors.Hand, Background = Brushes.Transparent,
                BorderThickness = new Thickness(2), BorderBrush = Brushes.Transparent,
                Focusable = true, SnapsToDevicePixels = true
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            string icon;
            if (NavIcons.TryGetValue(entry.Key, out icon))
            {
                row.Children.Add(new TextBlock { Text = icon, FontSize = 13.5, Margin = new Thickness(0,0,8,0), VerticalAlignment = VerticalAlignment.Center });
            }
            var text = new TextBlock { Text = I18n.T(entry.LabelKey), FontSize = 13.5, Foreground = Theme.Get("TextBrush"), VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(text);
            border.Child = row;

            // This is a custom-drawn Border, not a real Button/ListBoxItem, so
            // none of WPF's built-in keyboard/Narrator support is free here -
            // it's the app's whole primary navigation, so this matters a lot
            // (section 18.2). Wired explicitly: Tab reaches it, Enter/Space
            // activates it exactly like clicking it, a visible focus ring shows
            // while it's keyboard-focused (the flat Border has no default WPF
            // focus adorner the way a Control does), and Narrator gets a real
            // name instead of silently skipping a clickable-but-invisible-to-it
            // element.
            AutomationProperties.SetName(border, I18n.T(entry.LabelKey));
            border.MouseLeftButtonUp += (s, e) => NavigateTo(entry.Key);
            border.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Space) { NavigateTo(entry.Key); e.Handled = true; }
            };
            border.GotKeyboardFocus += (s, e) => border.BorderBrush = Theme.Get("AccentBrush");
            border.LostKeyboardFocus += (s, e) => border.BorderBrush = Brushes.Transparent;
            border.MouseEnter += (s, e) => { if (_currentNavKey != entry.Key) { border.Background = Theme.Get("HoverBgBrush"); text.Foreground = Theme.Get("HoverTextBrush"); } };
            border.MouseLeave += (s, e) => { if (_currentNavKey != entry.Key) { border.Background = Brushes.Transparent; text.Foreground = Theme.Get("TextBrush"); } };
            _navButtons[entry.Key] = border;
            _navLabels[entry.Key] = text;
            return border;
        }

        private UIElement BuildGlobalBar()
        {
            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(14,10,14,4) };
            DockPanel.SetDock(bar, Dock.Top);

            var btnHunter = MakeButton(I18n.T("global_hunter"), "GhostButtonStyle", 150);
            btnHunter.Click += (s, e) => Dialogs.ShowLeftoverHunter();

            var btnEmpty = MakeButton(I18n.T("global_empty_folders"), "GhostButtonStyle", 170);
            btnEmpty.Click += (s, e) => Dialogs.ShowEmptyFolderCleaner();

            var btnLog = MakeButton(I18n.T("global_open_log"), "GhostButtonStyle", 100);
            btnLog.Click += (s, e) =>
            {
                AppPaths.EnsureDataDir();
                if (!System.IO.File.Exists(AppPaths.LogFile)) System.IO.File.WriteAllText(AppPaths.LogFile, "");
                Process.Start("notepad.exe", AppPaths.LogFile);
            };

            bar.Children.Add(btnHunter);
            bar.Children.Add(btnEmpty);
            bar.Children.Add(btnLog);
            return bar;
        }

        private void NavigateTo(string key)
        {
            var entry = _navEntries.FirstOrDefault(n => n.Key == key);
            if (entry == null) return;

            if (!entry.Built)
            {
                entry.Content = entry.Builder();
                entry.Built = true;
                if (entry.OnFirstShow != null) entry.OnFirstShow();
            }
            if (entry.OnEveryShow != null) entry.OnEveryShow();

            if (_currentNavKey != null && _navButtons.ContainsKey(_currentNavKey))
            {
                _navButtons[_currentNavKey].Background = Brushes.Transparent;
                _navLabels[_currentNavKey].Foreground = Theme.Get("TextBrush");
                _navLabels[_currentNavKey].FontWeight = FontWeights.Normal;
            }
            _currentNavKey = key;
            _navButtons[key].Background = Theme.Get("AccentBrush");
            _navLabels[key].Foreground = Theme.Get("AccentTextBrush");
            _navLabels[key].FontWeight = FontWeights.SemiBold;

            _contentHost.Content = entry.Content;
            entry.Content.Opacity = 0;
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            entry.Content.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------
        private DataGrid CreateGrid(params Tuple<string, string, double>[] cols)
        {
            var grid = new DataGrid
            {
                Style = (Style)Theme.GetStyle("ModernDataGridStyle"),
                ColumnHeaderStyle = (Style)Theme.GetStyle("ModernColumnHeaderStyle"),
                CellStyle = (Style)Theme.GetStyle("ModernCellStyle"),
                RowStyle = (Style)Theme.GetStyle("ModernRowStyle"),
                CanUserSortColumns = true
            };
            foreach (var c in cols)
            {
                grid.Columns.Add(new DataGridTextColumn { Header = c.Item1, Binding = new Binding(c.Item2), Width = new DataGridLength(c.Item3, DataGridLengthUnitType.Star) });
            }
            return grid;
        }

        private StackPanel BottomBar()
        {
            return new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(14,8,14,14) };
        }

        // MinWidth, not Width: the button styles don't wrap or trim their text
        // (BaseButtonStyle's ContentPresenter has no TextWrapping/TextTrimming,
        // and a WPF Border with CornerRadius does not auto-clip its child on
        // .NET Framework), so a fixed Width sized for the English caption would
        // let a longer Hebrew (or just longer-than-expected) translation spill
        // text outside the button's rounded rectangle instead of growing to fit.
        // MinWidth keeps every button at its tuned width by default and only
        // grows it when the actual rendered caption needs more room.
        private static Button MakeButton(string text, string styleKey, double width)
        {
            return new Button { Content = text, Style = (Style)Theme.GetStyle(styleKey), MinWidth = width, Margin = new Thickness(0,0,6,0) };
        }

        // מאחד כמה כפתורים משניים לכפתור "עוד" אחד עם תפריט נפתח - מצמצם עומס
        // חזותי בשורות פעולה עמוסות בלי לוותר על אף פעולה. הכפתורים המקוריים
        // לעולם לא נכנסים לעץ הוויזואלי - רק ה-Click שלהם מופעל מתוך התפריט,
        // כך שכל החיווט הקיים (שמתבצע בהמשך הקוד) ממשיך לעבוד ללא שינוי.
        private static Button MakeMoreMenuButton(params Button[] actions)
        {
            var trigger = MakeButton("⋯ " + I18n.T("btn_more_actions"), "GhostButtonStyle", 90);
            var menu = new ContextMenu { Background = Theme.Get("PanelBrush"), BorderBrush = Theme.Get("BorderColorBrush"), BorderThickness = new Thickness(1) };
            foreach (var action in actions)
            {
                var capturedAction = action;
                var item = new MenuItem { Header = action.Content, Foreground = Theme.Get("TextBrush"), Background = Theme.Get("PanelBrush"), Padding = new Thickness(12,6,12,6) };
                item.Click += (s, e) => capturedAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                menu.Items.Add(item);
            }
            // A ContextMenu lives in its own popup window, outside MainWindow's
            // visual tree, so it doesn't reliably pick up the window's
            // FlowDirection - set it explicitly so Hebrew menus open RTL.
            trigger.Click += (s, e) => { menu.FlowDirection = I18n.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight; menu.PlacementTarget = trigger; menu.IsOpen = true; };
            return trigger;
        }

        private TextBlock SectionLabel(string text)
        {
            return new TextBlock { Text = text, Style = (Style)Theme.GetStyle("SectionLabelStyle") };
        }

        // DataGridCheckBoxColumn נראה עובד אבל בפועל אינו לחיץ: DataGrid.IsReadOnly
        // (שמוגדר ב-Style המשותף) גובר על IsReadOnly=false ברמת העמודה - זה תנאי
        // OR, לא override. CheckBox אמיתי בתוך DataGridTemplateColumn עוקף את
        // מנגנון עריכת התאים לגמרי ולכן תמיד לחיץ, גם כשה-Grid כולו read-only.
        private static DataGridTemplateColumn CreateCheckboxColumn(string bindingPath)
        {
            var factory = new FrameworkElementFactory(typeof(CheckBox));
            factory.SetBinding(CheckBox.IsCheckedProperty, new Binding(bindingPath) { Mode = BindingMode.TwoWay });
            factory.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            factory.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            var template = new DataTemplate { VisualTree = factory };
            return new DataGridTemplateColumn { Header = "", CellTemplate = template, Width = new DataGridLength(36) };
        }

        // ---------------------------------------------------------------
        // Dashboard
        // ---------------------------------------------------------------
        private Border StatCard(string value, string label)
        {
            var card = new Border
            {
                Background = Theme.Get("PanelBrush"), BorderBrush = Theme.Get("BorderColorBrush"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(12,10,12,10), Margin = new Thickness(0,0,8,8),
                MinWidth = 160
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = value, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Theme.Get("AccentBrush") });
            stack.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,3,0,0) });
            card.Child = stack;
            return card;
        }

        private Border WizardMiniCard(string title, string desc, Button actionButton, TextBlock successIndicator)
        {
            var card = new Border
            {
                Background = Theme.Get("AccentLightBrush"), BorderBrush = Theme.Get("AccentBrush"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0,0,8,8),
                Width = 226, Effect = Theme.CardShadow(_settings.Theme)
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Theme.Get("TextBrush"), TextWrapping = TextWrapping.Wrap });
            stack.Children.Add(new TextBlock { Text = desc, Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,4,0,10), FontSize = 11 });
            actionButton.HorizontalAlignment = HorizontalAlignment.Left;
            stack.Children.Add(actionButton);
            if (successIndicator != null)
            {
                successIndicator.Margin = new Thickness(0,8,0,0);
                stack.Children.Add(successIndicator);
            }
            card.Child = stack;
            return card;
        }

        private UIElement BuildHealthScoreCard()
        {
            var card = new Border
            {
                Background = Theme.Get("PanelBrush"), BorderBrush = Theme.Get("BorderColorBrush"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Padding = new Thickness(16,12,16,12), Margin = new Thickness(0,0,0,10),
                Effect = Theme.CardShadow(_settings.Theme)
            };
            var outer = new DockPanel();
            card.Child = outer;

            var scoreText = new TextBlock { FontSize = 34, FontWeight = FontWeights.Bold, Foreground = Theme.Get("AccentBrush"), Text = "--", VerticalAlignment = VerticalAlignment.Center };
            var scoreBox = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,16,0) };
            DockPanel.SetDock(scoreBox, Dock.Left);
            scoreBox.Children.Add(scoreText);
            scoreBox.Children.Add(new TextBlock { Text = I18n.T("health_score_title"), FontSize = 10, Foreground = Theme.Get("TextMutedBrush"), HorizontalAlignment = HorizontalAlignment.Center });
            outer.Children.Add(scoreBox);

            var btnRefreshScore = MakeButton("🔄", "GhostButtonStyle", 34);
            btnRefreshScore.ToolTip = I18n.T("btn_refresh");
            // Icon-only button (emoji Content) - Narrator would otherwise read
            // the raw glyph instead of a meaningful label (section 18.2).
            AutomationProperties.SetName(btnRefreshScore, I18n.T("btn_refresh"));
            DockPanel.SetDock(btnRefreshScore, Dock.Right);
            btnRefreshScore.VerticalAlignment = VerticalAlignment.Top;
            outer.Children.Add(btnRefreshScore);

            var rightStack = new StackPanel();
            outer.Children.Add(rightStack);
            var descLbl = new TextBlock { Text = I18n.T("health_score_computing"), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,4), FontSize = 11 };
            rightStack.Children.Add(descLbl);
            var issuesPanel = new StackPanel();
            rightStack.Children.Add(issuesPanel);

            Action<HealthScoreResult> render = result =>
            {
                scoreText.Text = result.Score.ToString();
                var brush = result.Score >= 80 ? Theme.Get("AccentBrush") : (result.Score >= 50 ? Theme.Get("TextBrush") : Theme.Get("DangerBrush"));
                scoreText.Foreground = brush;
                card.BorderBrush = brush;
                issuesPanel.Children.Clear();
                if (result.Issues.Count == 0)
                {
                    descLbl.Text = I18n.T("health_score_perfect");
                }
                else
                {
                    descLbl.Text = I18n.T("health_score_desc");
                    foreach (var issue in result.Issues.OrderByDescending(i => i.Penalty).Take(3))
                    {
                        var row = new DockPanel { Margin = new Thickness(0,1,0,1) };
                        var text = I18n.T(issue.TitleKey) + (issue.Detail != null ? " (" + issue.Detail + ")" : "");
                        var btnFix = MakeButton(I18n.T("btn_fix_this"), "GhostButtonStyle", 90);
                        btnFix.FontSize = 11; btnFix.Height = 24;
                        var navTarget = issue.NavTarget;
                        btnFix.Click += (s, e) => NavigateTo(navTarget);
                        DockPanel.SetDock(btnFix, Dock.Right);
                        row.Children.Add(btnFix);
                        row.Children.Add(new TextBlock { Text = "• " + text, FontSize = 11, Foreground = Theme.Get("TextBrush"), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
                        issuesPanel.Children.Add(row);
                    }
                }
            };

            bool isComputing = false;
            Action refreshScore = async () =>
            {
                if (isComputing) return;
                isComputing = true;
                btnRefreshScore.IsEnabled = false;
                descLbl.Text = I18n.T("health_score_computing");
                var dispatcher = Dispatcher;
                var result = await Task.Run(() => HealthScoreData.Compute());
                dispatcher.Invoke(() =>
                {
                    render(result);
                    btnRefreshScore.IsEnabled = true;
                    isComputing = false;
                    // Keep the desktop widget's score in sync immediately
                    // rather than waiting for its own periodic timer - handing
                    // it this result instead of making it run the (WMI-heavy)
                    // HealthScoreData.Compute() a second time.
                    WidgetWindow.ShowResultIfOpen(result);
                });
            };
            btnRefreshScore.Click += (s, e) => refreshScore();
            _refreshHealthScore = refreshScore;
            refreshScore();

            return card;
        }

        // ---------------------------------------------------------------
        // Window state persistence (size/position/maximized) - remembered
        // across sessions per the cross-tool standard. Falls back to a
        // sane default that fits a 1366x768 laptop screen (previously a
        // fixed 1260x840 could open partially off-screen below the taskbar
        // on smaller displays) if nothing was saved yet, or if the saved
        // position no longer fits any current monitor (e.g. a second
        // display was unplugged since the last run).
        // ---------------------------------------------------------------
        private void RestoreWindowState()
        {
            double defaultWidth = Math.Min(1260, SystemParameters.WorkArea.Width - 40);
            double defaultHeight = Math.Min(760, SystemParameters.WorkArea.Height - 40);

            if (_settings.WindowWidth > 0 && _settings.WindowHeight > 0)
            {
                Width = Math.Max(MinWidth, Math.Min(_settings.WindowWidth, SystemParameters.WorkArea.Width));
                Height = Math.Max(MinHeight, Math.Min(_settings.WindowHeight, SystemParameters.WorkArea.Height));

                bool fitsOnScreen = ScreenHelper.FitsVirtualScreen(_settings.WindowLeft, _settings.WindowTop, 100);

                if (fitsOnScreen)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = _settings.WindowLeft;
                    Top = _settings.WindowTop;
                }
                else
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
            }
            else
            {
                Width = defaultWidth;
                Height = defaultHeight;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            // Per the standard: never restore "opens maximized" as a surprise on
            // first-ever launch, but do honor it once the user has actually done
            // it themselves in a previous session.
            if (_settings.WindowMaximized) WindowState = WindowState.Maximized;
        }

        private void SaveWindowState()
        {
            try
            {
                _settings.WindowMaximized = WindowState == WindowState.Maximized;
                // RestoreBounds stays correct even while maximized, so size/position
                // restore to something sane if the user un-maximizes next time.
                var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
                _settings.WindowWidth = bounds.Width;
                _settings.WindowHeight = bounds.Height;
                _settings.WindowLeft = bounds.Left;
                _settings.WindowTop = bounds.Top;
                _settings.Save();
            }
            catch { }
        }

        private UIElement BuildDashboardTab()
        {
            var panel = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
            var scroll = new ScrollViewer { Background = Theme.Get("BgBrush"), Content = panel };

            var greeting = new TextBlock { Text = string.Format(I18n.T("dash_greeting"), I18n.T("app_name")), FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0,0,0,2) };
            var sinceLbl = new TextBlock { FontSize = 11, Foreground = Theme.Get("TextMutedBrush"), Margin = new Thickness(0,0,0,8) };
            panel.Children.Add(greeting);
            panel.Children.Add(sinceLbl);

            panel.Children.Add(BuildHealthScoreCard());

            var statsPanel = new System.Windows.Controls.WrapPanel { Margin = new Thickness(0,8,0,8) };
            panel.Children.Add(statsPanel);

            panel.Children.Add(SectionLabel(I18n.T("dash_wizards_section")));
            panel.Children.Add(new TextBlock { Text = I18n.T("dash_wizards_intro"), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,8), FontSize = 11 });

            var wizardsWrap = new WrapPanel { Margin = new Thickness(0,0,0,12) };
            panel.Children.Add(wizardsWrap);

            var btnWizard = MakeButton(I18n.T("btn_run_wizard"), "AccentButtonStyle", 220);
            var successLbl = new TextBlock { Text = "✓ " + I18n.T("secwiz_fixed"), FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Theme.Get("AccentBrush"), Opacity = 0 };
            wizardsWrap.Children.Add(WizardMiniCard(I18n.T("dash_wizard_title"), I18n.T("dash_wizard_desc"), btnWizard, successLbl));

            var btnSecWizard = MakeButton(I18n.T("btn_run_security_wizard"), "AccentButtonStyle", 220);
            wizardsWrap.Children.Add(WizardMiniCard(I18n.T("dash_secwiz_title"), I18n.T("dash_secwiz_desc"), btnSecWizard, null));

            var btnPrivWizard = MakeButton(I18n.T("btn_run_privacy_wizard"), "AccentButtonStyle", 220);
            wizardsWrap.Children.Add(WizardMiniCard(I18n.T("dash_privwiz_title"), I18n.T("dash_privwiz_desc"), btnPrivWizard, null));

            var btnRegWizard = MakeButton(I18n.T("btn_run_registry_wizard"), "AccentButtonStyle", 220);
            wizardsWrap.Children.Add(WizardMiniCard(I18n.T("dash_regwiz_title"), I18n.T("dash_regwiz_desc"), btnRegWizard, null));

            btnSecWizard.Click += async (s, e) =>
            {
                var origSecContent = btnSecWizard.Content;
                btnSecWizard.IsEnabled = false;
                btnSecWizard.Content = I18n.T("junk_scanning");
                List<SecurityIssue> issues;
                try { issues = await Task.Run(() => SecurityWizard.Scan()); }
                catch (Exception ex) { btnSecWizard.Content = origSecContent; btnSecWizard.IsEnabled = true; Dialogs.ShowError(I18n.T("scan_error_title"), ex.Message); return; }
                btnSecWizard.Content = origSecContent;
                btnSecWizard.IsEnabled = true;
                Dialogs.ShowSecurityWizard(issues);
                if (_refreshHealthScore != null) _refreshHealthScore();
            };

            btnPrivWizard.Click += (s, e) => { Dialogs.ShowPrivacyWizard(); if (_refreshHealthScore != null) _refreshHealthScore(); };

            btnRegWizard.Click += (s, e) => NavigateTo("regclean");

            Action refresh = () =>
            {
                var d = Stats.Load();
                sinceLbl.Text = string.Format(I18n.T("dash_since"), d.FirstUseDate);
                statsPanel.Children.Clear();
                statsPanel.Children.Add(StatCard(d.ProgramsUninstalled.ToString(), I18n.T("dash_stat_programs")));
                statsPanel.Children.Add(StatCard(d.ResidualItemsRemoved.ToString(), I18n.T("dash_stat_leftovers")));
                statsPanel.Children.Add(StatCard(JunkCleanerData.FormatSize(d.JunkBytesFreed), I18n.T("dash_stat_junk")));
                statsPanel.Children.Add(StatCard(d.JunkCleanupRuns.ToString(), I18n.T("dash_stat_junk_runs")));
                statsPanel.Children.Add(StatCard(d.StartupItemsCleaned.ToString(), I18n.T("dash_stat_startup")));
                statsPanel.Children.Add(StatCard(d.ExtensionsRemoved.ToString(), I18n.T("dash_stat_ext")));
                statsPanel.Children.Add(StatCard(d.GamesRemoved.ToString(), I18n.T("dash_stat_games")));
            };
            _refreshDashboard = refresh;

            btnWizard.Click += async (s, e) =>
            {
                var origWizContent = btnWizard.Content;
                btnWizard.IsEnabled = false;
                btnWizard.Content = I18n.T("junk_scanning");
                List<WizardFinding> findings;
                try { findings = await Task.Run(() => SmartWizard.Scan()); }
                catch (Exception ex) { btnWizard.Content = origWizContent; btnWizard.IsEnabled = true; Dialogs.ShowError(I18n.T("scan_error_title"), ex.Message); return; }
                btnWizard.Content = origWizContent;
                btnWizard.IsEnabled = true;
                var summary = Dialogs.ShowWizardResults(findings);
                refresh();
                if (summary != null)
                {
                    if (_refreshHealthScore != null) _refreshHealthScore();
                    successLbl.Opacity = 1;
                    var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromSeconds(2)) { BeginTime = TimeSpan.FromSeconds(1) };
                    successLbl.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                }
            };

            refresh();
            return scroll;
        }

        private UIElement BuildTimelineTab()
        {
            var dock = new DockPanel { Background = Theme.Get("BgBrush") };

            var intro = new TextBlock { Text = I18n.T("timeline_intro"), Margin = new Thickness(14,10,14,6), TextWrapping = TextWrapping.Wrap, Foreground = Theme.Get("TextMutedBrush") };
            DockPanel.SetDock(intro, Dock.Top);
            dock.Children.Add(intro);

            var bottom = BottomBar();
            DockPanel.SetDock(bottom, Dock.Bottom);
            var btnRefresh = MakeButton(I18n.T("btn_refresh"), "GhostButtonStyle", 90);
            bottom.Children.Add(btnRefresh);
            dock.Children.Add(bottom);

            _gridTimeline = CreateGrid(
                Tuple.Create(I18n.T("col_timeline_when"), "WhenText", 1.0),
                Tuple.Create(I18n.T("col_timeline_what"), "TitleText", 1.2),
                Tuple.Create(I18n.T("col_timeline_detail"), "Detail", 2.0));
            dock.Children.Add(_gridTimeline);

            Action refreshTimeline = () =>
            {
                try
                {
                    var events = ActivityTimelineData.GetRecent();
                    _gridTimeline.ItemsSource = events;
                }
                catch (Exception ex) { Dialogs.ShowError(I18n.T("generic_error_title"), ex.Message); }
            };
            _refreshTimeline = refreshTimeline;
            btnRefresh.Click += (s, e) => refreshTimeline();
            refreshTimeline();

            return dock;
        }

        // ---------------------------------------------------------------
        // Programs
        // ---------------------------------------------------------------
        private UIElement BuildProgramsTab()
        {
            var dock = new DockPanel { Background = Theme.Get("BgBrush") };

            // WrapPanel, not a plain horizontal StackPanel: at the window's own
            // declared MinWidth (960) this toolbar's controls (label+box+checkbox+
            // label+combo+button+status on the Programs tab) don't all fit on one
            // line. A non-wrapping StackPanel would render the trailing controls
            // (Refresh, the status count) past the visible window edge instead of
            // reflowing them - WrapPanel drops them to a second line instead.
            var top = new WrapPanel { Margin = new Thickness(14,10,14,6) };
            DockPanel.SetDock(top, Dock.Top);
            top.Children.Add(new TextBlock { Text = I18n.T("search_label"), VerticalAlignment = VerticalAlignment.Center, Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0,0,8,0) });
            _txtSearch = new TextBox { Width = 220, Height = 28, VerticalContentAlignment = VerticalAlignment.Center };
            top.Children.Add(_txtSearch);
            _chkShowSystem = new CheckBox { Content = I18n.T("show_system_components"), Style = (Style)Theme.GetStyle("CardCheckBoxStyle"), Margin = new Thickness(16,0,0,0), VerticalAlignment = VerticalAlignment.Center, ToolTip = I18n.T("show_system_components_tooltip") };
            _chkShowSystem.IsChecked = _settings.ShowSystemComponents;
            _chkShowSystem.Click += (s, e) => RefreshPrograms();
            top.Children.Add(_chkShowSystem);
            top.Children.Add(new TextBlock { Text = I18n.T("category_label"), VerticalAlignment = VerticalAlignment.Center, Foreground = Theme.Get("TextBrush"), Margin = new Thickness(16,0,8,0) });
            _cmbCategory = new ComboBox { Width = 190, Height = 28, DisplayMemberPath = "Label", SelectedValuePath = "Key" };
            _cmbCategory.Items.Add(new KeyLabel { Key = null, Label = I18n.T("category_all") });
            foreach (var c in Categorizer.AllCategories()) _cmbCategory.Items.Add(new KeyLabel { Key = c, Label = I18n.T(c) });
            _cmbCategory.SelectedIndex = 0;
            top.Children.Add(_cmbCategory);
            var btnRefresh = MakeButton(I18n.T("btn_refresh"), "GhostButtonStyle", 90);
            btnRefresh.Margin = new Thickness(16,0,0,0);
            btnRefresh.Click += (s, e) => RefreshPrograms();
            top.Children.Add(btnRefresh);
            var statusLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Foreground = Theme.Get("TextMutedBrush"), Margin = new Thickness(16,0,0,0) };
            top.Children.Add(statusLabel);
            dock.Children.Add(top);

            // Update status label when filter changes
            Action updateStatus = () =>
            {
                int total = _allPrograms != null ? _allPrograms.Count : 0;
                int filtered = _programsView != null ? _programsView.Cast<object>().Count() : 0;
                if (filtered == total) statusLabel.Text = string.Format(I18n.T("programs_status"), total);
                else statusLabel.Text = string.Format(I18n.T("programs_status_filtered"), filtered, total);
            };

            _txtSearch.TextChanged += (s, e) => { if (_programsView != null) { _programsView.Refresh(); updateStatus(); } };
            _cmbCategory.SelectionChanged += (s, e) => { if (_programsView != null) { _programsView.Refresh(); updateStatus(); } };
            dock.Loaded += (s, e) => updateStatus();

            var bottom = BottomBar();
            DockPanel.SetDock(bottom, Dock.Bottom);
            // היררכיית כפתורים: פעולה אחת בולטת (ירוק) לכל תצוגה, השאר משניות
            // (אפור), והשתיים ההרסניות (Force/Uninstall Selected) מקובצות יחד
            // בקצה עם רווח מפריד - כדי שלא יהיה קל ללחוץ עליהן בטעות תוך כדי
            // שימוש בפעולות הבטוחות היומיומיות.
            var btnUninstall = MakeButton(I18n.T("btn_uninstall_normal"), "AccentButtonStyle", 130);
            var btnDeep = MakeButton(I18n.T("btn_uninstall_deep"), "GhostButtonStyle", 200);
            var btnOpenFolder = MakeButton(I18n.T("btn_open_install_folder"), "GhostButtonStyle", 170);
            var btnDupes = MakeButton(I18n.T("btn_check_duplicates"), "GhostButtonStyle", 190);
            btnDupes.Click += (s, e) => Dialogs.ShowDuplicatePurposeReport(_allPrograms);
            var btnExport = MakeButton(I18n.T("btn_export_list"), "GhostButtonStyle", 140);
            // 4 פעולות משניות מאוחדות לתפריט "עוד" אחד במקום 4 כפתורים נפרדים -
            // צמצום עומס חזותי בשורת הכפתורים בלי לאבד גישה לאף פעולה.
            var btnMore = MakeMoreMenuButton(btnDeep, btnOpenFolder, btnDupes, btnExport);
            var btnForce = MakeButton(I18n.T("btn_uninstall_force"), "DangerButtonStyle", 140);
            btnForce.Margin = new Thickness(24,0,6,0);
            var btnUninstallSelected = MakeButton(I18n.T("btn_uninstall_selected"), "DangerButtonStyle", 160);
            bottom.Children.Add(btnUninstall);
            bottom.Children.Add(btnMore);
            bottom.Children.Add(btnForce);
            bottom.Children.Add(btnUninstallSelected);
            dock.Children.Add(bottom);

            _gridPrograms = CreateGrid(
                Tuple.Create(I18n.T("col_name"), "DisplayName", 2.6),
                Tuple.Create(I18n.T("col_category"), "CategoryText", 1.6),
                Tuple.Create(I18n.T("col_publisher"), "Publisher", 1.6),
                Tuple.Create(I18n.T("col_version"), "DisplayVersion", 1.0),
                Tuple.Create(I18n.T("col_size"), "SizeText", 0.9),
                Tuple.Create(I18n.T("col_install_date"), "InstallDateText", 1.0),
                Tuple.Create(I18n.T("col_last_used"), "LastUsedText", 1.0),
                Tuple.Create(I18n.T("col_safe_removal"), "SafeRemovalText", 1.3));
            _gridPrograms.Columns.Insert(0, CreateCheckboxColumn("IsSelected"));
            dock.Children.Add(_gridPrograms);

            btnUninstallSelected.Click += async (s, e) =>
            {
                var chosen = _allPrograms.Where(p => p.IsSelected).ToList();
                if (chosen.Count == 0) { Dialogs.Info("", I18n.T("no_items_selected")); return; }
                if (!Dialogs.Confirm(I18n.T("confirm_uninstall_title"), string.Format(I18n.T("confirm_batch_uninstall_msg"), chosen.Count))) return;
                Mouse.OverrideCursor = Cursors.Wait;
                int started = await Task.Run(() =>
                {
                    int count = 0;
                    foreach (var p in chosen)
                    {
                        if (string.IsNullOrWhiteSpace(p.UninstallString)) continue;
                        ProgramsData.RunUninstallString(p.UninstallString);
                        count++;
                    }
                    return count;
                });
                Mouse.OverrideCursor = null;
                if (started > 0) Stats.Add(d => d.ProgramsUninstalled += started);
                RefreshPrograms();
            };

            btnExport.Click += (s, e) =>
            {
                using (var sfd = new System.Windows.Forms.SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "OptiGuard-Programs-" + DateTime.Now.ToString("yyyyMMdd") + ".csv" })
                {
                    if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    try
                    {
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine("Name,Publisher,Version,Category,Size,InstallDate,LastUsed,SafeToRemove");
                        Func<string, string> esc = v => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
                        foreach (var p in _allPrograms.OrderBy(p => p.DisplayName))
                            sb.AppendLine(string.Join(",", esc(p.DisplayName), esc(p.Publisher), esc(p.DisplayVersion), esc(p.CategoryText), esc(p.SizeText), esc(p.InstallDateText), esc(p.LastUsedText), esc(p.SafeRemovalText)));
                        System.IO.File.WriteAllText(sfd.FileName, sb.ToString(), System.Text.Encoding.UTF8);
                        Dialogs.Info(I18n.T("generic_done_title"), I18n.T("export_done_msg"));
                    }
                    catch (Exception ex) { Dialogs.ShowError(I18n.T("generic_error_title"), ex.Message); }
                }
            };

            Func<InstalledProgram> Selected = () => _gridPrograms.SelectedItem as InstalledProgram;

            btnUninstall.Click += async (s, e) =>
            {
                var a = Selected(); if (a == null) return;
                if (string.IsNullOrWhiteSpace(a.UninstallString)) { Dialogs.Info("", I18n.T("msg_no_uninstall_string")); return; }
                if (!Dialogs.Confirm(I18n.T("confirm_uninstall_title"), string.Format(I18n.T("confirm_uninstall_msg"), a.DisplayName))) return;
                Mouse.OverrideCursor = Cursors.Wait;
                await Task.Run(() => ProgramsData.RunUninstallString(a.UninstallString));
                Mouse.OverrideCursor = null;
                Stats.Add(d => d.ProgramsUninstalled++);
                RefreshPrograms();
            };

            btnDeep.Click += async (s, e) =>
            {
                var a = Selected(); if (a == null) return;
                if (string.IsNullOrWhiteSpace(a.UninstallString)) { Dialogs.Info("", I18n.T("msg_no_uninstall_string")); return; }
                if (!Dialogs.Confirm(I18n.T("confirm_deep_title"), string.Format(I18n.T("confirm_deep_msg"), a.DisplayName))) return;
                Mouse.OverrideCursor = Cursors.Wait;
                await Task.Run(() => ProgramsData.RunUninstallString(a.UninstallString));
                Mouse.OverrideCursor = null;
                Stats.Add(d => d.ProgramsUninstalled++);
                Dialogs.Info(I18n.T("confirm_deep_title"), I18n.T("deep_wait_msg"));
                List<ResidualItem> items;
                Mouse.OverrideCursor = Cursors.Wait;
                try { items = await Task.Run(() => ProgramsData.FindResidualItems(a.DisplayName, a.Publisher, a.InstallLocation)); }
                catch (Exception ex) { Mouse.OverrideCursor = null; Dialogs.ShowError(I18n.T("scan_error_title"), string.Format(I18n.T("scan_error_msg"), ex.Message)); RefreshPrograms(); return; }
                Mouse.OverrideCursor = null;
                if (RegistryUtil.SubKeyExists(a.Hive, a.SubKeyPath) && !items.Any(i => i.Type == ResidualType.Registry && i.SubKeyPath == a.SubKeyPath))
                {
                    items.Add(new ResidualItem { Type = ResidualType.Registry, Hive = a.Hive, SubKeyPath = a.SubKeyPath, Path = a.SubKeyPath, DisplayPath = ProgramsData.HiveName(a.Hive) + "\\" + a.SubKeyPath, Reason = "original uninstall entry (not yet deleted)" });
                }
                MergeFingerprintItems(items, a.DisplayName);
                Dialogs.ShowResidualPicker(items, a.DisplayName);
                RefreshPrograms();
            };

            btnForce.Click += async (s, e) =>
            {
                var a = Selected(); if (a == null) return;
                if (!Dialogs.Confirm(I18n.T("confirm_force_title"), string.Format(I18n.T("confirm_force_msg"), a.DisplayName))) return;
                Mouse.OverrideCursor = Cursors.Wait;
                List<ResidualItem> items;
                try
                {
                    await Task.Run(() =>
                    {
                        RegistryUtil.DeleteSubKeyTree(a.Hive, a.SubKeyPath);
                        Logger.Log("Force-removed registry key: " + a.SubKeyPath);
                    });
                    items = await Task.Run(() => ProgramsData.FindResidualItems(a.DisplayName, a.Publisher, a.InstallLocation));
                }
                catch (Exception ex) { Mouse.OverrideCursor = null; Dialogs.ShowError(I18n.T("scan_error_title"), string.Format(I18n.T("scan_error_msg"), ex.Message)); RefreshPrograms(); return; }
                Mouse.OverrideCursor = null;
                Stats.Add(d => d.ProgramsUninstalled++);
                MergeFingerprintItems(items, a.DisplayName);
                Dialogs.ShowResidualPicker(items, a.DisplayName);
                RefreshPrograms();
            };

            btnOpenFolder.Click += (s, e) =>
            {
                var a = Selected(); if (a == null) return;
                if (!string.IsNullOrEmpty(a.InstallLocation) && System.IO.Directory.Exists(a.InstallLocation))
                    Process.Start("explorer.exe", "\"" + a.InstallLocation + "\"");
            };

            return dock;
        }

        // אם קיימת טביעת אצבע ממעקב התקנה עבור התוכנה הזו, משלב את הפריטים
        // המדויקים (לא ניחוש) עם תוצאות הסריקה ההיוריסטית הרגילה.
        private void MergeFingerprintItems(List<ResidualItem> items, string programName)
        {
            var fp = InstallMonitor.LoadFingerprintFor(programName);
            if (fp == null) return;
            var tracked = InstallMonitor.ToResidualItems(fp);
            var existingPaths = new HashSet<string>(items.Select(i => i.Type + "|" + i.Path), StringComparer.OrdinalIgnoreCase);
            foreach (var t in tracked)
            {
                if (existingPaths.Add(t.Type + "|" + t.Path)) items.Add(t);
            }
        }

        private void RefreshPrograms()
        {
            try
            {
                bool showSystem = _chkShowSystem != null && _chkShowSystem.IsChecked == true;
                _allPrograms = ProgramsData.GetInstalledPrograms().Where(p => showSystem || !p.SystemComponent).ToList();
                _programsView = new ListCollectionView(_allPrograms);
                _programsView.Filter = o =>
                {
                    var p = o as InstalledProgram;
                    if (_cmbCategory != null && _cmbCategory.SelectedIndex > 0)
                    {
                        var wanted = _cmbCategory.SelectedValue as string;
                        if (p.Category != wanted) return false;
                    }
                    if (_txtSearch != null && !string.IsNullOrWhiteSpace(_txtSearch.Text))
                    {
                        var q = _txtSearch.Text;
                        bool matches = (p.DisplayName != null && p.DisplayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                            || (p.Publisher != null && p.Publisher.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (!matches) return false;
                    }
                    return true;
                };
                _gridPrograms.ItemsSource = _programsView;
            }
            catch (Exception ex)
            {
                Dialogs.ShowError(I18n.T("generic_error_title"), ex.Message);
            }
        }

        // ---------------------------------------------------------------
        // Games
        // ---------------------------------------------------------------
        private UIElement BuildGamesTab()
        {
            var dock = new DockPanel { Background = Theme.Get("BgBrush") };

            // WrapPanel, not a plain horizontal StackPanel: at the window's own
            // declared MinWidth (960) this toolbar's controls (label+box+checkbox+
            // label+combo+button+status on the Programs tab) don't all fit on one
            // line. A non-wrapping StackPanel would render the trailing controls
            // (Refresh, the status count) past the visible window edge instead of
            // reflowing them - WrapPanel drops them to a second line instead.
            var top = new WrapPanel { Margin = new Thickness(14,10,14,6) };
            DockPanel.SetDock(top, Dock.Top);
            top.Children.Add(new TextBlock { Text = I18n.T("col_platform") + ":", VerticalAlignment = VerticalAlignment.Center, Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0,0,8,0) });
            _cmbGamePlatform = new ComboBox { Width = 200, Height = 28 };
            _cmbGamePlatform.Items.Add(I18n.T("category_all"));
            _cmbGamePlatform.SelectedIndex = 0;
            _cmbGamePlatform.SelectionChanged += (s, e) => { if (_gamesView != null) _gamesView.Refresh(); };
            top.Children.Add(_cmbGamePlatform);
            dock.Children.Add(top);

            var bottom = BottomBar();
            DockPanel.SetDock(bottom, Dock.Bottom);
            var btnOfficial = MakeButton(I18n.T("btn_official_uninstall"), "AccentButtonStyle", 200);
            var btnForce = MakeButton(I18n.T("btn_force_remove_files"), "DangerButtonStyle", 190);
            var btnFolder = MakeButton(I18n.T("btn_open_folder"), "GhostButtonStyle", 120);
            var btnRefresh = MakeButton(I18n.T("btn_refresh"), "GhostButtonStyle", 90);
            bottom.Children.Add(btnOfficial);
            bottom.Children.Add(btnForce);
            bottom.Children.Add(btnFolder);
            bottom.Children.Add(btnRefresh);
            dock.Children.Add(bottom);

            _gridGames = CreateGrid(Tuple.Create(I18n.T("col_platform"), "Source", 1.0), Tuple.Create(I18n.T("col_name"), "Name", 2.5), Tuple.Create(I18n.T("col_install_dir"), "InstallDir", 3.0));
            dock.Children.Add(_gridGames);

            btnRefresh.Click += (s, e) => RefreshGames();
            btnOfficial.Click += (s, e) =>
            {
                var g = _gridGames.SelectedItem as GameInfo; if (g == null) return;
                if (g.Source == "Steam" || g.Source == "Epic Games") GamesData.OpenOfficialUninstall(g);
                else Dialogs.Info("", string.Format(I18n.T("no_official_uninstall_msg"), g.Source));
            };
            btnForce.Click += async (s, e) =>
            {
                var g = _gridGames.SelectedItem as GameInfo; if (g == null) return;
                if (!Dialogs.Confirm(I18n.T("confirm_force_game_title"), string.Format(I18n.T("confirm_force_game_msg"), g.Name, g.Source))) return;
                Mouse.OverrideCursor = Cursors.Wait;
                bool ok = await Task.Run(() => GamesData.ForceRemove(g));
                Mouse.OverrideCursor = null;
                if (ok) { Stats.Add(d => d.GamesRemoved++); Dialogs.Info(I18n.T("generic_done_title"), I18n.T("fix_done_msg")); } else Dialogs.ShowError(I18n.T("generic_error_title"), "See log file.");
                RefreshGames();
            };
            btnFolder.Click += (s, e) =>
            {
                var g = _gridGames.SelectedItem as GameInfo; if (g == null) return;
                if (!string.IsNullOrEmpty(g.InstallDir) && System.IO.Directory.Exists(g.InstallDir)) Process.Start("explorer.exe", "\"" + g.InstallDir + "\"");
            };

            return dock;
        }

        private void RefreshGames()
        {
            try
            {
                _games = GamesData.GetAllGames();
                _gamesView = new ListCollectionView(_games);
                _cmbGamePlatform.Items.Clear();
                _cmbGamePlatform.Items.Add(I18n.T("category_all"));
                foreach (var src in _games.Select(g => g.Source).Distinct()) _cmbGamePlatform.Items.Add(src);
                _cmbGamePlatform.SelectedIndex = 0;
                _gamesView.Filter = o => _cmbGamePlatform.SelectedIndex <= 0 || (o as GameInfo).Source == (string)_cmbGamePlatform.SelectedItem;
                _gridGames.ItemsSource = _gamesView;
            }
            catch (Exception ex) { Dialogs.ShowError(I18n.T("generic_error_title"), ex.Message); }
        }

        // ---------------------------------------------------------------
        // Browser Extensions
        // ---------------------------------------------------------------
        private UIElement BuildExtensionsTab()
        {
            var dock = new DockPanel { Background = Theme.Get("BgBrush") };

            // WrapPanel, not a plain horizontal StackPanel: at the window's own
            // declared MinWidth (960) this toolbar's controls (label+box+checkbox+
            // label+combo+button+status on the Programs tab) don't all fit on one
            // line. A non-wrapping StackPanel would render the trailing controls
            // (Refresh, the status count) past the visible window edge instead of
            // reflowing them - WrapPanel drops them to a second line instead.
            var top = new WrapPanel { Margin = new Thickness(14,10,14,6) };
            DockPanel.SetDock(top, Dock.Top);
            top.Children.Add(new TextBlock { Text = I18n.T("col_browser") + ":", VerticalAlignment = VerticalAlignment.Center, Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0,0,8,0) });
            _cmbExtBrowser = new ComboBox { Width = 180, Height = 28 };
            _cmbExtBrowser.Items.Add(I18n.T("category_all"));
            _cmbExtBrowser.SelectedIndex = 0;
            _cmbExtBrowser.SelectionChanged += (s, e) => { if (_extensionsView != null) _extensionsView.Refresh(); };
            top.Children.Add(_cmbExtBrowser);
            dock.Children.Add(top);

            var bottom = BottomBar();
            DockPanel.SetDock(bottom, Dock.Bottom);
            var btnRemove = MakeButton(I18n.T("btn_remove_extension"), "DangerButtonStyle", 140);
            var btnRefresh = MakeButton(I18n.T("btn_refresh"), "GhostButtonStyle", 90);
            bottom.Children.Add(btnRemove);
            bottom.Children.Add(btnRefresh);
            dock.Children.Add(bottom);

            _gridExt = CreateGrid(Tuple.Create(I18n.T("col_browser"), "Browser", 1.0), Tuple.Create(I18n.T("col_profiles"), "ProfilesText", 1.3), Tuple.Create(I18n.T("col_name"), "Name", 2.5), Tuple.Create(I18n.T("col_version"), "Version", 1.0), Tuple.Create(I18n.T("col_ext_risk"), "RiskLevelText", 1.0));
            dock.Children.Add(_gridExt);

            btnRefresh.Click += (s, e) => RefreshExtensions();
            btnRemove.Click += (s, e) =>
            {
                var ext = _gridExt.SelectedItem as BrowserExtension; if (ext == null) return;
                if (!Dialogs.Confirm(I18n.T("confirm_remove_ext_title"), string.Format(I18n.T("confirm_remove_ext_msg"), ext.Name, ext.Browser))) return;
                bool ok = ExtensionsData.RemoveExtension(ext);
                if (ok) { Stats.Add(d => d.ExtensionsRemoved++); Dialogs.Info(I18n.T("generic_done_title"), I18n.T("fix_done_msg")); } else Dialogs.ShowError(I18n.T("generic_error_title"), "See log file.");
                RefreshExtensions();
            };

            return dock;
        }

        private void RefreshExtensions()
        {
            try
            {
                _extensions = ExtensionsData.GetAllExtensions();
                _extensionsView = new ListCollectionView(_extensions);
                _cmbExtBrowser.Items.Clear();
                _cmbExtBrowser.Items.Add(I18n.T("category_all"));
                foreach (var b in _extensions.Select(x => x.Browser).Distinct()) _cmbExtBrowser.Items.Add(b);
                _cmbExtBrowser.SelectedIndex = 0;
                _extensionsView.Filter = o => _cmbExtBrowser.SelectedIndex <= 0 || (o as BrowserExtension).Browser == (string)_cmbExtBrowser.SelectedItem;
                _gridExt.ItemsSource = _extensionsView;
            }
            catch (Exception ex) { Dialogs.ShowError(I18n.T("generic_error_title"), ex.Message); }
        }

        // ---------------------------------------------------------------
        // Startup
        // ---------------------------------------------------------------
        private UIElement BuildStartupTab()
        {
            var dock = new DockPanel { Background = Theme.Get("BgBrush") };

            // WrapPanel, not a plain horizontal StackPanel: at the window's own
            // declared MinWidth (960) this toolbar's controls (label+box+checkbox+
            // label+combo+button+status on the Programs tab) don't all fit on one
            // line. A non-wrapping StackPanel would render the trailing controls
            // (Refresh, the status count) past the visible window edge instead of
            // reflowing them - WrapPanel drops them to a second line instead.
            var top = new WrapPanel { Margin = new Thickness(14,10,14,6) };
            DockPanel.SetDock(top, Dock.Top);
            top.Children.Add(new TextBlock { Text = I18n.T("col_status") + ":", VerticalAlignment = VerticalAlignment.Center, Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0,0,8,0) });
            _cmbStartupStatus = new ComboBox { Width = 160, Height = 28 };
            _cmbStartupStatus.Items.Add(I18n.T("category_all"));
            _cmbStartupStatus.Items.Add(I18n.T("status_enabled"));
            _cmbStartupStatus.Items.Add(I18n.T("status_disabled"));
            _cmbStartupStatus.SelectedIndex = 0;
            _cmbStartupStatus.SelectionChanged += (s, e) => { if (_startupView != null) _startupView.Refresh(); };
            top.Children.Add(_cmbStartupStatus);
            dock.Children.Add(top);

            var bottom = BottomBar();
            DockPanel.SetDock(bottom, Dock.Bottom);
            var btnToggle = MakeButton(I18n.T("btn_toggle_startup"), "AccentButtonStyle", 140);
            var btnDelete = MakeButton(I18n.T("btn_delete_permanent"), "DangerButtonStyle", 150);
            var btnRefresh = MakeButton(I18n.T("btn_refresh"), "GhostButtonStyle", 90);
            bottom.Children.Add(btnToggle);
            bottom.Children.Add(btnDelete);
            bottom.Children.Add(btnRefresh);
            dock.Children.Add(bottom);

            _gridStartup = CreateGrid(Tuple.Create(I18n.T("col_status"), "StatusText", 0.7), Tuple.Create(I18n.T("col_name"), "DisplayName", 1.5), Tuple.Create(I18n.T("col_location"), "Location", 1.5), Tuple.Create(I18n.T("col_command"), "Command", 3.0));
            dock.Children.Add(_gridStartup);

            btnRefresh.Click += (s, e) => RefreshStartup();
            btnToggle.Click += (s, e) =>
            {
                var it = _gridStartup.SelectedItem as StartupItem; if (it == null) return;
                if (it.Enabled) { StartupData.Disable(it); Stats.Add(d => d.StartupItemsCleaned++); } else StartupData.Enable(it);
                RefreshStartup();
            };
            btnDelete.Click += (s, e) =>
            {
                var it = _gridStartup.SelectedItem as StartupItem; if (it == null) return;
                if (!Dialogs.Confirm(I18n.T("confirm_uninstall_title"), string.Format(I18n.T("confirm_delete_startup_msg"), it.DisplayName))) return;
                StartupData.RemovePermanently(it);
                Stats.Add(d => d.StartupItemsCleaned++);
                RefreshStartup();
            };

            return dock;
        }

        private void RefreshStartup()
        {
            try
            {
                _startupItems = StartupData.GetStartupItems();
                _startupView = new ListCollectionView(_startupItems);
                _startupView.Filter = o =>
                {
                    if (_cmbStartupStatus.SelectedIndex <= 0) return true;
                    var it = o as StartupItem;
                    bool wantEnabled = _cmbStartupStatus.SelectedIndex == 1;
                    return it.Enabled == wantEnabled;
                };
                _gridStartup.ItemsSource = _startupView;
            }
            catch (Exception ex) { Dialogs.ShowError(I18n.T("generic_error_title"), ex.Message); }
        }

        // ---------------------------------------------------------------
        // Junk Cleaner
        // ---------------------------------------------------------------
        private UIElement BuildJunkCleanerTab()
        {
            var dock = new DockPanel { Background = Theme.Get("BgBrush") };

            var intro = new TextBlock { Text = I18n.T("junk_intro"), Margin = new Thickness(14,10,14,6), TextWrapping = TextWrapping.Wrap, Foreground = Theme.Get("TextMutedBrush") };
            DockPanel.SetDock(intro, Dock.Top);
            dock.Children.Add(intro);

            var junkProgressBar = new ProgressBar { Height = 8, Minimum = 0, Maximum = 100, Margin = new Thickness(14,0,14,8), Visibility = Visibility.Collapsed };
            DockPanel.SetDock(junkProgressBar, Dock.Top);
            dock.Children.Add(junkProgressBar);

            var bottom = BottomBar();
            DockPanel.SetDock(bottom, Dock.Bottom);
            var btnScan = MakeButton(I18n.T("btn_scan_junk"), "AccentButtonStyle", 150);
            var btnClean = MakeButton(I18n.T("btn_clean_selected"), "DangerButtonStyle", 160);
            var btnUndo = MakeButton(I18n.T("btn_undo_last_cleanup"), "GhostButtonStyle", 170);
            _junkStatusLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0), Foreground = Theme.Get("TextBrush") };
            bottom.Children.Add(btnScan);
            bottom.Children.Add(btnClean);
            bottom.Children.Add(btnUndo);
            bottom.Children.Add(_junkStatusLabel);
            dock.Children.Add(bottom);

            btnUndo.Click += async (s, e) =>
            {
                var batchId = Quarantine.GetLastBatchId();
                if (batchId == null) { Dialogs.Info("", I18n.T("undo_nothing_to_undo")); return; }
                if (!Dialogs.Confirm(I18n.T("btn_undo_last_cleanup"), I18n.T("confirm_undo_msg"))) return;
                Mouse.OverrideCursor = Cursors.Wait;
                int restored = await Task.Run(() => Quarantine.RestoreBatch(batchId));
                Mouse.OverrideCursor = null;
                Dialogs.Info(I18n.T("generic_done_title"), string.Format(I18n.T("undo_restored_msg"), restored));
            };

            _junkList = new ListBox { Margin = new Thickness(14,0,14,10), Background = Theme.Get("PanelBrush"), BorderBrush = Theme.Get("BorderColorBrush") };
            var template = new DataTemplate();
            var panelFactory = new FrameworkElementFactory(typeof(DockPanel));
            var cbFactory = new FrameworkElementFactory(typeof(CheckBox));
            cbFactory.SetBinding(CheckBox.IsCheckedProperty, new Binding("Selected") { Mode = BindingMode.TwoWay });
            cbFactory.SetValue(CheckBox.ForegroundProperty, Theme.Get("TextBrush"));
            cbFactory.SetValue(CheckBox.VerticalAlignmentProperty, VerticalAlignment.Center);
            var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
            nameFactory.SetBinding(TextBlock.TextProperty, new Binding("Category.Name"));
            nameFactory.SetValue(TextBlock.ForegroundProperty, Theme.Get("TextBrush"));
            nameFactory.SetValue(TextBlock.MarginProperty, new Thickness(8,0,0,0));
            nameFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            var sizeFactory = new FrameworkElementFactory(typeof(TextBlock));
            sizeFactory.SetBinding(TextBlock.TextProperty, new Binding("Category.SizeText"));
            sizeFactory.SetValue(TextBlock.ForegroundProperty, Theme.Get("TextMutedBrush"));
            sizeFactory.SetValue(DockPanel.DockProperty, Dock.Right);
            sizeFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            panelFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(6));
            panelFactory.AppendChild(sizeFactory);
            panelFactory.AppendChild(cbFactory);
            panelFactory.AppendChild(nameFactory);
            template.VisualTree = panelFactory;
            _junkList.ItemTemplate = template;
            dock.Children.Add(_junkList);

            btnScan.Click += async (s, e) =>
            {
                _junkStatusLabel.Text = I18n.T("junk_scanning");
                btnScan.IsEnabled = false; btnClean.IsEnabled = false;
                Mouse.OverrideCursor = Cursors.Wait;
                try { _junkCategories = await Task.Run(() => JunkCleanerData.ScanAll()); }
                catch (Exception ex) { Mouse.OverrideCursor = null; btnScan.IsEnabled = true; btnClean.IsEnabled = true; Dialogs.ShowError(I18n.T("scan_error_title"), ex.Message); return; }
                Mouse.OverrideCursor = null;
                btnScan.IsEnabled = true; btnClean.IsEnabled = true;
                if (_junkCategories.Count == 0) { _junkStatusLabel.Text = I18n.T("junk_none_found"); _junkList.ItemsSource = null; return; }
                _junkStatusLabel.Text = "";
                _junkList.ItemsSource = _junkCategories.Select(c => new JunkRow { Category = c, Selected = false }).ToList();
            };

            btnClean.Click += async (s, e) =>
            {
                var rows = _junkList.ItemsSource as IEnumerable<JunkRow>;
                if (rows == null) return;
                var chosen = rows.Where(r => r.Selected).ToList();
                if (chosen.Count == 0) { Dialogs.Info("", I18n.T("no_items_selected")); return; }
                if (!Dialogs.Confirm(I18n.T("confirm_delete_residual_title"), I18n.T("confirm_clean_junk_msg"))) return;
                btnScan.IsEnabled = false; btnClean.IsEnabled = false;
                junkProgressBar.Visibility = Visibility.Visible;
                junkProgressBar.IsIndeterminate = true;
                _junkStatusLabel.Text = I18n.T("progress_creating_restore_point");
                var dispatcher = Dispatcher;
                long freed = await Task.Run(() =>
                {
                    // ניקוי זבל בטוח דרך הסגר (לא מחיקה מיידית) - לא דורש נקודת שחזור
                    // בעצמו, אבל אם התוכנה כבר יצרה אחת ב-4 השעות האחרונות (למשל
                    // מהאשף החכם) אין תקורה נוספת כלל.
                    RestorePoint.Create("OptiGuard - before Junk Cleaner");
                    dispatcher.BeginInvoke((Action)(() => junkProgressBar.IsIndeterminate = false));
                    var batchId = Quarantine.NewBatchId();
                    long total = 0;
                    for (int i = 0; i < chosen.Count; i++)
                    {
                        var idx = i;
                        dispatcher.BeginInvoke((Action)(() =>
                        {
                            junkProgressBar.Value = (idx * 100.0) / chosen.Count;
                            _junkStatusLabel.Text = string.Format(I18n.T("progress_cleaning_x_of_y"), idx + 1, chosen.Count, chosen[idx].Category.Name);
                        }));
                        total += JunkCleanerData.Clean(chosen[idx].Category, batchId);
                    }
                    return total;
                });
                junkProgressBar.Value = 100;
                junkProgressBar.Visibility = Visibility.Collapsed;
                btnScan.IsEnabled = true; btnClean.IsEnabled = true;
                Stats.Add(d => { d.JunkBytesFreed += freed; d.JunkCleanupRuns++; });
                Dialogs.Info(I18n.T("generic_done_title"), string.Format(I18n.T("junk_cleaned_msg"), JunkCleanerData.FormatSize(freed)) + " " + I18n.T("junk_undo_hint"));
                btnScan.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };

            return dock;
        }

        public class JunkRow : INotifyPropertyChanged
        {
            private bool _selected;
            public JunkCategory Category { get; set; }
            public bool Selected { get { return _selected; } set { _selected = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Selected")); } }
            public event PropertyChangedEventHandler PropertyChanged;
        }

        // ---------------------------------------------------------------
        // Disk Health
        // ---------------------------------------------------------------
        private UIElement BuildDiskHealthTab()
        {
            var dock = new DockPanel { Background = Theme.Get("BgBrush") };

            var bottom = BottomBar();
            DockPanel.SetDock(bottom, Dock.Bottom);
            var btnScanDrive = MakeButton(I18n.T("btn_scan_drive"), "AccentButtonStyle", 220);
            var btnRefresh = MakeButton(I18n.T("btn_refresh"), "GhostButtonStyle", 90);
            bottom.Children.Add(btnScanDrive);
            bottom.Children.Add(btnRefresh);
            dock.Children.Add(bottom);

            _gridDisks = CreateGrid(
                Tuple.Create(I18n.T("col_drive"), "Drive", 1.6),
                Tuple.Create(I18n.T("col_total"), "TotalText", 1.0),
                Tuple.Create(I18n.T("col_free"), "FreeText", 1.0),
                Tuple.Create(I18n.T("col_used_pct"), "UsedPercentText", 0.8),
                Tuple.Create(I18n.T("col_health"), "Health", 1.0));
            dock.Children.Add(_gridDisks);

            Action refreshDisks = () => { try { _gridDisks.ItemsSource = DiskHealthData.GetDrives(); } catch (Exception ex) { Dialogs.ShowError(I18n.T("generic_error_title"), ex.Message); } };
            btnRefresh.Click += (s, e) => refreshDisks();
            btnScanDrive.Click += async (s, e) =>
            {
                var d = _gridDisks.SelectedItem as DiskRow; if (d == null) return;
                var letter = d.Drive.Split(' ')[0];
                if (!Dialogs.Confirm(I18n.T("btn_scan_drive"), string.Format(I18n.T("confirm_scan_drive_msg"), letter))) return;
                btnScanDrive.IsEnabled = false;
                btnScanDrive.Content = string.Format(I18n.T("disk_scan_running"), letter);
                var result = await Task.Run(() => DiskHealthData.ScanDrive(letter));
                btnScanDrive.IsEnabled = true;
                btnScanDrive.Content = I18n.T("btn_scan_drive");
                Dialogs.ShowTextReport(string.Format(I18n.T("disk_scan_result_title"), letter), result);
            };

            refreshDisks();
            return dock;
        }

        // ---------------------------------------------------------------
        // Disk Space Analyzer
        // ---------------------------------------------------------------
        private UIElement BuildDiskSpaceAnalyzerTab()
        {
            var dock = new DockPanel { Background = Theme.Get("BgBrush") };

            var intro = new TextBlock { Text = I18n.T("diskspace_intro"), Margin = new Thickness(14,10,14,6), TextWrapping = TextWrapping.Wrap, Foreground = Theme.Get("TextMutedBrush") };
            DockPanel.SetDock(intro, Dock.Top);
            dock.Children.Add(intro);

            var top = new WrapPanel { Margin = new Thickness(14,0,14,6) };
            DockPanel.SetDock(top, Dock.Top);
            _cmbSpaceDrive = new ComboBox { Width = 100, Height = 28 };
            foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady)) _cmbSpaceDrive.Items.Add(d.Name);
            var btnGoUp = MakeButton("⬆ " + I18n.T("btn_go_up"), "GhostButtonStyle", 90);
            top.Children.Add(_cmbSpaceDrive);
            top.Children.Add(btnGoUp);
            _spacePathLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14,0,0,0), Foreground = Theme.Get("TextBrush"), FontWeight = FontWeights.SemiBold };
            top.Children.Add(_spacePathLabel);
            dock.Children.Add(top);

            var bottom = BottomBar();
            DockPanel.SetDock(bottom, Dock.Bottom);
            var btnLargest = MakeButton(I18n.T("btn_show_largest_files"), "AccentButtonStyle", 260);
            var spaceStatusLbl = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0), Foreground = Theme.Get("TextBrush") };
            bottom.Children.Add(btnLargest);
            bottom.Children.Add(spaceStatusLbl);
            dock.Children.Add(bottom);

            var spaceLoadingBar = new ProgressBar { Height = 4, IsIndeterminate = true, Margin = new Thickness(14,0,14,4), Visibility = Visibility.Collapsed };
            DockPanel.SetDock(spaceLoadingBar, Dock.Top);
            dock.Children.Add(spaceLoadingBar);

            _gridSpace = CreateGrid(
                Tuple.Create("", "Icon", 0.3),
                Tuple.Create(I18n.T("col_space_name"), "Name", 2.2),
                Tuple.Create(I18n.T("col_space_size"), "SizeText", 1.0),
                Tuple.Create(I18n.T("col_space_percent"), "PercentText", 0.8));
            dock.Children.Add(_gridSpace);

            Action<string> loadPath = null;
            loadPath = async path =>
            {
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
                _spaceCurrentPath = path;
                _spacePathLabel.Text = path;
                _gridSpace.ItemsSource = null;
                // Scanning a large/slow folder can take a few seconds - a thin
                // indeterminate bar plus status text instead of just an empty
                // grid, so it reads as "working" rather than "broken/empty"
                // (section 18.5).
                spaceStatusLbl.Text = I18n.T("diskspace_scanning");
                spaceLoadingBar.Visibility = Visibility.Visible;
                Mouse.OverrideCursor = Cursors.Wait;
                _spaceEntries = await Task.Run(() => DiskSpaceAnalyzerData.AnalyzeFolder(path));
                Mouse.OverrideCursor = null;
                spaceLoadingBar.Visibility = Visibility.Collapsed;
                spaceStatusLbl.Text = "";
                _gridSpace.ItemsSource = _spaceEntries;
            };

            _cmbSpaceDrive.SelectionChanged += (s, e) => { if (_cmbSpaceDrive.SelectedItem != null) loadPath((string)_cmbSpaceDrive.SelectedItem); };
            btnGoUp.Click += (s, e) =>
            {
                if (string.IsNullOrEmpty(_spaceCurrentPath)) return;
                var parent = Directory.GetParent(_spaceCurrentPath.TrimEnd('\\'));
                if (parent != null) loadPath(parent.FullName);
            };
            _gridSpace.MouseDoubleClick += (s, e) =>
            {
                var sel = _gridSpace.SelectedItem as SpaceEntry;
                if (sel != null && sel.IsDirectory) loadPath(sel.Path);
            };

            btnLargest.Click += async (s, e) =>
            {
                if (string.IsNullOrEmpty(_spaceCurrentPath)) return;
                btnLargest.IsEnabled = false;
                var dispatcher = Dispatcher;
                var largest = await Task.Run(() => DiskSpaceAnalyzerData.GetLargestFiles(_spaceCurrentPath, 50, count =>
                {
                    dispatcher.BeginInvoke((Action)(() => spaceStatusLbl.Text = string.Format(I18n.T("largest_files_scanning"), count)));
                }));
                btnLargest.IsEnabled = true;
                spaceStatusLbl.Text = "";
                Dialogs.ShowLargestFiles(largest);
            };

            if (_cmbSpaceDrive.Items.Count > 0)
            {
                _cmbSpaceDrive.SelectedIndex = 0;
            }

            return dock;
        }

        // ---------------------------------------------------------------
        // Duplicate File Finder
        // ---------------------------------------------------------------
        private UIElement BuildDuplicateFinderTab()
        {
            var dock = new DockPanel { Background = Theme.Get("BgBrush") };

            var intro = new TextBlock { Text = I18n.T("dup_intro"), Margin = new Thickness(14,10,14,6), TextWrapping = TextWrapping.Wrap, Foreground = Theme.Get("TextMutedBrush") };
            DockPanel.SetDock(intro, Dock.Top);
            dock.Children.Add(intro);

            var top = new WrapPanel { Margin = new Thickness(14,0,14,6) };
            DockPanel.SetDock(top, Dock.Top);
            top.Children.Add(new TextBlock { Text = I18n.T("dup_folder_label"), VerticalAlignment = VerticalAlignment.Center, Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0,0,8,0) });
            _txtDupFolder = new TextBox { Width = 400, Height = 28, Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), VerticalContentAlignment = VerticalAlignment.Center };
            top.Children.Add(_txtDupFolder);
            var btnBrowse = MakeButton(I18n.T("btn_browse"), "GhostButtonStyle", 100);
            btnBrowse.Margin = new Thickness(8,0,0,0);
            top.Children.Add(btnBrowse);
            dock.Children.Add(top);

            var bottom = BottomBar();
            DockPanel.SetDock(bottom, Dock.Bottom);
            var btnScanDup = MakeButton(I18n.T("btn_scan_duplicates"), "AccentButtonStyle", 170);
            var btnCleanDup = MakeButton(I18n.T("btn_clean_duplicates_selected"), "DangerButtonStyle", 210);
            var dupStatusLbl = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0), Foreground = Theme.Get("TextBrush") };
            bottom.Children.Add(btnScanDup);
            bottom.Children.Add(btnCleanDup);
            bottom.Children.Add(dupStatusLbl);
            dock.Children.Add(bottom);

            var scroll = new ScrollViewer { Margin = new Thickness(14,0,14,10), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _dupResultsPanel = new StackPanel();
            scroll.Content = _dupResultsPanel;
            dock.Children.Add(scroll);

            btnBrowse.Click += (s, e) =>
            {
                using (var fbd = new System.Windows.Forms.FolderBrowserDialog { Description = I18n.T("dup_folder_label"), SelectedPath = _txtDupFolder.Text })
                {
                    if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK) _txtDupFolder.Text = fbd.SelectedPath;
                }
            };

            Action renderGroups = () =>
            {
                _dupResultsPanel.Children.Clear();
                if (_dupGroups.Count == 0)
                {
                    _dupResultsPanel.Children.Add(new TextBlock { Text = I18n.T("dup_none_found"), Foreground = Theme.Get("TextMutedBrush") });
                    return;
                }
                foreach (var group in _dupGroups)
                {
                    var card = new Border { Background = Theme.Get("PanelBrush"), BorderBrush = Theme.Get("BorderColorBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(0,0,0,12) };
                    var inner = new StackPanel();
                    inner.Children.Add(new TextBlock
                    {
                        Text = string.Format(I18n.T("dup_group_header"), group.Files.Count, group.SizeText, group.WastedText),
                        FontWeight = FontWeights.Bold, Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0,0,0,6)
                    });
                    foreach (var f in group.Files)
                    {
                        var row = new DockPanel { Margin = new Thickness(0,2,0,2) };
                        var chk = new CheckBox { IsChecked = f.IsSelected, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) };
                        var capturedF = f;
                        chk.Checked += (s, e) => capturedF.IsSelected = true;
                        chk.Unchecked += (s, e) => capturedF.IsSelected = false;
                        var dateTxt = new TextBlock { Text = f.LastModifiedText, Foreground = Theme.Get("TextMutedBrush"), Width = 90 };
                        DockPanel.SetDock(dateTxt, Dock.Right);
                        row.Children.Add(chk);
                        row.Children.Add(dateTxt);
                        row.Children.Add(new TextBlock { Text = f.Path, Foreground = Theme.Get("TextBrush"), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
                        inner.Children.Add(row);
                    }
                    card.Child = inner;
                    _dupResultsPanel.Children.Add(card);
                }
            };

            btnScanDup.Click += async (s, e) =>
            {
                var folder = _txtDupFolder.Text;
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) { Dialogs.Info("", I18n.T("no_leftovers_found")); return; }
                btnScanDup.IsEnabled = false; btnCleanDup.IsEnabled = false;
                dupStatusLbl.Text = I18n.T("dup_scanning");
                var dispatcher = Dispatcher;
                _dupGroups = await Task.Run(() => DuplicateFileFinderData.Scan(folder,
                    enumerated => dispatcher.BeginInvoke((Action)(() => dupStatusLbl.Text = string.Format(I18n.T("dup_enumerating_progress"), enumerated))),
                    (done, total) => dispatcher.BeginInvoke((Action)(() => dupStatusLbl.Text = string.Format(I18n.T("dup_hashing_progress"), done, total)))));
                btnScanDup.IsEnabled = true; btnCleanDup.IsEnabled = true;
                dupStatusLbl.Text = "";
                renderGroups();
            };

            btnCleanDup.Click += async (s, e) =>
            {
                var selected = _dupGroups.SelectMany(g => g.Files).Where(f => f.IsSelected).ToList();
                if (selected.Count == 0) { Dialogs.Info("", I18n.T("no_items_selected")); return; }
                if (!Dialogs.Confirm(I18n.T("btn_clean_duplicates_selected"), string.Format(I18n.T("confirm_clean_duplicates_msg"), selected.Count))) return;
                btnScanDup.IsEnabled = false; btnCleanDup.IsEnabled = false;
                var batchId = Quarantine.NewBatchId();
                long freed = await Task.Run(() => DuplicateFileFinderData.RemoveSelected(selected, batchId));
                btnScanDup.IsEnabled = true; btnCleanDup.IsEnabled = true;
                Stats.Add(d => d.JunkBytesFreed += freed);
                Dialogs.Info(I18n.T("generic_done_title"), string.Format(I18n.T("dup_cleaned_msg"), JunkCleanerData.FormatSize(freed)) + " " + I18n.T("junk_undo_hint"));
                btnScanDup.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };

            return dock;
        }

        // ---------------------------------------------------------------
        // System Info
        // ---------------------------------------------------------------
        private UIElement BuildSystemInfoTab()
        {
            var scroll = new ScrollViewer { Background = Theme.Get("BgBrush") };
            var panel = new StackPanel { Margin = new Thickness(20) };
            scroll.Content = panel;

            Action<string, List<InfoRow>> addSection = (title, rows) =>
            {
                panel.Children.Add(SectionLabel(title));
                var card = new Border { Background = Theme.Get("PanelBrush"), BorderBrush = Theme.Get("BorderColorBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(0,0,0,18) };
                var inner = new StackPanel();
                foreach (var r in rows)
                {
                    var row = new DockPanel { Margin = new Thickness(0,3,0,3) };
                    row.Children.Add(new TextBlock { Text = r.Label, FontWeight = FontWeights.SemiBold, Foreground = Theme.Get("TextBrush"), Width = 260 });
                    row.Children.Add(new TextBlock { Text = r.Value, Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap });
                    inner.Children.Add(row);
                }
                card.Child = inner;
                panel.Children.Add(card);
            };

            try
            {
                addSection(I18n.T("si_section_os"), SystemInfoData.GetOsInfo());
                addSection(I18n.T("si_section_cpu"), SystemInfoData.GetCpuInfo());
                addSection(I18n.T("si_section_ram"), SystemInfoData.GetMemoryInfo());
                addSection(I18n.T("si_section_gpu"), SystemInfoData.GetGpuInfo());
                addSection(I18n.T("si_section_motherboard"), SystemInfoData.GetMotherboardInfo());
                addSection(I18n.T("si_section_disks"), SystemInfoData.GetDisksInfo());
            }
            catch (Exception ex) { panel.Children.Add(new TextBlock { Text = ex.Message, Foreground = Theme.Get("DangerBrush") }); }

            panel.Children.Add(SectionLabel(I18n.T("si_section_drivers")));
            var driverCard = new Border { Background = Theme.Get("PanelBrush"), BorderBrush = Theme.Get("BorderColorBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(0,0,0,18) };
            var driverStack = new StackPanel();
            driverStack.Children.Add(new TextBlock { Text = I18n.T("driver_health_note"), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,10), FontSize = 12 });
            var driverListHost = new StackPanel();
            driverStack.Children.Add(driverListHost);
            var btnOpenDevMgr = MakeButton(I18n.T("btn_open_device_manager"), "GhostButtonStyle", 190);
            btnOpenDevMgr.HorizontalAlignment = HorizontalAlignment.Left;
            btnOpenDevMgr.Margin = new Thickness(0,10,0,0);
            btnOpenDevMgr.Click += (s, e) => { try { Process.Start("devmgmt.msc"); } catch { } };
            driverStack.Children.Add(btnOpenDevMgr);
            driverCard.Child = driverStack;
            panel.Children.Add(driverCard);

            Task.Run(() => DriverHealthData.GetProblemDevices()).ContinueWith(t =>
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    var issues = t.Result;
                    if (issues.Count == 0)
                    {
                        driverListHost.Children.Add(new TextBlock { Text = I18n.T("driver_issue_none"), Foreground = Theme.Get("AccentBrush") });
                    }
                    else
                    {
                        foreach (var issue in issues)
                        {
                            var row = new DockPanel { Margin = new Thickness(0,2,0,2) };
                            row.Children.Add(new TextBlock { Text = issue.DeviceClass, Foreground = Theme.Get("TextMutedBrush"), Width = 140 });
                            row.Children.Add(new TextBlock { Text = issue.DeviceName, Foreground = Theme.Get("DangerBrush"), TextWrapping = TextWrapping.Wrap });
                            driverListHost.Children.Add(row);
                        }
                    }
                }));
            });

            var btnCopy = MakeButton(I18n.T("btn_copy_report"), "AccentButtonStyle", 180);
            btnCopy.HorizontalAlignment = HorizontalAlignment.Left;
            btnCopy.Click += (s, e) =>
            {
                try { Clipboard.SetText(SystemInfoData.BuildFullReport()); Dialogs.Info(I18n.T("generic_done_title"), I18n.T("report_copied_msg")); } catch { }
            };
            panel.Children.Add(btnCopy);

            return scroll;
        }

        // ---------------------------------------------------------------
        // Quick Fixes
        // ---------------------------------------------------------------
        private UIElement BuildQuickFixesTab()
        {
            var scroll = new ScrollViewer { Background = Theme.Get("BgBrush") };
            var panel = new StackPanel { Margin = new Thickness(20) };
            scroll.Content = panel;

            panel.Children.Add(new TextBlock { Text = I18n.T("fixes_intro"), TextWrapping = TextWrapping.Wrap, Foreground = Theme.Get("TextMutedBrush"), Margin = new Thickness(0,0,0,16) });

            foreach (var fix in QuickFixesData.GetActions())
            {
                var card = new Border { Background = Theme.Get("PanelBrush"), BorderBrush = Theme.Get("BorderColorBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(0,0,0,12) };
                var row = new DockPanel();
                var textStack = new StackPanel();
                textStack.Children.Add(new TextBlock { Text = fix.Name, FontWeight = FontWeights.Bold, FontSize = 13.5, Foreground = Theme.Get("TextBrush") });
                textStack.Children.Add(new TextBlock { Text = fix.Description, Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,4,0,0) });
                var btnRun = MakeButton(I18n.T("btn_run_fix"), "AccentButtonStyle", 100);
                DockPanel.SetDock(btnRun, Dock.Right);
                btnRun.VerticalAlignment = VerticalAlignment.Center;
                var capturedFix = fix;
                btnRun.Click += (s, e) =>
                {
                    if (!Dialogs.Confirm(capturedFix.Name, string.Format(I18n.T("confirm_run_fix_msg"), capturedFix.Name, capturedFix.Description))) return;
                    try { capturedFix.Run(); } catch (Exception ex) { Dialogs.ShowError(I18n.T("generic_error_title"), ex.Message); }
                };
                row.Children.Add(btnRun);
                row.Children.Add(textStack);
                card.Child = row;
                panel.Children.Add(card);
            }

            return scroll;
        }

        // ---------------------------------------------------------------
        // Software Updates (winget)
        // ---------------------------------------------------------------
        private UIElement BuildSoftwareUpdatesTab()
        {
            var dock = new DockPanel { Background = Theme.Get("BgBrush") };

            var intro = new TextBlock { Text = I18n.T("updates_intro"), Margin = new Thickness(14,10,14,10), TextWrapping = TextWrapping.Wrap, Foreground = Theme.Get("TextMutedBrush") };
            DockPanel.SetDock(intro, Dock.Top);
            dock.Children.Add(intro);

            var bottom = BottomBar();
            DockPanel.SetDock(bottom, Dock.Bottom);
            // עדכון תוכנה הוא לא הרסני - לא נכון סמנטית לצבוע אותו באדום "מסוכן"
            // (שמור לפעולות בלתי הפיכות כמו מחיקה/הסרה). ירוק = פעולה חיובית.
            var btnScanUpdates = MakeButton(I18n.T("btn_scan_updates"), "GhostButtonStyle", 170);
            var btnUpdateSelected = MakeButton(I18n.T("btn_update_selected_sw"), "AccentButtonStyle", 170);
            var updatesStatusLbl = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0), Foreground = Theme.Get("TextBrush") };
            bottom.Children.Add(btnScanUpdates);
            bottom.Children.Add(btnUpdateSelected);
            bottom.Children.Add(updatesStatusLbl);
            dock.Children.Add(bottom);

            var gridUpdates = new DataGrid
            {
                Style = (Style)Theme.GetStyle("ModernDataGridStyle"), ColumnHeaderStyle = (Style)Theme.GetStyle("ModernColumnHeaderStyle"),
                CellStyle = (Style)Theme.GetStyle("ModernCellStyle"), RowStyle = (Style)Theme.GetStyle("ModernRowStyle")
            };
            gridUpdates.Columns.Add(CreateCheckboxColumn("IsChecked"));
            gridUpdates.Columns.Add(new DataGridTextColumn { Header = I18n.T("col_name"), Binding = new Binding("Name"), Width = new DataGridLength(2.4, DataGridLengthUnitType.Star) });
            gridUpdates.Columns.Add(new DataGridTextColumn { Header = I18n.T("col_update_current"), Binding = new Binding("CurrentVersion"), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
            gridUpdates.Columns.Add(new DataGridTextColumn { Header = I18n.T("col_update_available"), Binding = new Binding("AvailableVersion"), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
            gridUpdates.Columns.Add(new DataGridTextColumn { Header = I18n.T("col_update_source"), Binding = new Binding("Source"), Width = new DataGridLength(0.9, DataGridLengthUnitType.Star) });
            dock.Children.Add(gridUpdates);

            List<UpdatableRow> updateRows = new List<UpdatableRow>();

            btnScanUpdates.Click += async (s, e) =>
            {
                btnScanUpdates.IsEnabled = false; btnUpdateSelected.IsEnabled = false;
                updatesStatusLbl.Text = I18n.T("updates_scanning_msg");
                Mouse.OverrideCursor = Cursors.Wait;
                try
                {
                    bool available = await Task.Run(() => SoftwareUpdateChecker.IsWingetAvailable());
                    if (!available)
                    {
                        Mouse.OverrideCursor = null; updatesStatusLbl.Text = "";
                        Dialogs.Info("", I18n.T("winget_not_found_msg"));
                        return;
                    }
                    var updates = await Task.Run(() => SoftwareUpdateChecker.GetAvailableUpdates());
                    Mouse.OverrideCursor = null;
                    if (updates.Count == 0) { updatesStatusLbl.Text = I18n.T("updates_none_found_msg"); gridUpdates.ItemsSource = null; return; }
                    updatesStatusLbl.Text = "";
                    updateRows = updates.Select(u => new UpdatableRow { Program = u, IsChecked = false }).ToList();
                    gridUpdates.ItemsSource = updateRows;
                }
                catch (Exception ex) { Mouse.OverrideCursor = null; updatesStatusLbl.Text = ""; Dialogs.ShowError(I18n.T("generic_error_title"), ex.Message); }
                finally { btnScanUpdates.IsEnabled = true; btnUpdateSelected.IsEnabled = true; }
            };

            btnUpdateSelected.Click += async (s, e) =>
            {
                var chosen = updateRows.Where(r => r.IsChecked).ToList();
                if (chosen.Count == 0) { Dialogs.Info("", I18n.T("no_items_selected")); return; }
                if (!Dialogs.Confirm(I18n.T("btn_update_selected_sw"), string.Format(I18n.T("confirm_update_selected_msg"), chosen.Count))) return;
                btnScanUpdates.IsEnabled = false; btnUpdateSelected.IsEnabled = false;
                Mouse.OverrideCursor = Cursors.Wait;
                int success = await Task.Run(() =>
                {
                    RestorePoint.Create("OptiGuard - before software updates");
                    int count = 0;
                    foreach (var r in chosen)
                    {
                        string output;
                        if (SoftwareUpdateChecker.UpdateOne(r.Program.Id, out output)) count++;
                    }
                    return count;
                });
                Mouse.OverrideCursor = null;
                btnScanUpdates.IsEnabled = true; btnUpdateSelected.IsEnabled = true;
                Dialogs.Info(I18n.T("generic_done_title"), string.Format(I18n.T("update_result_summary_msg"), success, chosen.Count));
                btnScanUpdates.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };

            return dock;
        }

        // ---------------------------------------------------------------
        // Settings
        // ---------------------------------------------------------------
        // ---------------------------------------------------------------
        // Security Center (Windows Defender / Firewall / Windows Update - real status, real scan)
        // ---------------------------------------------------------------
        private UIElement BuildSecurityCenterTab()
        {
            var scroll = new ScrollViewer { Background = Theme.Get("BgBrush") };
            var panel = new StackPanel { Margin = new Thickness(20) };
            scroll.Content = panel;

            panel.Children.Add(new TextBlock { Text = I18n.T("sec_intro"), TextWrapping = TextWrapping.Wrap, Foreground = Theme.Get("TextMutedBrush"), Margin = new Thickness(0,0,0,16) });

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,10) };
            var btnScan = MakeButton(I18n.T("btn_run_quick_scan"), "AccentButtonStyle", 220);
            var btnRefresh = MakeButton(I18n.T("btn_refresh"), "GhostButtonStyle", 90);
            btnRow.Children.Add(btnScan);
            btnRow.Children.Add(btnRefresh);
            panel.Children.Add(btnRow);

            // סריקה מהירה אמיתית של Defender יכולה לקחת כמה דקות - בלי אינדיקציה
            // חיה זה בדיוק נראה כאילו התוכנה נתקעה. פס התקדמות לא-קבוע + טיימר
            // שסופר שניות מוכיחים ברציפות שהתהליך עדיין חי.
            var scanProgressBar = new ProgressBar { Height = 6, IsIndeterminate = true, Margin = new Thickness(0,0,0,4), Visibility = Visibility.Collapsed };
            var scanElapsedLbl = new TextBlock { Foreground = Theme.Get("TextMutedBrush"), FontSize = 12, Margin = new Thickness(0,0,0,16), Visibility = Visibility.Collapsed };
            panel.Children.Add(scanProgressBar);
            panel.Children.Add(scanElapsedLbl);

            var contentHost = new StackPanel();
            panel.Children.Add(contentHost);

            Action<string, List<Tuple<string, string, bool?>>> addSection = (title, rows) =>
            {
                contentHost.Children.Add(SectionLabel(title));
                var card = new Border { Background = Theme.Get("PanelBrush"), BorderBrush = Theme.Get("BorderColorBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(0,0,0,18) };
                var inner = new StackPanel();
                foreach (var r in rows)
                {
                    var row = new DockPanel { Margin = new Thickness(0,3,0,3) };
                    row.Children.Add(new TextBlock { Text = r.Item1, FontWeight = FontWeights.SemiBold, Foreground = Theme.Get("TextBrush"), Width = 260 });
                    var valueText = r.Item2;
                    var valueBrush = Theme.Get("TextMutedBrush");
                    if (r.Item3 == true) { valueText = "✓ " + valueText; valueBrush = Theme.Get("AccentBrush"); }
                    else if (r.Item3 == false) { valueText = "⚠ " + valueText; valueBrush = Theme.Get("DangerBrush"); }
                    row.Children.Add(new TextBlock { Text = valueText, Foreground = valueBrush, TextWrapping = TextWrapping.Wrap, FontWeight = r.Item3 != null ? FontWeights.SemiBold : FontWeights.Normal });
                    inner.Children.Add(row);
                }
                card.Child = inner;
                contentHost.Children.Add(card);
            };

            Action refresh = () =>
            {
                contentHost.Children.Clear();
                try
                {
                    var defender = SecurityData.GetDefenderStatus();
                    var defRows = new List<Tuple<string, string, bool?>>();
                    if (!defender.Available)
                    {
                        defRows.Add(Tuple.Create(I18n.T("sec_defender_service"), I18n.T("sec_unavailable"), (bool?)null));
                    }
                    else
                    {
                        defRows.Add(Tuple.Create(I18n.T("sec_defender_service"), defender.ServiceEnabled ? I18n.T("sec_on") : I18n.T("sec_off"), (bool?)defender.ServiceEnabled));
                        defRows.Add(Tuple.Create(I18n.T("sec_defender_rtp"), defender.RealTimeProtection ? I18n.T("sec_on") : I18n.T("sec_off"), (bool?)defender.RealTimeProtection));
                        defRows.Add(Tuple.Create(I18n.T("sec_defender_sig"), defender.SignatureLastUpdated ?? I18n.T("sec_never"), (bool?)null));
                        defRows.Add(Tuple.Create(I18n.T("sec_defender_quick_scan"), defender.LastQuickScan ?? I18n.T("sec_never"), (bool?)null));
                        defRows.Add(Tuple.Create(I18n.T("sec_defender_full_scan"), defender.LastFullScan ?? I18n.T("sec_never"), (bool?)null));
                    }
                    addSection(I18n.T("sec_section_defender"), defRows);

                    var fwRows = new List<Tuple<string, string, bool?>>();
                    var fw = SecurityData.GetFirewallStatus();
                    if (fw.Count == 0) fwRows.Add(Tuple.Create(I18n.T("sec_fw_status"), I18n.T("sec_unavailable"), (bool?)null));
                    else foreach (var p in fw) fwRows.Add(Tuple.Create(p.Name, p.Enabled ? I18n.T("sec_on") : I18n.T("sec_off"), (bool?)p.Enabled));
                    addSection(I18n.T("sec_section_firewall"), fwRows);

                    var wu = SecurityData.GetWindowsUpdateStatus();
                    var wuRows = new List<Tuple<string, string, bool?>>
                    {
                        Tuple.Create(I18n.T("sec_wu_last_search"), wu.LastSearch ?? I18n.T("sec_never"), (bool?)null),
                        Tuple.Create(I18n.T("sec_wu_last_install"), wu.LastInstall ?? I18n.T("sec_never"), (bool?)null),
                    };
                    addSection(I18n.T("sec_section_updates"), wuRows);
                }
                catch (Exception ex) { contentHost.Children.Add(new TextBlock { Text = ex.Message, Foreground = Theme.Get("DangerBrush") }); }
            };

            btnRefresh.Click += (s, e) => refresh();
            btnScan.Click += async (s, e) =>
            {
                if (!Dialogs.Confirm(I18n.T("btn_run_quick_scan"), I18n.T("sec_scan_running"))) return;
                btnScan.IsEnabled = false;
                var original = btnScan.Content;
                btnScan.Content = I18n.T("sec_scan_running");
                scanProgressBar.Visibility = Visibility.Visible;
                scanElapsedLbl.Visibility = Visibility.Visible;

                var startedAt = DateTime.Now;
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                timer.Tick += (ts, te) =>
                {
                    var elapsed = (int)(DateTime.Now - startedAt).TotalSeconds;
                    scanElapsedLbl.Text = string.Format(I18n.T("sec_scan_elapsed"), elapsed / 60, elapsed % 60);
                };
                timer.Start();

                var result = await Task.Run(() =>
                {
                    string error;
                    bool success = SecurityData.RunQuickScan(out error);
                    return Tuple.Create(success, error);
                });

                timer.Stop();
                scanProgressBar.Visibility = Visibility.Collapsed;
                scanElapsedLbl.Visibility = Visibility.Collapsed;
                btnScan.IsEnabled = true;
                btnScan.Content = original;
                if (result.Item2 == "not_found") Dialogs.ShowError(I18n.T("sec_scan_done_title"), I18n.T("sec_scan_not_found"));
                else Dialogs.Info(I18n.T("sec_scan_done_title"), result.Item1 ? I18n.T("sec_scan_done_ok") : I18n.T("sec_scan_done_fail"));
                refresh();
            };

            refresh();
            return scroll;
        }

        // ---------------------------------------------------------------
        // Privacy Dashboard (real Windows privacy registry settings)
        // ---------------------------------------------------------------
        private UIElement BuildPrivacyTab()
        {
            var scroll = new ScrollViewer { Background = Theme.Get("BgBrush") };
            var panel = new StackPanel { Margin = new Thickness(20) };
            scroll.Content = panel;

            panel.Children.Add(new TextBlock { Text = I18n.T("privacy_intro"), TextWrapping = TextWrapping.Wrap, Foreground = Theme.Get("TextMutedBrush"), Margin = new Thickness(0,0,0,10) });
            var statusLbl = new TextBlock { Foreground = Theme.Get("AccentBrush"), Margin = new Thickness(0,0,0,10), FontWeight = FontWeights.SemiBold };
            panel.Children.Add(statusLbl);

            var trackingKeys = new HashSet<string> { "advertising_id", "tailored_experiences", "diagnostic_data", "app_suggestions", "feedback_requests" };

            foreach (var toggle in PrivacyData.GetAll())
            {
                var card = new Border { Background = Theme.Get("PanelBrush"), BorderBrush = Theme.Get("BorderColorBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(0,0,0,12) };
                var stack = new StackPanel();
                var headerRow = new StackPanel { Orientation = Orientation.Horizontal };
                var chk = new CheckBox { Content = I18n.T(toggle.LabelKey), Style = (Style)Theme.GetStyle("CardCheckBoxStyle"), IsChecked = toggle.CurrentState };
                headerRow.Children.Add(chk);
                TextBlock badge = null;
                if (trackingKeys.Contains(toggle.Key))
                {
                    badge = new TextBlock
                    {
                        Text = toggle.CurrentState ? "⚠ " + I18n.T("sec_on") : "✓ " + I18n.T("sec_off"),
                        Foreground = toggle.CurrentState ? Theme.Get("DangerBrush") : Theme.Get("AccentBrush"),
                        FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0)
                    };
                    headerRow.Children.Add(badge);
                }
                stack.Children.Add(headerRow);
                stack.Children.Add(new TextBlock { Text = I18n.T(toggle.DescKey), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24,4,0,0) });
                card.Child = stack;
                panel.Children.Add(card);

                var capturedKey = toggle.Key;
                var capturedBadge = badge;
                Action<bool> onToggle = isOn =>
                {
                    PrivacyData.SetToggle(capturedKey, isOn);
                    statusLbl.Text = I18n.T("privacy_applied_msg");
                    if (capturedBadge != null)
                    {
                        capturedBadge.Text = isOn ? "⚠ " + I18n.T("sec_on") : "✓ " + I18n.T("sec_off");
                        capturedBadge.Foreground = isOn ? Theme.Get("DangerBrush") : Theme.Get("AccentBrush");
                    }
                };
                chk.Checked += (s, e) => onToggle(true);
                chk.Unchecked += (s, e) => onToggle(false);
            }

            return scroll;
        }

        // ---------------------------------------------------------------
        // Registry Ghost-Entry Cleaner (narrow, evidence-based, backed up before removal)
        // ---------------------------------------------------------------
        private UIElement BuildRegistryCleanerTab()
        {
            var dock = new DockPanel { Background = Theme.Get("BgBrush") };

            var intro = new TextBlock { Text = I18n.T("regclean_intro"), Margin = new Thickness(14,10,14,0), TextWrapping = TextWrapping.Wrap, Foreground = Theme.Get("TextMutedBrush") };
            DockPanel.SetDock(intro, Dock.Top);
            dock.Children.Add(intro);

            var regProgressBar = new ProgressBar { Height = 8, Minimum = 0, Maximum = 100, Margin = new Thickness(14,0,14,8), Visibility = Visibility.Collapsed };
            DockPanel.SetDock(regProgressBar, Dock.Top);
            dock.Children.Add(regProgressBar);

            var bottom = BottomBar();
            DockPanel.SetDock(bottom, Dock.Bottom);
            var btnScan = MakeButton(I18n.T("btn_scan_registry"), "AccentButtonStyle", 190);
            var btnSelectAll = MakeButton(I18n.T("btn_select_all"), "GhostButtonStyle", 110);
            var btnRemove = MakeButton(I18n.T("btn_remove_selected_reg"), "DangerButtonStyle", 190);
            var statusLbl = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10,0,0,0), Foreground = Theme.Get("TextBrush") };
            bottom.Children.Add(btnScan);
            bottom.Children.Add(btnSelectAll);
            bottom.Children.Add(btnRemove);
            bottom.Children.Add(statusLbl);
            dock.Children.Add(bottom);

            bool allSelected = false;
            btnSelectAll.Click += (s, e) =>
            {
                allSelected = !allSelected;
                foreach (var entry in _ghostEntries) entry.IsSelected = allSelected;
                _gridRegClean.Items.Refresh();
                btnSelectAll.Content = allSelected ? I18n.T("btn_deselect_all") : I18n.T("btn_select_all");
                statusLbl.Text = allSelected ? string.Format(I18n.T("regclean_selected_count"), _ghostEntries.Count) : "";
            };

            _gridRegClean = CreateGrid(
                Tuple.Create(I18n.T("col_reg_name"), "DisplayName", 1.6),
                Tuple.Create(I18n.T("col_reg_reason"), "ReasonText", 1.2),
                Tuple.Create(I18n.T("col_reg_path"), "MissingPath", 1.8),
                Tuple.Create(I18n.T("col_reg_hive"), "Hive", 0.6));
            _gridRegClean.Columns.Insert(0, CreateCheckboxColumn("IsSelected"));
            dock.Children.Add(_gridRegClean);

            btnScan.Click += async (s, e) =>
            {
                btnScan.IsEnabled = false;
                statusLbl.Text = I18n.T("regclean_scanning");
                _ghostEntries = await Task.Run(() => RegistryCleanerData.Scan());
                btnScan.IsEnabled = true;
                allSelected = false;
                btnSelectAll.Content = I18n.T("btn_select_all");
                _gridRegClean.ItemsSource = _ghostEntries;
                statusLbl.Text = _ghostEntries.Count == 0 ? I18n.T("regclean_none_found") : string.Format(I18n.T("regclean_found_count"), _ghostEntries.Count);
            };

            btnRemove.Click += async (s, e) =>
            {
                var selected = _ghostEntries.Where(x => x.IsSelected).ToList();
                if (selected.Count == 0) return;
                if (!Dialogs.Confirm(I18n.T("btn_remove_selected_reg"), string.Format(I18n.T("confirm_remove_registry_msg"), selected.Count))) return;
                btnScan.IsEnabled = false; btnRemove.IsEnabled = false;
                regProgressBar.Visibility = Visibility.Visible;
                regProgressBar.Value = 0;
                regProgressBar.IsIndeterminate = true;
                statusLbl.Text = I18n.T("regclean_backing_up");
                var dispatcher = Dispatcher;
                var result = await Task.Run(() =>
                {
                    var backupPath = RegistryCleanerData.BackupToRegFile(selected, (idx, total) =>
                    {
                        dispatcher.BeginInvoke((Action)(() =>
                        {
                            regProgressBar.IsIndeterminate = false;
                            regProgressBar.Value = (idx * 100.0) / total;
                            statusLbl.Text = string.Format(I18n.T("progress_deleting_x_of_y"), idx + 1, total);
                        }));
                    });
                    var removed = RegistryCleanerData.RemoveEntries(selected);
                    return Tuple.Create(removed, backupPath);
                });
                regProgressBar.Value = 100;
                regProgressBar.Visibility = Visibility.Collapsed;
                btnScan.IsEnabled = true; btnRemove.IsEnabled = true;
                statusLbl.Text = "";
                _ghostEntries = _ghostEntries.Except(selected).ToList();
                _gridRegClean.ItemsSource = null;
                _gridRegClean.ItemsSource = _ghostEntries;
                Dialogs.Info(I18n.T("generic_done_title"), string.Format(I18n.T("regclean_removed_msg"), result.Item1, selected.Count, result.Item2 ?? "-"));
            };

            return dock;
        }

        // ---------------------------------------------------------------
        // Phishing / Suspicious Link Checker (paste-and-analyze, heuristic only)
        // ---------------------------------------------------------------
        private UIElement BuildPhishingCheckerTab()
        {
            var scroll = new ScrollViewer { Background = Theme.Get("BgBrush") };
            var panel = new StackPanel { Margin = new Thickness(20) };
            scroll.Content = panel;

            panel.Children.Add(new TextBlock { Text = I18n.T("phish_intro"), TextWrapping = TextWrapping.Wrap, Foreground = Theme.Get("TextMutedBrush"), Margin = new Thickness(0,0,0,14) });

            var txtInput = new TextBox
            {
                AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 160,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                ToolTip = I18n.T("phish_paste_placeholder"),
                Margin = new Thickness(0,0,0,10)
            };
            panel.Children.Add(txtInput);

            var btnAnalyze = MakeButton(I18n.T("btn_analyze"), "AccentButtonStyle", 160);
            btnAnalyze.HorizontalAlignment = HorizontalAlignment.Left;
            btnAnalyze.Margin = new Thickness(0,0,0,20);
            panel.Children.Add(btnAnalyze);

            var resultHost = new StackPanel();
            panel.Children.Add(resultHost);

            btnAnalyze.Click += (s, e) =>
            {
                resultHost.Children.Clear();
                if (string.IsNullOrWhiteSpace(txtInput.Text))
                {
                    resultHost.Children.Add(new TextBlock { Text = I18n.T("phish_no_text"), Foreground = Theme.Get("TextMutedBrush") });
                    return;
                }
                var result = PhishingChecker.Analyze(txtInput.Text);
                var riskBrush = result.RiskLevel == "high" ? Theme.Get("DangerBrush") : (result.RiskLevel == "medium" ? Theme.Get("AccentBrush") : Theme.Get("TextMutedBrush"));
                var riskText = result.RiskLevel == "high" ? I18n.T("phish_risk_high") : (result.RiskLevel == "medium" ? I18n.T("phish_risk_medium") : I18n.T("phish_risk_low"));

                var riskCard = new Border { Background = Theme.Get("PanelBrush"), BorderBrush = riskBrush, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(0,0,0,14) };
                riskCard.Child = new TextBlock { Text = riskText, FontWeight = FontWeights.Bold, FontSize = 14, Foreground = riskBrush, TextWrapping = TextWrapping.Wrap };
                resultHost.Children.Add(riskCard);

                if (result.UrlsFound.Count > 0)
                {
                    resultHost.Children.Add(SectionLabel(I18n.T("phish_urls_found")));
                    foreach (var url in result.UrlsFound) resultHost.Children.Add(new TextBlock { Text = url, Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,2) });
                }

                resultHost.Children.Add(SectionLabel(I18n.T("phish_reasons_found")));
                if (result.Findings.Count == 0)
                {
                    resultHost.Children.Add(new TextBlock { Text = I18n.T("phish_no_findings"), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap });
                }
                else
                {
                    foreach (var f in result.Findings.OrderByDescending(x => x.Weight))
                    {
                        var line = I18n.T(f.ReasonKey) + (f.Detail != null ? " (" + f.Detail + ")" : "");
                        resultHost.Children.Add(new TextBlock { Text = "• " + line, Foreground = Theme.Get("TextBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,4) });
                    }
                }
            };

            return scroll;
        }

        private UIElement BuildSettingsTab()
        {
            var panel = new StackPanel { Margin = new Thickness(28), Background = Theme.Get("BgBrush") };
            var scroll = new ScrollViewer { Content = panel, Background = Theme.Get("BgBrush") };

            panel.Children.Add(SectionLabel(I18n.T("section_appearance")));
            var themeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,14) };
            themeRow.Children.Add(new TextBlock { Text = I18n.T("theme_label"), Foreground = Theme.Get("TextBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0) });
            var cmbTheme = new ComboBox { Width = 160, Height = 28 };
            cmbTheme.Items.Add(I18n.T("theme_light"));
            cmbTheme.Items.Add(I18n.T("theme_dark"));
            cmbTheme.Items.Add(I18n.T("theme_high_contrast"));
            cmbTheme.SelectedIndex = _settings.Theme == "Dark" ? 1 : (_settings.Theme == "HighContrast" ? 2 : 0);
            themeRow.Children.Add(cmbTheme);
            themeRow.Children.Add(new TextBlock { Text = I18n.T("restart_note"), Foreground = Theme.Get("TextMutedBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12,0,0,0) });
            panel.Children.Add(themeRow);

            var chkFollowSystemTheme = new CheckBox { Content = I18n.T("follow_system_theme"), Style = (Style)Theme.GetStyle("CardCheckBoxStyle"), Margin = new Thickness(0,0,0,4) };
            chkFollowSystemTheme.IsChecked = _settings.FollowSystemTheme;
            // When on, the manual Light/Dark/High-Contrast picker above is
            // moot - the theme is re-derived from Windows on every launch
            // (see MainWindow ctor) - so disable it to avoid implying a
            // choice that won't stick.
            Action updateThemeComboEnabled = () => cmbTheme.IsEnabled = chkFollowSystemTheme.IsChecked != true;
            chkFollowSystemTheme.Checked += (s, e) => updateThemeComboEnabled();
            chkFollowSystemTheme.Unchecked += (s, e) => updateThemeComboEnabled();
            updateThemeComboEnabled();
            panel.Children.Add(chkFollowSystemTheme);
            panel.Children.Add(new TextBlock { Text = I18n.T("follow_system_theme_desc"), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,20) });

            panel.Children.Add(SectionLabel(I18n.T("section_language")));
            var langRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,20) };
            langRow.Children.Add(new TextBlock { Text = I18n.T("language_label"), Foreground = Theme.Get("TextBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0) });
            var cmbLang = new ComboBox { Width = 160, Height = 28 };
            cmbLang.Items.Add("English");
            cmbLang.Items.Add("עברית");
            cmbLang.SelectedIndex = _settings.Language == I18n.Hebrew ? 1 : 0;
            langRow.Children.Add(cmbLang);
            langRow.Children.Add(new TextBlock { Text = I18n.T("restart_note"), Foreground = Theme.Get("TextMutedBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12,0,0,0) });
            panel.Children.Add(langRow);

            // Advanced settings — collapsed by default. Power-user options
            // (default program-list filter, log management, and fine control
            // over the scheduled cleanup) live here instead of the main flow,
            // so a casual user sees a short, focused Settings page while the
            // options are still one click away for anyone who wants them.
            var advancedPanel = new StackPanel { Visibility = Visibility.Collapsed };
            var btnToggleAdvanced = MakeButton(I18n.T("btn_show_advanced"), "GhostButtonStyle", 220);
            btnToggleAdvanced.HorizontalAlignment = HorizontalAlignment.Left;
            btnToggleAdvanced.Margin = new Thickness(0,0,0,20);
            btnToggleAdvanced.Click += (s, e) =>
            {
                bool showing = advancedPanel.Visibility != Visibility.Visible;
                advancedPanel.Visibility = showing ? Visibility.Visible : Visibility.Collapsed;
                btnToggleAdvanced.Content = I18n.T(showing ? "btn_hide_advanced" : "btn_show_advanced");
            };
            panel.Children.Add(btnToggleAdvanced);
            panel.Children.Add(advancedPanel);

            advancedPanel.Children.Add(new TextBlock { Text = I18n.T("advanced_settings_desc"), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,16) });

            advancedPanel.Children.Add(SectionLabel(I18n.T("section_programs_settings")));
            var chkDefaultSystem = new CheckBox { Content = I18n.T("default_show_system"), Style = (Style)Theme.GetStyle("CardCheckBoxStyle"), Margin = new Thickness(0,0,0,4) };
            chkDefaultSystem.IsChecked = _settings.ShowSystemComponents;
            advancedPanel.Children.Add(chkDefaultSystem);
            advancedPanel.Children.Add(new TextBlock { Text = I18n.T("default_show_system_desc"), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,20) });

            advancedPanel.Children.Add(SectionLabel(I18n.T("section_logs")));
            var logsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,20) };
            var btnOpenLogFolder = MakeButton(I18n.T("btn_open_log_folder"), "GhostButtonStyle", 170);
            btnOpenLogFolder.Click += (s, e) => { AppPaths.EnsureDataDir(); Process.Start("explorer.exe", "\"" + AppPaths.DataDir + "\""); };
            var btnClearLogs = MakeButton(I18n.T("btn_clear_old_logs"), "GhostButtonStyle", 160);
            btnClearLogs.Click += (s, e) =>
            {
                if (!Dialogs.Confirm(I18n.T("confirm_uninstall_title"), I18n.T("confirm_clear_logs_msg"))) return;
                foreach (var f in System.IO.Directory.GetFiles(AppPaths.DataDir, "Log_*.txt"))
                {
                    if (!string.Equals(f, AppPaths.LogFile, StringComparison.OrdinalIgnoreCase))
                    { try { System.IO.File.Delete(f); } catch { } }
                }
                Dialogs.Info(I18n.T("generic_done_title"), I18n.T("logs_cleared_msg"));
            };
            logsRow.Children.Add(btnOpenLogFolder);
            logsRow.Children.Add(btnClearLogs);
            advancedPanel.Children.Add(logsRow);

            // Scheduled cleanup fine-tuning: which categories an unattended run
            // may touch, plus a size safety limit. Kept in Advanced since it
            // only matters once "Scheduled automatic cleanup" (below, in the
            // main flow) is actually turned on.
            advancedPanel.Children.Add(SectionLabel(I18n.T("scheduled_cleanup_categories_label")));
            advancedPanel.Children.Add(new TextBlock { Text = I18n.T("scheduled_cleanup_categories_desc"), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,8) });
            var scheduledCategoryKeys = new[] { "user_temp", "win_temp", "browser_cache", "win_update", "thumbnails", "prefetch" };
            var scheduledCategoryLabelKeys = new[] { "junk_user_temp", "junk_win_temp", "junk_browser_cache", "junk_win_update", "junk_thumbnails", "junk_prefetch" };
            var selectedScheduledCategories = new HashSet<string>(_settings.ScheduledCleanupCategories.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            var categoryChecks = new List<CheckBox>();
            var categoriesPanel = new WrapPanel { Margin = new Thickness(0,0,0,16) };
            for (int i = 0; i < scheduledCategoryKeys.Length; i++)
            {
                var chk = new CheckBox { Content = I18n.T(scheduledCategoryLabelKeys[i]), Style = (Style)Theme.GetStyle("CardCheckBoxStyle"), Margin = new Thickness(0,0,20,10), Tag = scheduledCategoryKeys[i] };
                chk.IsChecked = selectedScheduledCategories.Contains(scheduledCategoryKeys[i]);
                categoryChecks.Add(chk);
                categoriesPanel.Children.Add(chk);
            }
            advancedPanel.Children.Add(categoriesPanel);

            var safetyRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,20) };
            safetyRow.Children.Add(new TextBlock { Text = I18n.T("scheduled_cleanup_safety_label"), Foreground = Theme.Get("TextBrush"), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Width = 320, Margin = new Thickness(0,0,10,0) });
            var cmbSafety = new ComboBox { Width = 140, Height = 28 };
            int[] safetyOptionsMb = { 0, 1024, 5120, 10240 };
            string[] safetyOptionKeys = { "opt_no_limit", "opt_1gb", "opt_5gb", "opt_10gb" };
            foreach (var k in safetyOptionKeys) cmbSafety.Items.Add(I18n.T(k));
            var safetyIdx = Array.IndexOf(safetyOptionsMb, _settings.ScheduledCleanupMaxSizeMB);
            cmbSafety.SelectedIndex = safetyIdx >= 0 ? safetyIdx : 0;
            safetyRow.Children.Add(cmbSafety);
            advancedPanel.Children.Add(safetyRow);

            panel.Children.Add(SectionLabel(I18n.T("section_safety")));
            var chkRestorePoint = new CheckBox { Content = I18n.T("restore_point_toggle"), Style = (Style)Theme.GetStyle("CardCheckBoxStyle"), Margin = new Thickness(0,0,0,14) };
            chkRestorePoint.IsChecked = _settings.CreateRestorePoints;
            panel.Children.Add(chkRestorePoint);
            var retentionRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,20) };
            retentionRow.Children.Add(new TextBlock { Text = I18n.T("quarantine_retention_label"), Foreground = Theme.Get("TextBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0) });
            var cmbRetention = new ComboBox { Width = 100, Height = 28 };
            int[] retentionOptions = { 1, 3, 7, 14, 30 };
            foreach (var d in retentionOptions) cmbRetention.Items.Add(d.ToString());
            var retentionIdx = Array.IndexOf(retentionOptions, _settings.QuarantineRetentionDays);
            cmbRetention.SelectedIndex = retentionIdx >= 0 ? retentionIdx : 2;
            retentionRow.Children.Add(cmbRetention);
            retentionRow.Children.Add(new TextBlock { Text = I18n.T("quarantine_retention_days_suffix"), Foreground = Theme.Get("TextMutedBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8,0,0,0) });
            panel.Children.Add(retentionRow);
            panel.Children.Add(new TextBlock { Text = I18n.T("quarantine_retention_desc"), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,-14,0,20) });
            var trustCard = new Border { Background = Theme.Get("AccentLightBrush"), BorderBrush = Theme.Get("AccentBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(16), Margin = new Thickness(0,0,0,20) };
            var trustStack = new StackPanel();
            trustStack.Children.Add(new TextBlock { Text = I18n.T("trust_note_title"), FontWeight = FontWeights.Bold, Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0,0,0,6) });
            trustStack.Children.Add(new TextBlock { Text = I18n.T("trust_note_text"), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap });
            trustCard.Child = trustStack;
            panel.Children.Add(trustCard);

            panel.Children.Add(SectionLabel(I18n.T("section_scheduled_cleanup")));
            var chkScheduledCleanup = new CheckBox { Content = I18n.T("scheduled_cleanup_toggle"), Style = (Style)Theme.GetStyle("CardCheckBoxStyle"), Margin = new Thickness(0,0,0,8) };
            chkScheduledCleanup.IsChecked = _settings.ScheduledCleanupEnabled;
            panel.Children.Add(chkScheduledCleanup);
            panel.Children.Add(new TextBlock { Text = I18n.T("scheduled_cleanup_desc"), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,10) });
            var freqRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,20) };
            freqRow.Children.Add(new TextBlock { Text = I18n.T("scheduled_cleanup_frequency_label"), Foreground = Theme.Get("TextBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,10,0) });
            var cmbFrequency = new ComboBox { Width = 140, Height = 28 };
            string[] freqKeys = { "Daily", "Weekly", "Monthly" };
            string[] freqLabels = { I18n.T("frequency_daily"), I18n.T("frequency_weekly"), I18n.T("frequency_monthly") };
            foreach (var l in freqLabels) cmbFrequency.Items.Add(l);
            var freqIdx = Array.IndexOf(freqKeys, _settings.ScheduledCleanupFrequency);
            cmbFrequency.SelectedIndex = freqIdx >= 0 ? freqIdx : 1;
            freqRow.Children.Add(cmbFrequency);
            panel.Children.Add(freqRow);
            panel.Children.Add(new TextBlock { Text = I18n.T("scheduled_cleanup_note_advanced"), Foreground = Theme.Get("TextMutedBrush"), FontStyle = FontStyles.Italic, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,-12,0,20) });

            panel.Children.Add(SectionLabel(I18n.T("section_updates")));
            panel.Children.Add(new TextBlock { Text = I18n.T("update_source_label"), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,10) });
            var btnCheckUpdates = MakeButton(I18n.T("btn_check_updates"), "GhostButtonStyle", 170);
            btnCheckUpdates.HorizontalAlignment = HorizontalAlignment.Left;
            btnCheckUpdates.Margin = new Thickness(0,0,0,20);
            btnCheckUpdates.Click += async (s, e) =>
            {
                btnCheckUpdates.IsEnabled = false;
                Mouse.OverrideCursor = Cursors.Wait;
                var info = await Task.Run(() => UpdateChecker.Check(Program.AppVersion));
                Mouse.OverrideCursor = null;
                btnCheckUpdates.IsEnabled = true;
                if (!string.IsNullOrEmpty(info.Error))
                {
                    Dialogs.ShowError(I18n.T("generic_error_title"), string.Format(I18n.T("update_check_error"), info.Error));
                    return;
                }
                if (!info.Available) { Dialogs.Info("", string.Format(I18n.T("update_up_to_date"), Program.AppVersion)); return; }
                if (!Dialogs.Confirm(I18n.T("update_available_title"), string.Format(I18n.T("update_available_msg"), info.LatestVersion, Program.AppVersion, info.Notes ?? ""))) return;
                await RunOneClickUpdateAsync(info);
            };
            panel.Children.Add(btnCheckUpdates);

            var chkAutoCheckUpdates = new CheckBox { Content = I18n.T("auto_check_updates"), Style = (Style)Theme.GetStyle("CardCheckBoxStyle"), Margin = new Thickness(0,0,0,4) };
            chkAutoCheckUpdates.IsChecked = _settings.AutoCheckUpdates;
            panel.Children.Add(chkAutoCheckUpdates);
            panel.Children.Add(new TextBlock { Text = I18n.T("auto_check_updates_desc"), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,10) });

            // Opt-in (default off, see AppSettings.AutoInstallUpdates): once
            // on, a detected update is downloaded and installed with the new
            // /SILENT switch with no further prompting, and OptiGuard closes
            // itself to let the install proceed - see the startup update
            // check above (BuildUpdateBanner) for where this is acted on.
            var chkAutoInstallUpdates = new CheckBox { Content = I18n.T("auto_install_updates"), Style = (Style)Theme.GetStyle("CardCheckBoxStyle"), Margin = new Thickness(0,0,0,4) };
            chkAutoInstallUpdates.IsChecked = _settings.AutoInstallUpdates;
            panel.Children.Add(chkAutoInstallUpdates);
            panel.Children.Add(new TextBlock { Text = I18n.T("auto_install_updates_desc"), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,14) });

            panel.Children.Add(SectionLabel(I18n.T("section_notifications")));
            var chkNotifications = new CheckBox { Content = I18n.T("enable_notifications"), Style = (Style)Theme.GetStyle("CardCheckBoxStyle"), Margin = new Thickness(0,0,0,4) };
            chkNotifications.IsChecked = _settings.EnableNotifications;
            panel.Children.Add(chkNotifications);
            panel.Children.Add(new TextBlock { Text = I18n.T("enable_notifications_desc"), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,20) });

            panel.Children.Add(SectionLabel(I18n.T("section_widget")));
            var chkShowWidget = new CheckBox { Content = I18n.T("show_desktop_widget"), Style = (Style)Theme.GetStyle("CardCheckBoxStyle"), Margin = new Thickness(0,0,0,4) };
            chkShowWidget.IsChecked = _settings.ShowDesktopWidget;
            // The widget's own X button turns ShowDesktopWidget off and saves
            // immediately. The Settings page is built once and cached, so
            // without this the checkbox would still show "on" - and the next
            // unrelated Save here would silently re-open the widget the user
            // just closed.
            WidgetWindow.HiddenByUser += () => Dispatcher.Invoke(() => chkShowWidget.IsChecked = false);
            panel.Children.Add(chkShowWidget);
            panel.Children.Add(new TextBlock { Text = I18n.T("show_desktop_widget_desc"), Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,20) });

            panel.Children.Add(SectionLabel(I18n.T("section_about")));
            panel.Children.Add(new TextBlock
            {
                Text = string.Format(I18n.T("about_text"), I18n.T("app_name"), Program.AppVersion, I18n.T("copyright")),
                Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12)
            });
            var aboutRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0,0,0,24) };
            var btnChangelog = MakeButton(I18n.T("btn_view_changelog"), "GhostButtonStyle", 160);
            btnChangelog.Click += (s, e) =>
            {
                try
                {
                    var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CHANGELOG.md");
                    if (File.Exists(path)) Process.Start("notepad.exe", "\"" + path + "\"");
                    else Dialogs.Info("", I18n.T("changelog_not_found"));
                }
                catch (Exception ex) { Dialogs.ShowError(I18n.T("generic_error_title"), ex.Message); }
            };
            var btnLicense = MakeButton(I18n.T("btn_view_license"), "GhostButtonStyle", 190);
            btnLicense.Margin = new Thickness(10,0,0,0);
            btnLicense.Click += (s, e) =>
            {
                try
                {
                    var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EULA.md");
                    if (File.Exists(path)) Process.Start("notepad.exe", "\"" + path + "\"");
                    else Dialogs.Info("", I18n.T("license_not_found"));
                }
                catch (Exception ex) { Dialogs.ShowError(I18n.T("generic_error_title"), ex.Message); }
            };
            var btnReplayOnboarding = MakeButton(I18n.T("btn_show_onboarding_again"), "GhostButtonStyle", 200);
            btnReplayOnboarding.Margin = new Thickness(10,0,0,0);
            btnReplayOnboarding.Click += (s, e) =>
            {
                var onboarding = new OnboardingWindow { Owner = this };
                onboarding.ShowDialog();
                _settings.Language = onboarding.ResultLanguage;
                _settings.Theme = onboarding.ResultTheme;
                I18n.CurrentLang = _settings.Language;
                Theme.Load(_settings.Theme);
                _settings.Save();
                Dialogs.Info(I18n.T("generic_done_title"), I18n.T("restart_note"));
            };
            var btnExportDiagnostics = MakeButton(I18n.T("btn_export_diagnostics"), "GhostButtonStyle", 190);
            btnExportDiagnostics.Margin = new Thickness(10,0,0,0);
            btnExportDiagnostics.Click += (s, e) =>
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = I18n.T("save_diagnostics_zip_title"),
                    FileName = DiagnosticsExporter.DefaultFileName(),
                    Filter = "Zip (*.zip)|*.zip",
                    DefaultExt = ".zip",
                    AddExtension = true
                };
                if (dlg.ShowDialog(this) != true) return;
                try
                {
                    btnExportDiagnostics.IsEnabled = false;
                    Mouse.OverrideCursor = Cursors.Wait;
                    DiagnosticsExporter.Export(dlg.FileName);
                    Dialogs.Info(I18n.T("generic_done_title"), string.Format(I18n.T("diagnostics_export_success"), dlg.FileName));
                }
                catch (Exception ex)
                {
                    Logger.Log("Export Diagnostics failed: " + ex);
                    Dialogs.ShowError(I18n.T("generic_error_title"), string.Format(I18n.T("diagnostics_export_error"), ex.Message));
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                    btnExportDiagnostics.IsEnabled = true;
                }
            };
            aboutRow.Children.Add(btnChangelog);
            aboutRow.Children.Add(btnLicense);
            aboutRow.Children.Add(btnReplayOnboarding);
            aboutRow.Children.Add(btnExportDiagnostics);
            panel.Children.Add(aboutRow);

            var btnSave = MakeButton(I18n.T("btn_save"), "AccentButtonStyle", 160);
            btnSave.HorizontalAlignment = HorizontalAlignment.Left;
            btnSave.Margin = new Thickness(0);
            btnSave.Click += (s, e) =>
            {
                _settings.FollowSystemTheme = chkFollowSystemTheme.IsChecked == true;
                _settings.Theme = _settings.FollowSystemTheme
                    ? UninstallerPro.Theme.DetectSystemTheme()
                    : (cmbTheme.SelectedIndex == 1 ? "Dark" : (cmbTheme.SelectedIndex == 2 ? "HighContrast" : "Light"));
                _settings.Language = cmbLang.SelectedIndex == 1 ? I18n.Hebrew : I18n.English;
                _settings.ShowSystemComponents = chkDefaultSystem.IsChecked == true;
                _settings.CreateRestorePoints = chkRestorePoint.IsChecked == true;
                int parsedRetention;
                if (int.TryParse(cmbRetention.SelectedItem as string, out parsedRetention)) _settings.QuarantineRetentionDays = parsedRetention;
                _settings.AutoCheckUpdates = chkAutoCheckUpdates.IsChecked == true;
                _settings.AutoInstallUpdates = chkAutoInstallUpdates.IsChecked == true;
                _settings.EnableNotifications = chkNotifications.IsChecked == true;
                RestorePoint.Enabled = _settings.CreateRestorePoints;

                _settings.ShowDesktopWidget = chkShowWidget.IsChecked == true;
                if (_settings.ShowDesktopWidget) WidgetWindow.OpenOrShow(_settings);
                else WidgetWindow.CloseIfOpen();

                var scheduledCleanupWasEnabled = _settings.ScheduledCleanupEnabled;
                var scheduledCleanupWasFrequency = _settings.ScheduledCleanupFrequency;
                _settings.ScheduledCleanupEnabled = chkScheduledCleanup.IsChecked == true;
                _settings.ScheduledCleanupFrequency = freqKeys[Math.Max(0, cmbFrequency.SelectedIndex)];

                var chosenCategories = categoryChecks.Where(c => c.IsChecked == true).Select(c => (string)c.Tag);
                _settings.ScheduledCleanupCategories = string.Join(",", chosenCategories);
                _settings.ScheduledCleanupMaxSizeMB = safetyOptionsMb[Math.Max(0, cmbSafety.SelectedIndex)];

                _settings.Save();
                _chkShowSystem.IsChecked = _settings.ShowSystemComponents;
                RefreshPrograms();

                // Only touch Task Scheduler if something about the schedule
                // actually changed - avoids re-registering the task on every
                // unrelated Settings save.
                if (_settings.ScheduledCleanupEnabled != scheduledCleanupWasEnabled ||
                    _settings.ScheduledCleanupFrequency != scheduledCleanupWasFrequency)
                {
                    if (_settings.ScheduledCleanupEnabled)
                    {
                        string taskError;
                        if (!ScheduledCleanupData.CreateOrUpdate(_settings.ScheduledCleanupFrequency, out taskError))
                        {
                            _settings.ScheduledCleanupEnabled = false;
                            _settings.Save();
                            chkScheduledCleanup.IsChecked = false;
                            Dialogs.ShowError(I18n.T("generic_error_title"), string.Format(I18n.T("scheduled_cleanup_error"), taskError));
                            return;
                        }
                    }
                    else
                    {
                        ScheduledCleanupData.Remove();
                    }
                }

                Dialogs.Info(I18n.T("generic_done_title"), I18n.T("settings_saved_msg"));
            };
            panel.Children.Add(btnSave);

            return scroll;
        }

    }
}
