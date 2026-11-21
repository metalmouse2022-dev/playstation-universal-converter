using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

internal sealed class Artwork : IDisposable
{
    internal readonly Size Size;
    internal readonly string FileName;
    internal Bitmap Image;
    internal string Text = "", FontName = "Microsoft YaHei UI";
    internal float Zoom = 1, PanX, PanY, TextX = 50, TextY = 80, FontSize;
    internal bool Fill = true, Bold = true, Dirty;
    internal Color TextColor = Color.White, BackColor = Color.FromArgb(20, 29, 44);
    internal Artwork(bool icon) { Size = icon ? new Size(144, 80) : new Size(480, 272); FileName = icon ? "ICON0.PNG" : "PIC1.PNG"; FontSize = icon ? 14 : 30; }
    internal void SetImage(Bitmap bitmap, bool changed)
    {
        if (Image != null) Image.Dispose(); Image = bitmap; Zoom = 1; PanX = PanY = 0; Dirty = changed;
    }
    internal void Reset()
    {
        SetImage(null, false); Text = ""; TextX = 50; TextY = 80; Fill = true; Bold = true;
        FontSize = Size.Width == 144 ? 14 : 30; FontName = "Microsoft YaHei UI"; TextColor = Color.White;
    }
    internal Bitmap Render()
    {
        var result = new Bitmap(Size.Width, Size.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(result))
        {
            g.Clear(BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            if (Image != null)
            {
                float sx = (float)Size.Width / Image.Width, sy = (float)Size.Height / Image.Height;
                float scale = (Fill ? Math.Max(sx, sy) : Math.Min(sx, sy)) * Zoom;
                float w = Image.Width * scale, h = Image.Height * scale;
                using (var attr = new ImageAttributes())
                {
                    attr.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(Image, new Rectangle((int)((Size.Width - w) / 2 + PanX * Size.Width / 100),
                        (int)((Size.Height - h) / 2 + PanY * Size.Height / 100), (int)w, (int)h), 0, 0, Image.Width, Image.Height, GraphicsUnit.Pixel, attr);
                }
            }
            if (!string.IsNullOrWhiteSpace(Text))
            {
                using (var family = new FontFamily(FontName))
                using (var path = new GraphicsPath())
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    path.AddString(Text, family, (int)(Bold ? FontStyle.Bold : FontStyle.Regular), FontSize,
                        new PointF(Size.Width * TextX / 100, Size.Height * TextY / 100), format);
                    using (var outline = new Pen(Color.FromArgb(210, 5, 12, 20), Math.Max(1.6F, FontSize / 12)) { LineJoin = LineJoin.Round }) g.DrawPath(outline, path);
                    using (var brush = new SolidBrush(TextColor)) g.FillPath(brush, path);
                }
            }
        }
        return result;
    }
    internal byte[] Png()
    {
        using (Bitmap bitmap = Render())
        using (var stream = new MemoryStream()) { bitmap.Save(stream, ImageFormat.Png); return stream.ToArray(); }
    }
    internal static Bitmap Load(byte[] bytes)
    {
        using (var stream = new MemoryStream(bytes))
        using (var original = System.Drawing.Image.FromStream(stream))
        {
            if ((long)original.Width * original.Height > 80000000) throw new InvalidDataException("图片过大，请使用不超过 8000 万像素的图片。");
            var result = new Bitmap(original);
            if (Array.IndexOf(original.PropertyIdList, 0x112) >= 0)
            {
                int orientation = original.GetPropertyItem(0x112).Value[0];
                RotateFlipType[] transforms = { RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipNone,
                    RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipX,
                    RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate270FlipNone };
                if (orientation < transforms.Length) result.RotateFlip(transforms[orientation]);
            }
            return result;
        }
    }
    public void Dispose() { if (Image != null) Image.Dispose(); }
}
