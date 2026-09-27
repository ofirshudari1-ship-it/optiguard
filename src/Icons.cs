using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace UninstallerPro
{
    // v4.21 modernization: replaces the emoji glyphs the navigation used to
    // lean on (see MainWindow.cs NavGroups/NavIcons) with a small, consistent
    // hand-drawn vector icon set. Emoji render differently across Windows
    // versions/fonts and read as "low effort" no matter how polished the rest
    // of the UI is; these icons are theme-color-aware (they take whatever
    // Brush the caller passes, so they always match the active palette
    // exactly, including High Contrast), scale cleanly at any size, and share
    // one visual language: 2px stroke, round line caps/joins, a 24x24 design
    // grid, no fills except where a shape is meant to read as solid (the
    // shield). Adding a new icon means adding one more case below - every
    // icon already shares the stroke weight/line style automatically.
    public static class Icons
    {
        private const double StrokeWeight = 2.0;

        public static UIElement Make(string key, double size, Brush stroke)
        {
            var canvas = new Canvas { Width = 24, Height = 24 };
            AddShapes(canvas, key, stroke);
            return new Viewbox
            {
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                SnapsToDevicePixels = true,
                Child = canvas
            };
        }

        // Nav icons are recolored in place (hover/active/normal) instead of
        // being rebuilt, so the swap is instant and doesn't churn the visual
        // tree on every mouse move. Works because Make() always returns a
        // Viewbox > Canvas > Shape[] tree it fully owns.
        public static void Recolor(UIElement icon, Brush stroke)
        {
            var viewbox = icon as Viewbox;
            var canvas = viewbox != null ? viewbox.Child as Canvas : null;
            if (canvas == null) return;
            foreach (var child in canvas.Children)
            {
                var shape = child as Shape;
                if (shape == null) continue;
                if (shape.Fill != null && shape.Fill != Brushes.Transparent) shape.Fill = stroke;
                else shape.Stroke = stroke;
            }
        }

        private static Polyline Line(Brush stroke, params double[] xy)
        {
            var pts = new PointCollection();
            for (int i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Point(xy[i], xy[i + 1]));
            return new Polyline
            {
                Points = pts,
                Stroke = stroke,
                StrokeThickness = StrokeWeight,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = Brushes.Transparent
            };
        }

        private static Ellipse Circle(Brush stroke, double cx, double cy, double r, bool filled = false)
        {
            var e = new Ellipse
            {
                Width = r * 2,
                Height = r * 2,
                Stroke = filled ? null : stroke,
                Fill = filled ? stroke : Brushes.Transparent,
                StrokeThickness = StrokeWeight
            };
            Canvas.SetLeft(e, cx - r);
            Canvas.SetTop(e, cy - r);
            return e;
        }

        private static void AddShapes(Canvas c, string key, Brush stroke)
        {
            switch (key)
            {
                case "home": // Overview
                    c.Children.Add(Line(stroke, 3, 11, 12, 4, 21, 11));
                    c.Children.Add(Line(stroke, 5.5, 9.5, 5.5, 20, 18.5, 20, 18.5, 9.5));
                    c.Children.Add(Line(stroke, 9.5, 20, 9.5, 14, 14.5, 14, 14.5, 20));
                    break;

                case "apps": // Apps & Startup (package/box)
                    c.Children.Add(Line(stroke, 4, 7, 12, 3, 20, 7, 20, 17, 12, 21, 4, 17, 4, 7));
                    c.Children.Add(Line(stroke, 4, 7, 12, 11, 20, 7));
                    c.Children.Add(Line(stroke, 12, 11, 12, 21));
                    break;

                case "cleanup": // Cleanup & Storage (broom)
                    c.Children.Add(Line(stroke, 17.5, 3, 10, 13));
                    c.Children.Add(Line(stroke, 10, 13, 4.5, 19.5, 7, 21, 11.5, 15.5));
                    c.Children.Add(Line(stroke, 6, 18.5, 8.5, 20.5));
                    c.Children.Add(Line(stroke, 7.5, 16.7, 10, 18.7));
                    break;

                case "shield": // Security & Updates
                    c.Children.Add(Line(stroke, 12, 3, 20, 6, 20, 12, 12, 21, 4, 12, 4, 6, 12, 3));
                    c.Children.Add(Line(stroke, 8.5, 12, 11, 14.5, 16, 9));
                    break;

                case "gear": // Settings ("sliders" glyph - simple, modern)
                    c.Children.Add(Line(stroke, 4, 6, 20, 6));
                    c.Children.Add(Circle(stroke, 9, 6, 2.1));
                    c.Children.Add(Line(stroke, 4, 12, 20, 12));
                    c.Children.Add(Circle(stroke, 15, 12, 2.1));
                    c.Children.Add(Line(stroke, 4, 18, 20, 18));
                    c.Children.Add(Circle(stroke, 10.5, 18, 2.1));
                    break;

                case "close":
                    c.Children.Add(Line(stroke, 6, 6, 18, 18));
                    c.Children.Add(Line(stroke, 18, 6, 6, 18));
                    break;

                case "more": // kebab dots, used before "More actions" menus
                    c.Children.Add(Circle(stroke, 12, 6, 1.6, filled: true));
                    c.Children.Add(Circle(stroke, 12, 12, 1.6, filled: true));
                    c.Children.Add(Circle(stroke, 12, 18, 1.6, filled: true));
                    break;

                case "search": // empty/no-results states
                    c.Children.Add(Circle(stroke, 10.5, 10.5, 6));
                    c.Children.Add(Line(stroke, 15, 15, 20.5, 20.5));
                    break;

                case "refresh": // re-run a scan/score without a full page reload
                    c.Children.Add(Line(stroke, 20, 12, 20, 8, 16, 8));
                    c.Children.Add(Line(stroke, 20, 8.5, 17, 11.3));
                    c.Children.Add(new System.Windows.Shapes.Path
                    {
                        Stroke = stroke, StrokeThickness = StrokeWeight,
                        StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                        Data = Geometry.Parse("M 19 12 A 7 7 0 1 1 15.5 5.8")
                    });
                    c.Children.Add(Line(stroke, 4, 12, 4, 16, 8, 16));
                    c.Children.Add(Line(stroke, 4, 15.5, 7, 12.7));
                    break;

                case "check": // a setting/status that is in the safe/expected state
                    c.Children.Add(Line(stroke, 4, 12.5, 9.5, 18, 20, 6));
                    break;

                case "warning": // a setting/status that needs the user's attention
                    c.Children.Add(Line(stroke, 12, 3, 22, 20, 2, 20, 12, 3));
                    c.Children.Add(Line(stroke, 12, 9.5, 12, 14.5));
                    c.Children.Add(Circle(stroke, 12, 17.2, 1, filled: true));
                    break;

                default:
                    c.Children.Add(Circle(stroke, 12, 12, 8));
                    break;
            }
        }
    }
}
