using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class DesignerPanel : UserControl
{
    private readonly Artwork[] art = { new Artwork(true), new Artwork(false) };
    private int active;
    private GameArtwork game;
    private bool binding;
    internal bool Busy { get; private set; }
    private readonly GlassButton iconTab = Theme.Button("游戏图标", false), backgroundTab = Theme.Button("游戏背景", false);
    private readonly TextBox title = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, MaxLength = 500 };
    private readonly ChoiceBox font = new ChoiceBox { Dock = DockStyle.Fill, AccessibleName = "字体" };
    private readonly ChoiceBox fit = new ChoiceBox { Dock = DockStyle.Fill, AccessibleName = "图片适配方式" };
    private readonly NumericUpDown fontSize = Number(6, 120, 14), zoom = Number(25, 400, 100);
    private readonly NumericUpDown imageX = Number(-150, 150, 0), imageY = Number(-150, 150, 0);
    private readonly NumericUpDown textX = Number(0, 100, 50), textY = Number(0, 100, 80);
    private readonly Label info = Theme.Label("144 × 80 · ICON0.PNG", 9, false);
    private readonly Label message = Theme.Label("导入图片或输入文字，开始设计。", 9, false);
    private readonly Label source = Theme.Label("可选：打开游戏，读取原有图标和背景", 9, false);
    private readonly ArtPreview preview;
    private readonly TableLayoutPanel controls;
    private readonly GlassButton openGame = Theme.Button("打开游戏…", false), saveGame = Theme.Button("另存游戏副本…", true);
    private readonly GlassButton export = Theme.Button("导出当前 PNG…", false);
    private readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Fill, Visible = false, Height = 5 };
    private readonly ToolTip tips = new ToolTip();
    internal DesignerPanel()
    {
        Dock = DockStyle.Fill; BackColor = Color.Transparent; Font = new Font("Microsoft YaHei UI", 9F); ForeColor = Theme.Ink;
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(26, 10, 26, 14), ColumnCount = 2, RowCount = 4 };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 298)); outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 53)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
        Controls.Add(outer);
        var heading = Theme.Label("为游戏设计一张新面孔", 18, true); outer.Controls.Add(heading, 0, 0); outer.SetColumnSpan(heading, 2);

        var settings = new GlassPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 14, 0), Padding = new Padding(16, 10, 16, 12), AutoScroll = true };
        controls = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); settings.Controls.Add(controls); outer.Controls.Add(settings, 0, 1);
        Row(Pair(iconTab, backgroundTab, 50), 40); Row(info, 24);
        var load = Theme.Button("导入图片…", false); var loadText = Theme.Button("导入 TXT…", false);
        Row(Pair(load, loadText, 55), 40);
        Row(Theme.Label("叠加文字", 9, false), 25); Row(title, 54);
        Row(Theme.Label("字体 / 字号", 9, false), 25); Row(Pair(font, fontSize, 73), 30);
        var color = Theme.Button("文字颜色", false); var back = Theme.Button("底色", false);
        Row(Pair(color, back, 50), 36);
        Row(Theme.Label("图片适配 / 缩放 %", 9, false), 24); Row(Pair(fit, zoom, 65), 29);
        Row(Theme.Label("图片位置 · 水平 / 垂直", 9, false), 24); Row(Pair(imageX, imageY, 50), 29);
        Row(Theme.Label("文字位置 % · 水平 / 垂直", 9, false), 24); Row(Pair(textX, textY, 50), 29);
        var reset = Theme.Button("图片居中", false); Row(reset, 36);
        info.ForeColor = Theme.Muted;
        foreach (Control c in new Control[] { title, font, fit, fontSize, zoom, imageX, imageY, textX, textY }) Theme.InputStyle(c);
        fit.Items.AddRange(new object[] { "填满并裁切", "完整显示" });
        using (var installed = new System.Drawing.Text.InstalledFontCollection())
            foreach (var f in installed.Families.OrderBy(f => f.Name)) if (f.IsStyleAvailable(FontStyle.Regular) && f.IsStyleAvailable(FontStyle.Bold)) font.Items.Add(f.Name);
        iconTab.Click += delegate { SelectCanvas(0); }; backgroundTab.Click += delegate { SelectCanvas(1); };
        load.Click += delegate { BrowseImage(); };
        loadText.Click += delegate
        {
            using (var dialog = new OpenFileDialog { Filter = "文字文件 (*.txt)|*.txt", CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                Try(delegate
                {
                    if (new FileInfo(dialog.FileName).Length > 16384) throw new InvalidDataException("请使用小于 16 KB 的标题文字文件。");
                    string text = File.ReadAllText(dialog.FileName, new UTF8Encoding(false, true)).Trim();
                    title.Text = text.Substring(0, Math.Min(500, text.Length));
                });
            }
        };
        title.TextChanged += Changed; font.SelectedIndexChanged += Changed; fit.SelectedIndexChanged += Changed;
        foreach (var n in new[] { fontSize, zoom, imageX, imageY, textX, textY }) n.ValueChanged += Changed;
        color.Click += delegate { PickColor(true); }; back.Click += delegate { PickColor(false); };
        reset.Click += delegate { zoom.Value = 100; imageX.Value = imageY.Value = 0; };

        var previewCard = new GlassPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(17, 14, 17, 14), Tint = 80 };
        var previewLayout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, RowCount = 3, ColumnCount = 1 };
        previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27)); previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        previewLayout.Controls.Add(Theme.Label("实时预览", 11, true), 0, 0);
        preview = new ArtPreview(art) { Dock = DockStyle.Fill, Margin = Padding.Empty };
        previewLayout.Controls.Add(preview, 0, 1);
        var hint = Theme.Label("在上方画布拖动图片 · 下方为 PSP 菜单效果示意", 8.5F, false); hint.ForeColor = Theme.Muted;
        previewLayout.Controls.Add(hint, 0, 2); previewCard.Controls.Add(previewLayout); outer.Controls.Add(previewCard, 1, 1);
        preview.Pan += delegate(float x, float y)
        {
            imageX.Value = Math.Max(imageX.Minimum, Math.Min(imageX.Maximum, (decimal)(art[active].PanX + x)));
            imageY.Value = Math.Max(imageY.Minimum, Math.Min(imageY.Maximum, (decimal)(art[active].PanY + y)));
        };
        preview.AllowDrop = true;
        preview.DragEnter += delegate(object s, DragEventArgs e) { if (!Busy && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
        preview.DragDrop += delegate(object s, DragEventArgs e)
        {
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths != null && paths.Length == 1 && !Busy) Try(delegate { ImportImage(paths[0]); });
        };

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, ColumnCount = 4, RowCount = 1, Margin = new Padding(0, 10, 0, 0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 169));
        bottom.Controls.Add(openGame, 0, 0); source.Padding = new Padding(12, 0, 8, 0); source.ForeColor = Theme.Muted; bottom.Controls.Add(source, 1, 0);
        export.Margin = new Padding(0, 3, 10, 3); bottom.Controls.Add(export, 2, 0); bottom.Controls.Add(saveGame, 3, 0);
        outer.Controls.Add(bottom, 0, 2); outer.SetColumnSpan(bottom, 2);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty };
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 4)); footer.Controls.Add(message, 0, 0); footer.Controls.Add(progress, 0, 1);
        outer.Controls.Add(footer, 0, 3); outer.SetColumnSpan(footer, 2);
        openGame.Click += delegate { BrowseGame(); }; export.Click += delegate { Export(); }; saveGame.Click += async delegate { await SaveGame(); };
        tips.SetToolTip(source, "支持 PSP ISO 和 EBOOT.PBP。仅修改有编辑的图标或背景，另存新文件。");
        tips.SetToolTip(title, "文字会绘制在图片上，不会更改 PARAM.SFO 中的游戏名称。");
        SelectCanvas(0); UpdateActions();
    }
    private static NumericUpDown Number(decimal min, decimal max, decimal value)
    {
        return new NumericUpDown { Minimum = min, Maximum = max, Value = value, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center };
    }
    private static TableLayoutPanel Pair(Control left, Control right, float percent)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Color.Transparent };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, percent)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100 - percent));
        left.Margin = new Padding(0, 2, 5, 2); right.Margin = new Padding(5, 2, 0, 2); row.Controls.Add(left, 0, 0); row.Controls.Add(right, 1, 0); return row;
    }
    private void Row(Control control, int height)
    {
        int row = controls.RowCount++; controls.RowStyles.Add(new RowStyle(SizeType.Absolute, height)); controls.Controls.Add(control, 0, row);
    }
    private void SelectCanvas(int index)
    {
        active = index; binding = true; Artwork a = art[index];
        iconTab.Selected = index == 0; backgroundTab.Selected = index == 1; iconTab.Invalidate(); backgroundTab.Invalidate();
        title.Text = a.Text; font.SelectedItem = a.FontName; fontSize.Value = (decimal)a.FontSize;
        fit.SelectedIndex = a.Fill ? 0 : 1; zoom.Value = (decimal)(a.Zoom * 100); imageX.Value = (decimal)a.PanX; imageY.Value = (decimal)a.PanY;
        textX.Value = (decimal)a.TextX; textY.Value = (decimal)a.TextY;
        info.Text = a.Size.Width + " × " + a.Size.Height + " · " + a.FileName;
        preview.Active = index; binding = false; preview.Invalidate();
    }
    private void Changed(object sender, EventArgs e)
    {
        if (binding) return; Artwork a = art[active];
        a.Text = title.Text; a.FontName = font.SelectedItem as string ?? "Microsoft YaHei UI"; a.FontSize = (float)fontSize.Value;
        a.Fill = fit.SelectedIndex == 0; a.Zoom = (float)zoom.Value / 100; a.PanX = (float)imageX.Value; a.PanY = (float)imageY.Value;
        a.TextX = (float)textX.Value; a.TextY = (float)textY.Value; a.Dirty = true; preview.Invalidate(); UpdateActions();
    }
    private void PickColor(bool text)
    {
        using (var dialog = new ColorDialog { Color = text ? art[active].TextColor : art[active].BackColor, FullOpen = true })
        {
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (text) art[active].TextColor = dialog.Color; else art[active].BackColor = dialog.Color;
            art[active].Dirty = true; preview.Invalidate(); UpdateActions();
        }
    }
    private void BrowseImage()
    {
        using (var dialog = new OpenFileDialog { Filter = "图片 (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif", CheckFileExists = true })
            if (dialog.ShowDialog(this) == DialogResult.OK) Try(delegate { ImportImage(dialog.FileName); });
    }
    private void ImportImage(string path)
    {
        if (new FileInfo(path).Length > 64L * 1024 * 1024) throw new InvalidDataException("请使用不超过 64 MB 的图片。");
        art[active].SetImage(Artwork.Load(File.ReadAllBytes(path)), true); SelectCanvas(active); UpdateActions();
        Say("已导入 " + Path.GetFileName(path) + "，可拖动图片调整位置。", false);
    }
    private bool CanReplaceDesign()
    {
        return !art.Any(a => a.Dirty) || MessageBox.Show(this, "打开另一游戏会替换当前编辑的两张画布，是否继续？", "保留当前设计", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK;
    }
    private void BrowseGame()
    {
        using (var dialog = new OpenFileDialog { Filter = "PSP ISO / EBOOT.PBP|*.iso;*.pbp", CheckFileExists = true })
        {
            if (dialog.ShowDialog(this) != DialogResult.OK || !CanReplaceDesign()) return;
            Try(delegate
            {
                GameArtwork loaded = GameArtwork.Open(dialog.FileName);
                Bitmap icon = null, background = null;
                try { if (loaded.Icon != null) icon = Artwork.Load(loaded.Icon); if (loaded.Background != null) background = Artwork.Load(loaded.Background); }
                catch { if (icon != null) icon.Dispose(); if (background != null) background.Dispose(); throw; }
                art[0].Reset(); art[1].Reset(); art[0].SetImage(icon, false); art[1].SetImage(background, false);
                game = loaded; source.Text = Path.GetFileName(game.Path); tips.SetToolTip(source, game.Path); SelectCanvas(active); UpdateActions();
                Say(loaded.HasAnimation ? "已读取原图。游戏含动态图标，选中时仍会播放原动画；本页编辑静态图标。" : "已读取原图。编辑后另存游戏副本，原文件保留。", false);
            });
        }
    }
    private string Destination(string suggestion, string filter)
    {
        while (true)
        {
            using (var dialog = new SaveFileDialog { Title = "另存为", FileName = Path.GetFileName(suggestion),
                InitialDirectory = Path.GetDirectoryName(suggestion), Filter = filter, AddExtension = true, DefaultExt = Path.GetExtension(suggestion).TrimStart('.'), OverwritePrompt = false })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return null;
                suggestion = Path.ChangeExtension(dialog.FileName, Path.GetExtension(suggestion));
                if (!File.Exists(suggestion) && !Directory.Exists(suggestion) && (game == null || !string.Equals(suggestion, game.Path, StringComparison.OrdinalIgnoreCase))) return suggestion;
                MessageBox.Show(this, "输出已存在，请使用另一个文件名。", "输出已存在", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
    private void Export()
    {
        string folder = game == null ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) : Path.GetDirectoryName(game.Path);
        string output = Destination(Path.Combine(folder, art[active].FileName), "PNG 图片|*.png");
        if (output == null) return;
        Try(delegate
        {
            byte[] data = art[active].Png();
            string temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (Stream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write)) stream.Write(data, 0, data.Length);
                File.Move(temporary, output);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            Say("已导出 " + output, false);
        });
    }
    private async Task SaveGame()
    {
        if (game == null || !art.Any(a => a.Dirty)) return;
        string suggestion = Path.Combine(Path.GetDirectoryName(game.Path), Path.GetFileNameWithoutExtension(game.Path) + "_封面版" + Path.GetExtension(game.Path));
        string output = Destination(suggestion, "游戏文件|*" + Path.GetExtension(game.Path)); if (output == null) return;
        Busy = true; UpdateActions(); controls.Enabled = false; preview.Enabled = false; progress.Visible = true; progress.Value = 0;
        Say("正在生成游戏副本…", false);
        try
        {
            byte[] icon = art[0].Dirty ? art[0].Png() : null, background = art[1].Dirty ? art[1].Png() : null;
            var notify = new Progress<int>(value => { progress.Value = value; });
            await Task.Run(() => game.SaveAs(output, icon, background, value => ((IProgress<int>)notify).Report(value)));
            Say("已另存并校验图片资源：" + output, false);
        }
        catch (Exception error) { Say(error.Message, true); MessageBox.Show(this, error.Message, "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { Busy = false; controls.Enabled = preview.Enabled = true; progress.Visible = false; UpdateActions(); }
    }
    private void UpdateActions() { openGame.Enabled = export.Enabled = !Busy; saveGame.Enabled = !Busy && game != null && art.Any(a => a.Dirty); }
    private void Say(string text, bool error) { message.Text = text; message.ForeColor = error ? Color.FromArgb(255, 166, 177) : Theme.Muted; tips.SetToolTip(message, text); }
    private void Try(Action action) { try { action(); } catch (Exception error) { Say(error.Message, true); MessageBox.Show(this, error.Message, "无法完成", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
    protected override void Dispose(bool disposing) { if (disposing) { foreach (Artwork a in art) a.Dispose(); tips.Dispose(); } base.Dispose(disposing); }
}

internal sealed class ArtPreview : Control
{
    private readonly Artwork[] art;
    internal int Active;
    internal event Action<float, float> Pan;
    private RectangleF canvas;
    private Point drag;
    internal ArtPreview(Artwork[] artwork)
    {
        art = artwork; DoubleBuffered = true; SetStyle(ControlStyles.SupportsTransparentBackColor, true); BackColor = Color.Transparent; Cursor = Cursors.SizeAll;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        if (Width < 20 || Height < 40) return;
        float available = (Height - 52) / 2F;
        float width = Math.Min(Width - 10, available * 480 / 272);
        if (Active == 0) width = Math.Min(width, 360);
        float height = width * art[Active].Size.Height / art[Active].Size.Width;
        canvas = new RectangleF((Width - width) / 2, 22 + (available - height) / 2, width, height);
        TextRenderer.DrawText(g, art[Active].FileName + " · 编辑画布", Font, new Point(2, 1), Theme.Muted,
            TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform);
        using (Bitmap current = art[Active].Render()) g.DrawImage(current, canvas);
        using (var border = new Pen(Color.FromArgb(140, 188, 214, 242))) g.DrawRectangle(border, canvas.X, canvas.Y, canvas.Width, canvas.Height);
        float y = available + 35;
        TextRenderer.DrawText(g, "PSP 菜单效果", Font, new Point(2, (int)y), Theme.Muted,
            TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform);
        width = Math.Min(Width - 10, available * 480 / 272); height = width * 272 / 480;
        var screen = new RectangleF((Width - width) / 2, y + 23, width, height);
        using (Bitmap background = art[1].Render()) g.DrawImage(background, screen);
        float s = width / 480;
        using (var line = new Pen(Color.FromArgb(75, Color.White), 1)) g.DrawLine(line, screen.X + 25 * s, screen.Y + 48 * s, screen.Right - 25 * s, screen.Y + 48 * s);
        using (Bitmap icon = art[0].Render()) g.DrawImage(icon, new RectangleF(screen.X + 48 * s, screen.Y + 96 * s, 144 * s, 80 * s));
        using (var face = new Font("Microsoft YaHei UI", Math.Max(6, 11 * s)))
        using (var white = new SolidBrush(Color.White))
        {
            g.DrawString("游戏", face, white, screen.X + 48 * s, screen.Y + 24 * s);
            g.DrawString("游戏名称", face, white, screen.X + 214 * s, screen.Y + 118 * s);
        }
    }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left && canvas.Contains(e.Location)) { drag = e.Location; Capture = true; } }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Capture && e.Button == MouseButtons.Left && Pan != null && canvas.Width > 0)
        { Pan((e.X - drag.X) * 100 / canvas.Width, (e.Y - drag.Y) * 100 / canvas.Height); drag = e.Location; }
    }
    protected override void OnMouseUp(MouseEventArgs e) { Capture = false; base.OnMouseUp(e); }
}
