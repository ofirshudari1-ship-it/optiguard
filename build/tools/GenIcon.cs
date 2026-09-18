using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

class GenIcon
{
    static Bitmap DrawFrame(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float radius = size * 0.24f;
            var rect = new RectangleF(size * 0.03f, size * 0.03f, size * 0.94f, size * 0.94f);
            using (var path = RoundedRect(rect, radius))
            using (var brush = new LinearGradientBrush(rect, Color.FromArgb(0x10, 0xB9, 0x81), Color.FromArgb(0x04, 0x78, 0x57), 45f))
            {
                g.FillPath(brush, path);
            }

            // מגן (Guard) לבן מלא - מסמל הגנה/בטיחות (נקודות שחזור, Undo) -
            // עם סימן וי בצבע כהה בפנים, מסמל "מותאם/מטופל". קריא בגדלים קטנים.
            Func<float, float> X = f => size * f;
            Func<float, float> Y = f => size * f;
            var shield = new[]
            {
                new PointF(X(0.30f), Y(0.14f)),
                new PointF(X(0.70f), Y(0.14f)),
                new PointF(X(0.80f), Y(0.25f)),
                new PointF(X(0.72f), Y(0.55f)),
                new PointF(X(0.50f), Y(0.89f)),
                new PointF(X(0.28f), Y(0.55f)),
                new PointF(X(0.20f), Y(0.25f)),
            };
            using (var shadowBrush = new SolidBrush(Color.FromArgb(45, 0, 0, 0)))
            {
                var shadow = shield.Select(p => new PointF(p.X, p.Y + size * 0.025f)).ToArray();
                g.FillPolygon(shadowBrush, shadow);
            }
            using (var shieldBrush = new SolidBrush(Color.White))
            {
                g.FillPolygon(shieldBrush, shield);
            }

            float cx = size * 0.5f, cy = size * 0.47f;
            float s = size * 0.155f;
            var p1 = new PointF(cx - s * 0.95f, cy - s * 0.05f);
            var p2 = new PointF(cx - s * 0.25f, cy + s * 0.55f);
            var p3 = new PointF(cx + s * 1.05f, cy - s * 0.65f);
            using (var pen = new Pen(Color.FromArgb(0x04, 0x78, 0x57), Math.Max(2f, size * 0.075f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.DrawLines(pen, new[] { p1, p2, p3 });
            }
        }
        return bmp;
    }

    static PointF Offset(PointF p, float dx, float dy) { return new PointF(p.X + dx, p.Y + dy); }

    static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    static void SaveIco(int[] sizes, string outputPath)
    {
        using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            var pngs = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++)
            {
                using (var bmp = DrawFrame(sizes[i]))
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    pngs[i] = ms.ToArray();
                }
            }

            bw.Write((short)0);
            bw.Write((short)1);
            bw.Write((short)sizes.Length);

            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                bw.Write((byte)(s >= 256 ? 0 : s));
                bw.Write((byte)(s >= 256 ? 0 : s));
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((short)1);
                bw.Write((short)32);
                bw.Write(pngs[i].Length);
                bw.Write(offset);
                offset += pngs[i].Length;
            }
            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write(pngs[i]);
            }
        }
    }

    static void Main(string[] args)
    {
        string outPath = args.Length > 0 ? args[0] : "AppIcon.ico";
        SaveIco(new[] { 16, 24, 32, 48, 64, 128, 256 }, outPath);
        Console.WriteLine("Saved " + outPath);
    }
}
