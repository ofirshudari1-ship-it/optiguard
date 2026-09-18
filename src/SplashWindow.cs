using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace UninstallerPro
{
    // Shown for a minimum of 800ms (even on a fast machine, so it never just
    // flickers) while the main window is constructed, per the shared
    // cross-tool standard (borderless, rounded corners, logo+name+version+
    // progress+status, subtle fade-in, closes itself). See Program.cs for
    // the timing/handoff to MainWindow.
    public class SplashWindow : Window
    {
        public TextBlock StatusText { get; private set; }

        public SplashWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = 420;
            Height = 210;
            Opacity = 0;

            var border = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x2E, 0x27)),
                Effect = new DropShadowEffect { BlurRadius = 32, ShadowDepth = 0, Opacity = 0.45, Color = Colors.Black }
            };

            var stack = new StackPanel { Margin = new Thickness(32), VerticalAlignment = VerticalAlignment.Center };

            stack.Children.Add(new TextBlock
            {
                Text = "OptiGuard",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            stack.Children.Add(new TextBlock
            {
                Text = "v" + Program.AppVersion,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xBF, 0xB0)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 24)
            });

            stack.Children.Add(new ProgressBar
            {
                IsIndeterminate = true,
                Height = 3,
                Margin = new Thickness(0, 0, 0, 14),
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
                BorderThickness = new Thickness(0)
            });

            StatusText = new TextBlock
            {
                Text = "Loading...",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8F, 0xBF, 0xB0)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stack.Children.Add(StatusText);

            border.Child = stack;
            Content = border;

            Loaded += (s, e) =>
            {
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220));
                BeginAnimation(OpacityProperty, fadeIn);
            };
        }
    }
}
