using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace UninstallerPro
{
    // Shared "is this saved position still on a connected monitor?" check for
    // MainWindow and WidgetWindow. Uses the full virtual desktop, including
    // monitors to the left of / above the primary one (negative coordinates) -
    // the previous checks required Left/Top >= 0, so a window or widget placed
    // on a left-hand second monitor was never restored there.
    public static class ScreenHelper
    {
        public static bool FitsVirtualScreen(double left, double top, double margin)
        {
            if (double.IsNaN(left) || double.IsNaN(top)) return false;
            double vLeft = SystemParameters.VirtualScreenLeft, vTop = SystemParameters.VirtualScreenTop;
            double vRight = vLeft + SystemParameters.VirtualScreenWidth, vBottom = vTop + SystemParameters.VirtualScreenHeight;
            return left >= vLeft - margin / 2 && top >= vTop
                && left + margin < vRight && top + margin < vBottom;
        }
    }

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
    // in OptiGuard (STANDARDS.md 12.1: X closes OptiGuard completely), so the
    // widget only exists while the OptiGuard process itself is running.
    public class WidgetWindow : Window
    {
        // Single instance - Program.cs / MainWindow.cs open/close/refresh it
        // through these statics rather than passing a reference around.
        public static WidgetWindow Instance { get; private set; }

        // Raised when the user hides the widget with its own X button (which
        // also turns AppSettings.ShowDesktopWidget off), so an already-built
        // Settings page can update its checkbox to match.
        public static event Action HiddenByUser;

        private TextBlock _scoreText;
        private TextBlock _scoreLabel;
        private Border _scoreRing;
        private Button _btnQuickScan;
        private AppSettings _settings;
        private DispatcherTimer _refreshTimer;
        private bool _isComputing;
        private bool _isScanning;
        private readonly bool _highContrast;

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

        // Same as RefreshIfOpen, but for callers that already have a fresh
        // result (the Dashboard's Health Score card) - shows it directly
        // instead of running the WMI-heavy HealthScoreData.Compute() twice.
        public static void ShowResultIfOpen(HealthScoreResult result)
        {
            try { if (Instance != null && result != null) Instance.ApplyResult(result); } catch { }
        }

        private WidgetWindow(AppSettings settings)
        {
            _settings = settings;
            // STANDARDS.md 20.2: with a Windows contrast theme on, the user's
            // own system colors replace the brand gradient entirely.
            _highContrast = Theme.IsSystemHighContrast;

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
            Title = I18n.T("app_name");
            AutomationProperties.SetName(this, I18n.T("app_name") + " - " + I18n.T("widget_health_score_label"));

            PlacePosition();

            Brush panelBackground;
            Brush textBrush, subTextBrush, buttonBorderBrush;
            if (_highContrast)
            {
                panelBackground = SystemColors.WindowBrush;
                textBrush = SystemColors.WindowTextBrush;
                subTextBrush = SystemColors.WindowTextBrush;
                buttonBorderBrush = SystemColors.WindowTextBrush;
            }
            else
            {
                // Brand gradient, ending on #047857 rather than the splash's
                // #10B981: the widget's small (10.5-11px) white button/label
                // text sits on the lower-right end of this gradient, and white
                // on #10B981 is only 2.54:1 - #047857 keeps it at 5.48:1
                // (WCAG AA, STANDARDS.md 20.1).
                panelBackground = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops =
                    {
                        new GradientStop(Color.FromRgb(0x0F, 0x2E, 0x27), 0.0),
                        new GradientStop(Color.FromRgb(0x10, 0x59, 0x46), 0.55),
                        new GradientStop(Color.FromRgb(0x04, 0x78, 0x57), 1.0)
                    }
                };
                textBrush = Brushes.White;
                subTextBrush = new SolidColorBrush(Color.FromRgb(0xD1, 0xFA, 0xE5));
                buttonBorderBrush = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255));
            }

            var outer = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = panelBackground,
                BorderBrush = _highContrast ? SystemColors.WindowTextBrush : null,
                BorderThickness = new Thickness(_highContrast ? 2 : 0),
                Effect = _highContrast ? null : new DropShadowEffect { BlurRadius = 24, ShadowDepth = 2, Opacity = 0.4, Color = Colors.Black },
                Cursor = Cursors.SizeAll
            };
            // Draggable from anywhere on the panel background. DragMove() runs
            // its own modal mouse loop and swallows the MouseLeftButtonUp that
            // ends the drag, so the position is saved right after DragMove()
            // returns (the old MouseLeftButtonUp handler never fired after a
            // real drag, so the dragged position was never persisted).
            outer.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource is Button) return;
                var before = new Point(Left, Top);
                try { DragMove(); } catch { }
                if (Math.Abs(Left - before.X) > 0.5 || Math.Abs(Top - before.Y) > 0.5) SavePosition();
            };
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
                Foreground = textBrush,
                Cursor = Cursors.Hand,
                Width = 28,
                Height = 28,
                FontSize = 11,
                Template = FlatButtonTemplate(),
                FocusVisualStyle = FocusRing(textBrush)
            };
            AutomationProperties.SetName(btnClose, I18n.T("widget_hide_tooltip"));
            btnClose.ToolTip = I18n.T("widget_hide_tooltip");
            // Logical Right = the trailing edge; FlowDirection mirrors it to
            // the physical left in Hebrew (no manual IsRtl flip - that used to
            // cancel the mirroring out and keep the X on the right in RTL).
            DockPanel.SetDock(btnClose, Dock.Right);
            btnClose.Click += (s, e) =>
            {
                _settings.ShowDesktopWidget = false;
                _settings.Save();
                Close();
                var handler = HiddenByUser;
                if (handler != null) { try { handler(); } catch { } }
            };
            headerRow.Children.Add(btnClose);

            headerRow.Children.Add(new TextBlock
            {
                Text = "OptiGuard",
                Foreground = textBrush,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });

            // Health score row: big number + label, same 0-100 figure and
            // wording as the Dashboard's Health Score card.
            var scoreRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 8) };
            Grid.SetRow(scoreRow, 1);
            root.Children.Add(scoreRow);

            _scoreRing = new Border
            {
                Width = 54,
                Height = 54,
                CornerRadius = new CornerRadius(27),
                Background = _highContrast ? SystemColors.WindowBrush : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderBrush = textBrush,
                BorderThickness = new Thickness(2),
                Margin = new Thickness(0, 0, 12, 0)
            };
            _scoreText = new TextBlock
            {
                Text = "--",
                Foreground = textBrush,
                FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.Bold,
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _scoreRing.Child = _scoreText;
            // Narrator: announce "Health Score 87" (not a bare "87"), and
            // re-announce politely whenever the number changes.
            AutomationProperties.SetName(_scoreRing, I18n.T("widget_health_score_label"));
            AutomationProperties.SetLiveSetting(_scoreText, AutomationLiveSetting.Polite);
            scoreRow.Children.Add(_scoreRing);

            var labelStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _scoreLabel = new TextBlock
            {
                Text = I18n.T("widget_health_score_label"),
                Foreground = subTextBrush,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap
            };
            labelStack.Children.Add(_scoreLabel);
            scoreRow.Children.Add(labelStack);

            // Quick action row.
            // WrapPanel, not a fixed row: longer translations wrap to a second
            // line instead of being clipped at the widget's fixed width.
            var actionsRow = new WrapPanel { Orientation = Orientation.Horizontal };
            Grid.SetRow(actionsRow, 2);
            root.Children.Add(actionsRow);

            var btnOpen = MakeActionButton(I18n.T("widget_open_optiguard"), isPrimary: true, textBrush: textBrush, borderBrush: buttonBorderBrush);
            btnOpen.Click += (s, e) => OpenMainWindow();
            actionsRow.Children.Add(btnOpen);

            // "Run Quick Scan" - a genuinely fast, safe action: it only scans
            // for junk (JunkCleanerData.ScanAll, same as the Dashboard/Junk
            // Cleaner tab) and reports the total size found via a Toast. It
            // never deletes anything from the widget itself - deleting stays
            // a deliberate, confirmed action inside the main window.
            _btnQuickScan = MakeActionButton(I18n.T("widget_quick_scan"), isPrimary: false, textBrush: textBrush, borderBrush: buttonBorderBrush);
            _btnQuickScan.Margin = new Thickness(6, 0, 0, 4);
            _btnQuickScan.Click += (s, e) => RunQuickScan();
            actionsRow.Children.Add(_btnQuickScan);

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

            // Keeps the score fresh without a manual refresh - a lightweight
            // periodic recompute (same checks the Dashboard card runs) rather
            // than a file-watcher on every module's data.
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            _refreshTimer.Tick += (s, e) => RefreshScore();
            _refreshTimer.Start();
        }

        // Flat rounded button template: the widget's buttons previously kept
        // WPF's stock Aero chrome underneath a transparent background, so on
        // hover the system's light-blue button face painted over the brand
        // gradient (and hid the white text). This template draws only what we
        // ask for, with real hover/pressed/disabled states.
        private ControlTemplate FlatButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var bd = new FrameworkElementFactory(typeof(Border), "Bd");
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bd.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            bd.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            bd.SetValue(Border.SnapsToDevicePixelsProperty, true);
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            cp.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Button.PaddingProperty));
            bd.AppendChild(cp);
            template.VisualTree = bd;

            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.88, "Bd"));
            template.Triggers.Add(hover);
            var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.72, "Bd"));
            template.Triggers.Add(pressed);
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.55, "Bd"));
            template.Triggers.Add(disabled);
            return template;
        }

        // 2px keyboard focus ring in the widget's own text color (WPF's
        // default dotted black focus rectangle is invisible on the dark
        // gradient).
        private static Style FocusRing(Brush brush)
        {
            var template = new ControlTemplate();
            var rect = new FrameworkElementFactory(typeof(System.Windows.Shapes.Rectangle));
            rect.SetValue(System.Windows.Shapes.Shape.StrokeProperty, brush);
            rect.SetValue(System.Windows.Shapes.Shape.StrokeThicknessProperty, 2.0);
            rect.SetValue(System.Windows.Shapes.Rectangle.RadiusXProperty, 7.0);
            rect.SetValue(System.Windows.Shapes.Rectangle.RadiusYProperty, 7.0);
            rect.SetValue(FrameworkElement.MarginProperty, new Thickness(-3));
            template.VisualTree = rect;
            var style = new Style();
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private Button MakeActionButton(string text, bool isPrimary, Brush textBrush, Brush borderBrush)
        {
            Brush background, foreground;
            if (_highContrast)
            {
                // Highlight/HighlightText is the pair Windows guarantees
                // contrast for; the secondary button stays Window/WindowText
                // with a border so the two remain distinguishable by more than
                // color alone.
                background = isPrimary ? SystemColors.HighlightBrush : SystemColors.WindowBrush;
                foreground = isPrimary ? SystemColors.HighlightTextBrush : SystemColors.WindowTextBrush;
            }
            else
            {
                background = isPrimary ? Brushes.White : new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
                foreground = isPrimary ? new SolidColorBrush(Color.FromRgb(0x0F, 0x2E, 0x27)) : textBrush;
            }

            return new Button
            {
                Content = text,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                MinHeight = 28,
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = Cursors.Hand,
                Background = background,
                Foreground = foreground,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(isPrimary && !_highContrast ? 0 : 1),
                FontWeight = isPrimary ? FontWeights.SemiBold : FontWeights.Normal,
                Template = FlatButtonTemplate(),
                FocusVisualStyle = FocusRing(textBrush)
            };
        }

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
                    ApplyResult(result);
                });
            });
        }

        private void ApplyResult(HealthScoreResult result)
        {
            var scoreString = result.Score.ToString();
            bool changed = _scoreText.Text != scoreString;
            _scoreText.Text = scoreString;
            AutomationProperties.SetName(_scoreRing, I18n.T("widget_health_score_label") + " " + scoreString);
            if (changed)
            {
                try
                {
                    var peer = UIElementAutomationPeer.FromElement(_scoreText) ?? UIElementAutomationPeer.CreatePeerForElement(_scoreText);
                    if (peer != null) peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
                }
                catch { }
            }
        }

        // Default position: bottom-right corner of the work area (bottom-left
        // in Hebrew, mirroring the reading direction the same way Toast
        // does), unless the user already dragged it somewhere and that
        // position still fits the current screen layout.
        private void PlacePosition()
        {
            var workArea = SystemParameters.WorkArea;
            bool neverSaved = _settings.WidgetLeft == -1 && _settings.WidgetTop == -1;
            bool fitsOnScreen = !neverSaved && ScreenHelper.FitsVirtualScreen(_settings.WidgetLeft, _settings.WidgetTop, 60);

            WindowStartupLocation = WindowStartupLocation.Manual;
            if (fitsOnScreen)
            {
                Left = _settings.WidgetLeft;
                Top = _settings.WidgetTop;
            }
            else
            {
                Left = I18n.IsRtl ? workArea.Left + 20 : workArea.Right - Width - 20;
                Top = workArea.Bottom - 150 - 20;
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
