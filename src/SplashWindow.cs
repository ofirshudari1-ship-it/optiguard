using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace UninstallerPro
{
    // Branded splash screen, built to the mandatory cross-tool template in
    // STANDARDS.md §19 (modeled on HOMEY AI's desktop/splash.html+main.js):
    //  1. Gradient background in the shared cross-tool palette (STANDARDS.md
    //     §21: dark base #0B1220 -> accent blue #2F6FED), not a flat/generic/
    //     white background and no longer OptiGuard's own green.
    //  2. The real app logo (AppIcon.ico) as the large, dominant, centered
    //     element - not a small icon or text-only.
    //  3. Frameless, transparent window with rounded corners (not a square
    //     system-bordered window).
    //  4. A subtle continuous rotation spinner - not a fake/marquee progress
    //     bar with no real percentage to report.
    //  5+6. Minimum-display-time and safety-timeout enforcement, plus the
    //     hidden-until-ready handoff to MainWindow, live in Program.cs
    //     (SplashWindow itself only renders and animates).
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
            Height = 280;
            Opacity = 0;

            // Shared cross-tool brand gradient (STANDARDS.md §21.1/§21.2):
            // dark base #0B1220 shading into the shared "IObit blue" accent
            // #2F6FED - OptiGuard, Playnest, ActionClip and SnapCap all use
            // this exact gradient in splash/installer so the four tools read
            // as one company's suite. OptiGuard's own green stays only in the
            // running app itself (main window/tray/widget), not here.
            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0x0B, 0x12, 0x20), 0.0),
                    new GradientStop(Color.FromRgb(0x13, 0x1B, 0x2E), 0.5),
                    new GradientStop(Color.FromRgb(0x2F, 0x6F, 0xED), 1.0)
                }
            };

            // STANDARDS.md 20.2: under a Windows contrast theme, no brand
            // gradient/shadow behind text - the user's own system colors.
            bool hc = Theme.IsSystemHighContrast;
            var border = new Border
            {
                CornerRadius = new CornerRadius(20),
                Background = hc ? (Brush)SystemColors.WindowBrush : gradient,
                BorderBrush = hc ? SystemColors.WindowTextBrush : null,
                BorderThickness = new Thickness(hc ? 2 : 0),
                Effect = hc ? null : new DropShadowEffect { BlurRadius = 32, ShadowDepth = 0, Opacity = 0.45, Color = Colors.Black }
            };

            var stack = new StackPanel { Margin = new Thickness(32, 30, 32, 26), VerticalAlignment = VerticalAlignment.Center };

            // Real logo - large and dominant, not a small corner icon.
            var logo = new Image
            {
                Width = 96,
                Height = 96,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14),
                Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 0, Opacity = 0.35, Color = Colors.Black }
            };
            // BitmapScalingMode.HighQuality avoids visible blockiness if the
            // rendered size ever falls between the .ico's embedded frame sizes.
            RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
            try
            {
                // AppIcon.ico embeds several frames (16..256px). BitmapImage's
                // simple UriSource path decodes frame 0, which .NET's icon
                // encoder does not guarantee to be the largest - on this icon
                // it resolved to a small frame that got upscaled to 192px and
                // came out visibly pixelated. Decode every frame explicitly
                // and pick the highest-resolution one instead.
                var resourceUri = new Uri("pack://application:,,,/AppIcon.ico", UriKind.Absolute);
                var streamInfo = Application.GetResourceStream(resourceUri);
                if (streamInfo != null)
                {
                    using (streamInfo.Stream)
                    {
                        var decoder = BitmapDecoder.Create(streamInfo.Stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                        var bestFrame = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
                        logo.Source = bestFrame;
                    }
                }
                if (logo.Source != null) stack.Children.Add(logo);
            }
            catch
            {
                // If the icon resource can't be decoded for any reason, fall back
                // to the text-only header below rather than breaking startup.
            }

            stack.Children.Add(new TextBlock
            {
                Text = "OptiGuard",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xEA, 0xEA, 0xEA)),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            stack.Children.Add(new TextBlock
            {
                Text = "v" + Program.AppVersion,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 22)
            });

            // Subtle continuous rotation spinner - a real "loading" cue, not a
            // fake determinate/marquee progress bar with no true percentage.
            var spinner = BuildSpinner();
            stack.Children.Add(spinner);

            StatusText = new TextBlock
            {
                Text = "Loading...",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            };
            stack.Children.Add(StatusText);

            border.Child = stack;
            Content = border;
            if (hc) ApplySystemTextColors(stack);

            Loaded += (s, e) =>
            {
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220));
                BeginAnimation(OpacityProperty, fadeIn);
            };
        }

        private static void ApplySystemTextColors(DependencyObject root)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                var tb = child as TextBlock;
                if (tb != null) tb.Foreground = SystemColors.WindowTextBrush;
                var shape = child as Shape;
                if (shape != null) shape.Stroke = SystemColors.WindowTextBrush;
                ApplySystemTextColors(child);
            }
        }

        private static FrameworkElement BuildSpinner()
        {
            const double size = 34;
            const double thickness = 3.5;

            var canvas = new Grid { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center };

            // Faint full ring underneath, so the spinning arc has something to
            // read against (matches the quiet, continuous feel called for in §19.1).
            canvas.Children.Add(new Ellipse
            {
                Width = size,
                Height = size,
                Stroke = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                StrokeThickness = thickness
            });

            // The rotating arc itself - a partial ring (270 degrees) so the
            // motion is unambiguous, spun by a Storyboard/RotateTransform
            // forever, i.e. genuine "still loading" motion rather than a
            // progress bar implying a percentage we can't actually measure.
            var arc = new Path
            {
                Width = size,
                Height = size,
                Stroke = Brushes.White,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            double r = (size - thickness) / 2;
            double cx = size / 2, cy = size / 2;
            var startPoint = new Point(cx, cy - r);
            var endPoint = new Point(cx + r * Math.Sin(Math.PI * 1.5), cy - r * Math.Cos(Math.PI * 1.5));
            var figure = new PathFigure { StartPoint = startPoint, IsClosed = false };
            figure.Segments.Add(new ArcSegment
            {
                Point = endPoint,
                Size = new Size(r, r),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = true
            });
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            arc.Data = geometry;

            var rotate = new RotateTransform(0);
            arc.RenderTransform = rotate;

            var spin = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromMilliseconds(900),
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(spin, arc);
            Storyboard.SetTargetProperty(spin, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
            var storyboard = new Storyboard();
            storyboard.Children.Add(spin);

            canvas.Children.Add(arc);
            canvas.Loaded += (s, e) => storyboard.Begin();

            return canvas;
        }
    }
}
