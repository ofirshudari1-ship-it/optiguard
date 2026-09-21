using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace UninstallerPro
{
    // Non-blocking passive notification, per the shared cross-tool standard
    // (section 15.2): top corner (right in LTR, left in RTL so it doesn't
    // collide with reading direction), fixed 300-400px width, 3-6s auto-dismiss
    // with an always-available manual close. Use for "FYI, something happened
    // in the background" - never for anything the user must acknowledge or
    // act on (those stay as Dialogs.Info/Confirm/ShowError, which correctly
    // block until dismissed).
    public static class Toast
    {
        public static void Show(string message)
        {
            try
            {
                var win = new Window
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    ResizeMode = ResizeMode.NoResize,
                    ShowInTaskbar = false,
                    Topmost = true,
                    Width = 340,
                    SizeToContent = SizeToContent.Height,
                    Opacity = 0,
                    // Toast is a plain top-level Window (not a child of MainWindow),
                    // so it does NOT inherit MainWindow's FlowDirection - without this
                    // Hebrew toast text renders left-aligned/LTR-flowed even though the
                    // rest of the app is in RTL (section 18.1).
                    FlowDirection = I18n.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
                };

                var workArea = SystemParameters.WorkArea;
                win.Left = I18n.IsRtl ? workArea.Left + 16 : workArea.Right - 340 - 16;
                win.Top = workArea.Top + 16;

                var border = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Background = Theme.Get("PanelBrush"),
                    BorderBrush = Theme.Get("AccentBrush"),
                    BorderThickness = new Thickness(1),
                    Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 2, Opacity = 0.25, Color = Colors.Black }
                };
                var row = new DockPanel { Margin = new Thickness(16, 12, 12, 12) };
                border.Child = row;
                win.Content = border;

                var btnClose = new Button
                {
                    Content = "✕",
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = Theme.Get("TextMutedBrush"),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Top,
                    Padding = new Thickness(4)
                };
                // Icon-only button ("✕" glyph as Content) - without an explicit
                // AutomationProperties.Name, Narrator reads the raw character
                // instead of a meaningful label (section 18.2).
                AutomationProperties.SetName(btnClose, I18n.T("btn_close"));
                DockPanel.SetDock(btnClose, I18n.IsRtl ? Dock.Left : Dock.Right);
                row.Children.Add(btnClose);

                var text = new TextBlock
                {
                    Text = message,
                    Foreground = Theme.Get("TextBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 13
                };
                row.Children.Add(text);

                Action close = () =>
                {
                    var fadeOut = new DoubleAnimation(win.Opacity, 0, TimeSpan.FromMilliseconds(200));
                    fadeOut.Completed += (s2, e2) => { try { win.Close(); } catch { } };
                    win.BeginAnimation(Window.OpacityProperty, fadeOut);
                };
                btnClose.Click += (s, e) => close();

                win.Loaded += (s, e) =>
                {
                    win.BeginAnimation(Window.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
                };

                win.Show();

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timer.Tick += (s, e) => { timer.Stop(); close(); };
                timer.Start();
            }
            catch { }
        }
    }
}
