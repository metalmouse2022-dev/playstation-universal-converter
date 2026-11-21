using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

internal static class Theme
{
    internal static readonly Color Ink = Color.FromArgb(240, 244, 250);
    internal static readonly Color Muted = Color.FromArgb(177, 190, 207);
    internal static readonly Color Input = Color.FromArgb(30, 40, 54);
    internal static readonly Color Accent = Color.FromArgb(167, 211, 255);
    internal static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath(); float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
    internal static Label Label(string text, float size, bool bold)
    {
        return new Label { Text = text, AutoSize = false, Dock = DockStyle.Fill, ForeColor = Ink,
            BackColor = Color.Transparent, Font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty, AutoEllipsis = true, UseMnemonic = false };
    }
    internal static GlassButton Button(string text, bool primary)
    {
        return new GlassButton { Text = text, Primary = primary, Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3) };
    }
    internal static void InputStyle(Control control)
    {
        control.BackColor = Input; control.ForeColor = Ink; control.Font = new Font("Microsoft YaHei UI", 9F);
    }
}

internal class GlassForm : Form
{
    private readonly Bitmap character = ToolBundle.Bitmap("character.png");
    private Bitmap scene, blurred;
    internal GlassForm() { DoubleBuffered = true; }
    private void EnsureScene()
    {
        if (scene != null && scene.Size == ClientSize) return;
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        if (scene != null) scene.Dispose(); if (blurred != null) blurred.Dispose();
        scene = new Bitmap(ClientSize.Width, ClientSize.Height);
        using (Graphics g = Graphics.FromImage(scene))
        {
            using (var fill = new LinearGradientBrush(ClientRectangle, Color.FromArgb(15, 25, 39), Color.FromArgb(26, 27, 43), 25F)) g.FillRectangle(fill, ClientRectangle);
            float h = ClientSize.Height * 1.08F, w = h * character.Width / character.Height;
            using (var attr = new ImageAttributes())
            {
                var matrix = new ColorMatrix(); matrix.Matrix33 = 0.23F; attr.SetColorMatrix(matrix);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(character, new Rectangle((int)(ClientSize.Width - w), -20, (int)w, (int)h), 0, 0, character.Width, character.Height, GraphicsUnit.Pixel, attr);
            }
            float left = Math.Max(0, ClientSize.Width - w) / ClientSize.Width;
            float end = Math.Min(0.98F, left + 0.25F);
            using (var fade = new LinearGradientBrush(ClientRectangle, Color.Black, Color.Transparent, 0F))
            {
                fade.InterpolationColors = new ColorBlend { Positions = new[] { 0F, Math.Max(0.001F, left), end, 1F },
                    Colors = new[] { Color.FromArgb(255, 15, 25, 39), Color.FromArgb(255, 15, 25, 39), Color.FromArgb(0, 15, 25, 39), Color.FromArgb(0, 15, 25, 39) } };
                g.FillRectangle(fade, ClientRectangle);
            }
            using (var veil = new LinearGradientBrush(ClientRectangle, Color.FromArgb(150, 13, 22, 35), Color.FromArgb(15, 13, 22, 35), 0F)) g.FillRectangle(veil, ClientRectangle);
        }
        using (var small = new Bitmap(Math.Max(1, Width / 28), Math.Max(1, Height / 28)))
        {
            using (Graphics g = Graphics.FromImage(small)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(scene, new Rectangle(Point.Empty, small.Size)); }
            blurred = new Bitmap(scene.Width, scene.Height);
            using (Graphics g = Graphics.FromImage(blurred)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.DrawImage(small, ClientRectangle); }
        }
    }
    internal void Backdrop(Graphics g, Control control, bool blur)
    {
        EnsureScene(); if (scene == null) return;
        Point origin = Point.Empty;
        for (Control c = control; c != null && c != this; c = c.Parent) origin.Offset(c.Left, c.Top);
        g.DrawImageUnscaled(blur ? blurred : scene, -origin.X, -origin.Y);
    }
    protected override void OnPaintBackground(PaintEventArgs e) { Backdrop(e.Graphics, this, false); }
    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(true); }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { character.Dispose(); if (scene != null) scene.Dispose(); if (blurred != null) blurred.Dispose(); }
        base.Dispose(disposing);
    }
}

internal class GlassPanel : Panel
{
    internal int Tint = 115;
    internal GlassPanel()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.Transparent;
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var form = FindForm() as GlassForm;
        if (form == null) { e.Graphics.Clear(Theme.Input); return; }
        form.Backdrop(e.Graphics, this, false);
        if (Width < 4 || Height < 4) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Theme.Round(new RectangleF(1, 1, Width - 3, Height - 3), 18))
        {
            // Transparent children repaint this panel with a translated, restricted graphics context.
            var state = e.Graphics.Save(); e.Graphics.SetClip(path, CombineMode.Intersect);
            form.Backdrop(e.Graphics, this, true);
            using (var brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(Tint, 49, 62, 80), Color.FromArgb(Tint + 35, 18, 28, 43), 70F)) e.Graphics.FillPath(brush, path);
            e.Graphics.Restore(state);
            using (var edge = new Pen(Color.FromArgb(65, 219, 238, 255))) e.Graphics.DrawPath(edge, path);
        }
    }
}

internal class GlassButton : Button
{
    internal bool Primary, Selected;
    private bool hover, down;
    internal GlassButton()
    {
        // Buttons are solid surfaces. Never ask transparent parents to repaint through them.
        BackColor = Theme.Input;
        SetStyle(ControlStyles.SupportsTransparentBackColor, false);
        SetStyle(ControlStyles.Opaque | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false;
        ForeColor = Theme.Ink; Cursor = Cursors.Hand; UseMnemonic = false;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using (var background = new SolidBrush(BackColor)) e.Graphics.FillRectangle(background, ClientRectangle);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        // Opaque controls must repaint every pixel, including rounded corners and old text.
        OnPaintBackground(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color fill = !Enabled ? Color.FromArgb(35, 45, 59) : Primary || Selected ?
            (down ? Color.FromArgb(39, 76, 114) : hover ? Color.FromArgb(62, 115, 171) : Color.FromArgb(49, 94, 142)) :
            (down ? Color.FromArgb(34, 47, 64) : hover ? Color.FromArgb(54, 73, 97) : Color.FromArgb(42, 56, 76));
        using (var path = Theme.Round(new RectangleF(1, 1, Width - 3, Height - 3), 10))
        using (var brush = new SolidBrush(fill))
        using (var pen = new Pen(Focused ? Color.FromArgb(198, 225, 255) : Selected ? Color.FromArgb(142, 181, 221) : Color.FromArgb(91, 111, 137)))
        { e.Graphics.FillPath(brush, path); e.Graphics.DrawPath(pen, path); }
        // GDI text must retain the GDI+ offset/clip used by WinForms transparent repainting.
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(7, 3, Width - 14, Height - 6), Enabled ? Theme.Ink : Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
            TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform);
    }
}

internal sealed class ChoiceBox : GlassButton
{
    internal readonly List<object> Items = new List<object>();
    private int index = -1;
    internal event EventHandler SelectedIndexChanged;
    internal int SelectedIndex
    {
        get { return index; }
        set
        {
            if (index == value) return;
            index = value; Text = index < 0 ? "选择…" : Items[index].ToString() + "  ▾";
            Invalidate(); if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
        }
    }
    internal object SelectedItem { get { return index < 0 ? null : Items[index]; } set { SelectedIndex = Items.IndexOf(value); } }
    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        var menu = new ContextMenuStrip { BackColor = Theme.Input, ForeColor = Theme.Ink, Font = Font, MaximumSize = new Size(Math.Max(Width, 200), 480), Renderer = new DarkMenuRenderer() };
        for (int i = 0; i < Items.Count; i++)
        {
            int selected = i; var item = new ToolStripMenuItem(Items[i].ToString()) { Checked = i == index, ForeColor = Theme.Ink };
            item.Click += delegate { SelectedIndex = selected; }; menu.Items.Add(item);
        }
        menu.Closed += delegate { menu.Dispose(); }; menu.Show(this, new Point(0, Height));
    }
    private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            using (var fill = new SolidBrush(e.Item.Selected ? Color.FromArgb(51, 78, 111) : Theme.Input)) e.Graphics.FillRectangle(fill, new Rectangle(Point.Empty, e.Item.Size));
        }
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { using (var fill = new SolidBrush(Theme.Input)) e.Graphics.FillRectangle(fill, e.AffectedBounds); }
    }
}
