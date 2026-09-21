using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace UninstallerPro
{
    // Persistent desktop widget: a small, always-on-top, frameless panel that
    // stays visible on the desktop separately from the main window, showing
    // the same Health Score computed by HealthScoreData.Compute() (no
    // duplicated logic) plus one-click access back into the full app. Design
    // follows the same brand gradient/rounded-corner language as
    // SplashWindow.cs (assets/BRAND.md), per STANDARDS.md.
    //
    // Lifecycle: created once by Program.cs right after the main window is
    // shown (if AppSettings.ShowDesktopWidget is on), and toggled open/closed
    // live from Settings. There is no system tray/background-service concept
    // in OptiGuard today, so the widget only exists while the OptiGuard
    // process itself is running - it is not a separate always-running
    // background app. That matches "launch automatically with the app" as
    // literally requested, without inventing tray-icon infrastructure that
    // doesn't exist anywhere else in this codebase.
    public class WidgetWindow : Window
    {
        // Single instance - Program.cs / MainWindow.cs open/close/refresh it
        // through these statics rather than passing a reference around.
        public static WidgetWindow Instance { get; private set; }

        private TextBlock _scoreText;
        private TextBlock _scoreLabel;
        private Border _scoreRing;
        private Button _btnQuickScan;
        private AppSettings _settings;
        private DispatcherTimer _refreshTimer;
        private bool _isComputing;
        private bool _isScanning;

        public static void OpenOrShow(AppSettings settings)
        {
            try
            {
                if (Instance != null)
                {
                    Instance.Show();
                    Instance.RefreshScore();
                    return;
                }
                Instance = new WidgetWindow(settings);
                Instance.Show();
            }
            catch (Exception ex) { Logger.Log("WidgetWindow.OpenOrShow failed: " + ex.Message); }
        }

        public static void CloseIfOpen()
        {
            try
            {
                if (Instance == null) return;
                Instance.Close();
            }
            catch { }
        }

        // Called after actions elsewhere in the app change system state
        // (cleanup, startup changes, etc.) so the widget doesn't show a stale
        // number until its own timer happens to tick.
        public static void RefreshIfOpen()
        {
            try { if (Instance != null) Instance.RefreshScore(); } catch { }
        }

        private WidgetWindow(AppSettings settings)
        {
            _settings = settings;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            // Sits on top of everything without stealing focus/keyboard input
            // from whatever the user is doing elsewhere - a passive glance
            // panel, not a window that competes for input like the main app.
            ShowActivated = false;
            Width = 236;
            SizeToContent = SizeToContent.Height;
            Opacity = 0;
            FlowDirection = I18n.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            PlacePosition();

            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0x0F, 0x2E, 0x27), 0.0),
                    new GradientStop(Color.FromRgb(0x10, 0x59, 0x46), 0.55),
                    new GradientStop(Color.FromRgb(0x10, 0xB9, 0x81), 1.0)
                }
            };

            var outer = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = gradient,
                Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 2, Opacity = 0.4, Color = Colors.Black },
                Cursor = Cursors.SizeAll
            };
            // Draggable from anywhere on the panel background, per the spec -
            // position is then persisted to Settings the same way MainWindow
            // persists its own size/position (SaveWindowState pattern).
            outer.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource is Button) return;
                try { DragMove(); } catch { }
            };
            outer.MouseLeftButtonUp += (s, e) => SavePosition();
            Content = outer;

            var root = new Grid { Margin = new Thickness(14, 10, 14, 12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            outer.Child = root;

            // Header row: app name + close/hide button.
            var headerRow = new DockPanel();
            Grid.SetRow(headerRow, 0);
            root.Children.Add(headerRow);

            var btnClose = new Button
            {
                Content = "✕",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Cursor = Cursors.Hand,
                Padding = new Thickness(4),
                FontSize = 11
            };
            AutomationProperties.SetName(btnClose, I18n.T("widget_hide_tooltip"));
            btnClose.ToolTip = I18n.T("widget_hide_tooltip");
            DockPanel.SetDock(btnClose, I18n.IsRtl ? Dock.Left : Dock.Right);
            btnClose.Click += (s, e) =>
            {
                _settings.ShowDesktopWidget = false;
                _settings.Save();
                Close();
            };
            headerRow.Children.Add(btnClose);

            headerRow.Children.Add(new TextBlock
            {
                Text = "OptiGuard",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.9
            });

            // Health score row: big number + label, same 0-100 figure and
            // wording as the Dashboard's Health Score card.
            var scoreRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
            Grid.SetRow(scoreRow, 1);
            root.Children.Add(scoreRow);

            _scoreRing = new Border
            {
                Width = 54,
                Height = 54,
                CornerRadius = new CornerRadius(27),
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(2),
                Margin = new Thickness(0, 0, 12, 0)
            };
            _scoreText = new TextBlock
            {
                Text = "--",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _scoreRing.Child = _scoreText;
            scoreRow.Children.Add(_scoreRing);

            var labelStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _scoreLabel = new TextBlock
            {
                Text = I18n.T("widget_health_score_label"),
                Foreground = new SolidColorBrush(Color.FromRgb(0xD1, 0xFA, 0xE5)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };
            labelStack.Children.Add(_scoreLabel);
            scoreRow.Children.Add(labelStack);

            // Quick action row.
            var actionsRow = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetRow(actionsRow, 2);
            root.Children.Add(actionsRow);

            var btnOpen = MakeActionButton(I18n.T("widget_open_optiguard"), isPrimary: true);
            btnOpen.Click += (s, e) => OpenMainWindow();
            actionsRow.Children.Add(_actionButtonChromes[btnOpen]);

            // "Run Quick Scan" - a genuinely fast, safe action: it only scans
            // for junk (JunkCleanerData.ScanAll, same as the Dashboard/Junk
            // Cleaner tab) and reports the total size found via a Toast. It
            // never deletes anything from the widget itself - deleting stays
            // a deliberate, confirmed action inside the main window.
            _btnQuickScan = MakeActionButton(I18n.T("widget_quick_scan"), isPrimary: false);
            _actionButtonChromes[_btnQuickScan].Margin = new Thickness(6, 0, 0, 0);
            _btnQuickScan.Click += (s, e) => RunQuickScan();
            actionsRow.Children.Add(_actionButtonChromes[_btnQuickScan]);

            Loaded += (s, e) =>
            {
                BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
                RefreshScore();
            };
            Closed += (s, e) =>
            {
                if (_refreshTimer != null) { _refreshTimer.Stop(); _refreshTimer = null; }
                if (Instance == this) Instance = null;
            };

            // Keeps the score fresh without a manual refresh, per the spec -
            // a lightweight periodic recompute (same checks the Dashboard
            // card runs) rather than a file-watcher on every module's data.
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            _refreshTimer.Tick += (s, e) => RefreshScore();
            _refreshTimer.Start();
        }

        // Returns the Button itself (already wrapped in a rounded Border for
        // its visual chrome) so callers can still hook Button.Click/Content -
        // built from a Border+Button pair rather than a hand-rolled
        // ControlTemplate, which keeps this file free of extra XAML plumbing.
        private Button MakeActionButton(string text, bool isPrimary)
        {
            var btn = new Button
            {
                Content = text,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10.5,
                Height = 26,
                Padding = new Thickness(8, 0, 8, 0),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = isPrimary ? new SolidColorBrush(Color.FromRgb(0x0F, 0x2E, 0x27)) : Brushes.White,
                FontWeight = isPrimary ? FontWeights.SemiBold : FontWeights.Normal,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            var chrome = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = isPrimary ? Brushes.White : new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                BorderThickness = new Thickness(isPrimary ? 0 : 1),
                Child = btn
            };
            _actionButtonChromes[btn] = chrome;
            return btn;
        }

        private readonly System.Collections.Generic.Dictionary<Button, Border> _actionButtonChromes = new System.Collections.Generic.Dictionary<Button, Border>();

        private void OpenMainWindow()
        {
            try
            {
                var main = Application.Current != null ? Application.Current.MainWindow : null;
                if (main == null)
                {
                    main = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                }
                if (main == null) return;
                // Same behavior a tray-icon click would give: restore if
                // minimized, bring to the foreground, focus it.
                if (main.WindowState == WindowState.Minimized) main.WindowState = WindowState.Normal;
                if (!main.IsVisible) main.Show();
                main.Activate();
                main.Topmost = true;
                main.Topmost = false;
                main.Focus();
            }
            catch (Exception ex) { Logger.Log("WidgetWindow.OpenMainWindow failed: " + ex.Message); }
        }

        private void RunQuickScan()
        {
            if (_isScanning) return;
            _isScanning = true;
            var originalText = _btnQuickScan.Content;
            _btnQuickScan.IsEnabled = false;
            _btnQuickScan.Content = I18n.T("widget_quick_scan_scanning");
            var dispatcher = Dispatcher;
            System.Threading.Tasks.Task.Run(() =>
            {
                long totalBytes = 0;
                try
                {
                    var categories = JunkCleanerData.ScanAll();
                    totalBytes = categories.Sum(c => c.SizeBytes);
                }
                catch (Exception ex) { Logger.Log("WidgetWindow quick scan failed: " + ex.Message); }
                dispatcher.Invoke(() =>
                {
                    _isScanning = false;
                    _btnQuickScan.IsEnabled = true;
                    _btnQuickScan.Content = originalText;
                    Toast.Show(string.Format(I18n.T("widget_quick_scan_result"), JunkCleanerData.FormatSize(totalBytes)));
                });
            });
        }

        public void RefreshScore()
        {
            if (_isComputing) return;
            _isComputing = true;
            var dispatcher = Dispatcher;
            System.Threading.Tasks.Task.Run(() =>
            {
                HealthScoreResult result = null;
                try { result = HealthScoreData.Compute(); }
                catch (Exception ex) { Logger.Log("WidgetWindow.RefreshScore failed: " + ex.Message); }
                dispatcher.Invoke(() =>
                {
                    _isComputing = false;
                    if (result == null) return;
                    _scoreText.Text = result.Score.ToString();
                });
            });
        }

        // Default position: bottom-right corner of the work area (a
        // conventional, out-of-the-way spot for a persistent glance widget),
        // unless the user already dragged it somewhere and that position was
        // saved and still fits the current screen layout - same fallback
        // pattern as MainWindow.RestoreWindowState.
        private void PlacePosition()
        {
            var workArea = SystemParameters.WorkArea;
            bool fitsOnScreen = _settings.WidgetLeft >= 0 && _settings.WidgetTop >= 0
                && _settings.WidgetLeft + 60 < SystemParameters.VirtualScreenWidth
                && _settings.WidgetTop + 60 < SystemParameters.VirtualScreenHeight;

            if (fitsOnScreen)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = _settings.WidgetLeft;
                Top = _settings.WidgetTop;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = workArea.Right - Width - 20;
                Top = workArea.Bottom - 140 - 20;
            }
        }

        private void SavePosition()
        {
            try
            {
                _settings.WidgetLeft = Left;
                _settings.WidgetTop = Top;
                _settings.Save();
            }
            catch { }
        }
    }
}
