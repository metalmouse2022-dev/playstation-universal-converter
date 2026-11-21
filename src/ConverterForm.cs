using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class ConverterForm : GlassForm
{
    private static readonly Color Canvas = Color.FromArgb(16, 22, 33);
    private static readonly Color Surface = Color.FromArgb(25, 34, 51);
    private static readonly Color Ink = Color.FromArgb(239, 243, 251);
    private static readonly Color Secondary = Color.FromArgb(175, 188, 211);
    private static readonly Color Line = Color.FromArgb(98, 115, 148);
    private static readonly Color Accent = Color.FromArgb(157, 182, 255);
    private static readonly Color Blue = Color.FromArgb(50, 86, 198);
    private static readonly Color BlueHover = Color.FromArgb(58, 94, 210);
    private static readonly Color Hover = Color.FromArgb(37, 52, 79);
    private static readonly Color ErrorInk = Color.FromArgb(255, 160, 169);
    private readonly Label fileTitle = new Label();
    private readonly Label fileType = new Label();
    private readonly Label status = new Label();
    private readonly Label detail = new Label();
    private readonly Button browse = new GlassButton();
    private readonly Button firstChoice = new GlassButton();
    private readonly Button secondChoice = new GlassButton();
    private readonly Button extractChoice = new GlassButton();
    private readonly LinkLabel outputLink = new LinkLabel();
    private readonly ProgressBar progress = new ProgressBar();
    private readonly Panel dropPanel = new GlassPanel();
    private readonly TableLayoutPanel layout = new TableLayoutPanel();
    private readonly CheckBox combine = new CheckBox();
    private readonly RowStyle actionRow = new RowStyle(SizeType.Absolute, 0);
    private readonly RowStyle combineRow = new RowStyle(SizeType.Absolute, 0);
    private readonly RowStyle extractRow = new RowStyle(SizeType.Absolute, 0);
    private readonly ToolTip tips = new ToolTip();
    private readonly string chdman = ToolBundle.Chdman;
    private readonly string packager = ToolBundle.Packager;
    private DesignerPanel designer;
    private DiscSet selection;
    private string outputFolder;
    private bool busy;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    internal ConverterForm()
    {
        Text = "PlayStation万能转换姬 · bilibili: metalmouse制作";
        Font = new Font("Microsoft YaHei UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(1040, 780);
        MinimumSize = new Size(1000, 790);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Canvas;
        ForeColor = Ink;
        using (Stream icon = ToolBundle.Resource("app.ico")) Icon = new Icon(icon);

        layout.Dock = DockStyle.Fill;
        layout.BackColor = Color.Transparent;
        layout.Padding = new Padding(28, 22, 28, 18);
        layout.ColumnCount = 2;
        layout.RowCount = 6;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 53));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(combineRow);
        layout.RowStyles.Add(actionRow);
        layout.RowStyles.Add(extractRow);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        Controls.Add(layout);

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = Color.Transparent };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        var title = new Label { Text = "PlayStation 镜像转换", Font = new Font(Font.FontFamily, 19F, FontStyle.Bold) };
        StyleLabel(title, Ink, ContentAlignment.MiddleLeft);
        header.Controls.Add(title, 0, 0);
        outputLink.Text = "输出目录…";
        outputLink.LinkColor = Accent;
        outputLink.ActiveLinkColor = Color.White;
        outputLink.TextAlign = ContentAlignment.MiddleRight;
        outputLink.Dock = DockStyle.Fill;
        outputLink.LinkClicked += SelectOutputFolder;
        tips.SetToolTip(outputLink, "默认保存到输入镜像旁。");
        header.Controls.Add(outputLink, 1, 0);
        layout.Controls.Add(header, 0, 0);
        layout.SetColumnSpan(header, 2);

        dropPanel.BackColor = Color.Transparent;
        dropPanel.Dock = DockStyle.Fill;
        dropPanel.Margin = Padding.Empty;
        var picker = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = Color.Transparent, ColumnCount = 3, RowCount = 5,
            Padding = new Padding(18, 10, 18, 10), Margin = Padding.Empty
        };
        picker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        picker.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        picker.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        picker.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        picker.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        picker.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        picker.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
        picker.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        fileTitle.Text = "拖入游戏镜像";
        fileTitle.Font = new Font(Font.FontFamily, 13F, FontStyle.Bold);
        StyleLabel(fileTitle, Ink, ContentAlignment.MiddleCenter);
        fileType.Text = "ISO  /  BIN + CUE  /  CHD  /  PBP";
        StyleLabel(fileType, Secondary, ContentAlignment.MiddleCenter);
        picker.Controls.Add(fileTitle, 0, 1);
        picker.SetColumnSpan(fileTitle, 3);
        picker.Controls.Add(fileType, 0, 2);
        picker.SetColumnSpan(fileType, 3);
        StyleButton(browse, "选择文件…", false);
        browse.Click += Browse;
        picker.Controls.Add(browse, 1, 3);
        dropPanel.Controls.Add(picker);
        layout.Controls.Add(dropPanel, 0, 1);
        layout.SetColumnSpan(dropPanel, 2);

        combine.Text = "制作合盘版";
        combine.BackColor = Color.Transparent;
        combine.Dock = DockStyle.Fill;
        combine.Margin = Padding.Empty;
        combine.Visible = false;
        combine.CheckedChanged += delegate { SetChoiceLabels(); };
        layout.Controls.Add(combine, 0, 2);
        layout.SetColumnSpan(combine, 2);
        tips.SetToolTip(combine, "实机生成一个多碟 PBP，模拟器生成 CHD 和 M3U 换盘列表。");

        StyleButton(firstChoice, "", true);
        StyleButton(secondChoice, "", false);
        firstChoice.Margin = new Padding(0, 8, 6, 8);
        secondChoice.Margin = new Padding(6, 8, 0, 8);
        firstChoice.Click += async delegate { await ConvertSelection(selection.Images[0].Platform == GamePlatform.Psp ? Destination.Hardware : Destination.Emulator); };
        secondChoice.Click += async delegate { await ConvertSelection(selection.Images[0].Platform == GamePlatform.Psp ? Destination.Emulator : Destination.Hardware); };
        firstChoice.Visible = secondChoice.Visible = false;
        layout.Controls.Add(firstChoice, 0, 3);
        layout.Controls.Add(secondChoice, 1, 3);

        var feedback = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty, BackColor = Color.Transparent };
        feedback.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        feedback.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        feedback.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
        StyleLabel(status, Ink, ContentAlignment.MiddleLeft);
        StyleLabel(detail, Secondary, ContentAlignment.MiddleLeft);
        progress.Dock = DockStyle.Fill;
        progress.Margin = Padding.Empty;
        progress.Style = ProgressBarStyle.Marquee;
        progress.MarqueeAnimationSpeed = 25;
        progress.Visible = false;
        feedback.Controls.Add(status, 0, 0);
        feedback.Controls.Add(detail, 0, 1);
        feedback.Controls.Add(progress, 0, 2);
        StyleButton(extractChoice, "导出 BIN + CUE", false);
        extractChoice.Margin = new Padding(0, 0, 0, 8);
        extractChoice.Visible = false;
        extractChoice.Click += async delegate { await ExtractChd(); };
        layout.Controls.Add(extractChoice, 0, 4);
        layout.SetColumnSpan(extractChoice, 2);
        layout.Controls.Add(feedback, 0, 5);
        layout.SetColumnSpan(feedback, 2);

        foreach (Control control in new Control[] { layout, dropPanel, picker, fileTitle, fileType })
        {
            control.AllowDrop = true;
            control.DragEnter += CheckDrop;
            control.DragLeave += delegate { SetDrag(false); };
            control.DragDrop += Drop;
        }
        FormClosing += delegate(object sender, FormClosingEventArgs e)
        {
            if ((busy || designer.Busy) && e.CloseReason == CloseReason.UserClosing)
                e.Cancel = true;
        };
        BuildShell();
        SetStatus("拖入镜像，自动识别平台", "默认保存在原镜像旁 · 遇到同名输出可另存");
    }

    private void BuildShell()
    {
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, RowCount = 2, ColumnCount = 1, Margin = Padding.Empty };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 78)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var navigation = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, ColumnCount = 4, RowCount = 1, Padding = new Padding(28, 17, 28, 8), Margin = Padding.Empty };
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62)); navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148)); navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 222));
        var logo = new PictureBox { Image = ToolBundle.Bitmap("playstation.png"), Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent, Margin = new Padding(0, 0, 14, 0) };
        navigation.Controls.Add(logo, 0, 0);
        var brand = Theme.Label("PlayStation万能转换姬\r\nbilibili: metalmouse制作", 10, false); navigation.Controls.Add(brand, 1, 0);
        var convertTab = Theme.Button("镜像转换", false); var designTab = Theme.Button("PSP图标/背景设计", false);
        convertTab.AccessibleRole = designTab.AccessibleRole = AccessibleRole.PageTab;
        convertTab.Margin = new Padding(0, 2, 8, 2); navigation.Controls.Add(convertTab, 2, 0); navigation.Controls.Add(designTab, 3, 0);
        var pages = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty };
        Controls.Remove(layout); pages.Controls.Add(layout);
        designer = new DesignerPanel { Visible = false }; pages.Controls.Add(designer);
        convertTab.Selected = true;
        convertTab.Click += delegate { designer.Visible = false; layout.Visible = true; layout.BringToFront(); convertTab.Selected = true; designTab.Selected = false; convertTab.Invalidate(); designTab.Invalidate(); };
        designTab.Click += delegate { layout.Visible = false; designer.Visible = true; designer.BringToFront(); convertTab.Selected = false; designTab.Selected = true; convertTab.Invalidate(); designTab.Invalidate(); };
        shell.Controls.Add(navigation, 0, 0); shell.Controls.Add(pages, 0, 1); Controls.Add(shell);
        FormClosed += delegate { logo.Image.Dispose(); };
    }

    private static void StyleLabel(Label label, Color color, ContentAlignment alignment)
    {
        label.Dock = DockStyle.Fill;
        label.Margin = Padding.Empty;
        label.AutoEllipsis = true;
        label.ForeColor = color;
        label.TextAlign = alignment;
        label.UseMnemonic = false;
        label.BackColor = Color.Transparent;
    }

    private static void StyleButton(Button button, string text, bool primary)
    {
        button.Text = text;
        button.Dock = DockStyle.Fill;
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = primary ? Blue : Surface;
        button.ForeColor = primary ? Color.White : Ink;
        button.FlatAppearance.BorderColor = primary ? Blue : Line;
        button.FlatAppearance.MouseOverBackColor = primary ? BlueHover : Hover;
        button.FlatAppearance.MouseDownBackColor = primary ? BlueHover : Hover;
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
        button.UseMnemonic = false;
        var glass = button as GlassButton;
        if (glass != null) { glass.Primary = primary; glass.BackColor = Theme.Input; }
    }

    private void SetDrag(bool active)
    {
        ((GlassPanel)dropPanel).Tint = active ? 160 : 115;
        dropPanel.Invalidate();
    }

    private void CheckDrop(object sender, DragEventArgs e)
    {
        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (!busy && files != null && files.Length > 0)
        {
            e.Effect = DragDropEffects.Copy;
            SetDrag(true);
        }
    }

    private async void Drop(object sender, DragEventArgs e)
    {
        SetDrag(false);
        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (!busy && files != null && files.Length > 0)
            await LoadImages(files);
    }

    private async void Browse(object sender, EventArgs e)
    {
        using (var dialog = new OpenFileDialog
        {
            Filter = "游戏镜像 (*.iso;*.cue;*.bin;*.chd;*.pbp)|*.iso;*.cue;*.bin;*.chd;*.pbp",
            CheckFileExists = true,
            Multiselect = true
        })
        {
            if (dialog.ShowDialog(this) == DialogResult.OK)
                await LoadImages(dialog.FileNames);
        }
    }

    private void SelectOutputFolder(object sender, LinkLabelLinkClickedEventArgs e)
    {
        using (var dialog = new FolderBrowserDialog
        {
            Description = "选择转换文件的保存目录",
            SelectedPath = outputFolder ?? (selection == null ? "" : Path.GetDirectoryName(selection.Images[0].FilePath))
        })
        {
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            outputFolder = dialog.SelectedPath;
            tips.SetToolTip(outputLink, outputFolder);
            SetStatus("已设置输出目录", outputFolder);
        }
    }

    private async Task LoadImages(string[] paths)
    {
        selection = null;
        firstChoice.Visible = secondChoice.Visible = false;
        extractChoice.Visible = false;
        extractRow.Height = 0;
        combine.Visible = combine.Checked = false;
        combineRow.Height = 0;
        actionRow.Height = 0;
        layout.PerformLayout();
        fileTitle.Text = paths.Length == 1 ? Path.GetFileName(paths[0]) : paths.Length + " 个文件";
        fileType.Text = "正在识别平台…";
        tips.SetToolTip(fileTitle, string.Join(Environment.NewLine, paths.Select(Path.GetFileName)));
        SetStatus("正在读取镜像", "");
        SetBusy(true);
        try
        {
            selection = await Task.Run(() => DiscSet.Read(paths, chdman));
            ShowSelection();
        }
        catch (Exception error)
        {
            fileType.Text = "无法识别镜像";
            SetStatus("识别失败", error.Message, true);
        }
        finally { SetBusy(false); }

        if (selection == null || selection.Images[0].Platform != GamePlatform.Ps2)
            return;

        GameImage ps2 = selection.Images[0];
        // CHD already works in emulators; leave the ISO choice available without auto-converting.
        if (ps2.Extension == ".chd")
        {
            if (ps2.Medium == DiscMedium.Unknown)
                SetStatus("无法确认 PS2 光盘介质", "CHD 缺少可确认的 CD/DVD 信息，无法安全导出 ISO。", true);
            return;
        }
        if (ps2.Medium != DiscMedium.Unknown || ps2.IsReadyFor(Destination.Emulator))
            await ConvertSelection(Destination.Emulator);
        else
            SetStatus("无法确认 PS2 光盘介质", "2048 字节扇区镜像可能来自 CD 或 DVD。如有原始 CUE，请选择 CUE。", true);
    }

    private void ShowSelection()
    {
        GameImage current = selection.Images[0];
        bool multi = selection.Images.Length > 1;
        string platform = current.Platform.ToString().ToUpperInvariant();
        if (multi)
        {
            fileTitle.Text = selection.Title;
            fileType.Text = "PS1  /  " + selection.Images.Length + " 碟";
        }
        else
        {
            fileType.Text = platform + "  /  " + current.Extension.Substring(1).ToUpperInvariant();
            if (current.Platform == GamePlatform.Ps2 && current.Medium != DiscMedium.Unknown)
                fileType.Text += "  /  " + current.Medium.ToString().ToUpperInvariant();
        }

        bool hasChoices = current.Platform != GamePlatform.Ps2 || current.Extension == ".chd";
        combine.Visible = multi;
        combineRow.Height = multi ? 34 : 0;
        firstChoice.Visible = secondChoice.Visible = hasChoices;
        actionRow.Height = hasChoices ? 78 : 0;
        bool canExtract = !multi && current.Platform == GamePlatform.Ps1 && current.Extension == ".chd";
        extractChoice.Visible = canExtract;
        extractRow.Height = canExtract ? 52 : 0;
        layout.PerformLayout();
        SetChoiceLabels();
        string description = current.Extension == ".bin" ? "无同名 CUE，将按单轨处理。多轨盘请选原始 CUE。" : current.FilePath;
        SetStatus(hasChoices ? "请选择用途" : "已识别 PS2", description);
    }

    private void SetChoiceLabels()
    {
        if (selection == null)
            return;
        GameImage current = selection.Images[0];
        if (current.Platform == GamePlatform.Ps1)
        {
            string emulator = combine.Checked ? "CHD + M3U" : current.Extension == ".pbp" ? "PBP" : "CHD";
            firstChoice.Text = "模拟器用\r\n" + emulator;
            secondChoice.Text = "PSP / PSV 实机用\r\n" + (combine.Checked ? "多碟 EBOOT.PBP" : "EBOOT.PBP");
        }
        else if (current.Platform == GamePlatform.Psp)
        {
            firstChoice.Text = "PSP / PSV 实机用\r\nISO";
            secondChoice.Text = "模拟器用\r\nCHD";
        }
        else if (current.Platform == GamePlatform.Ps2)
        {
            firstChoice.Text = "模拟器用\r\nCHD（已可用）";
            secondChoice.Text = "转为 ISO\r\nPS2 实机用";
        }
    }

    private async Task ConvertSelection(Destination target)
    {
        DiscSet current = selection;
        GameImage[] images = current.Images;
        bool makePbp = combine.Checked && target == Destination.Hardware;
        bool makePlaylist = combine.Checked && target == Destination.Emulator;
        if (!makePbp && !makePlaylist && images.All(disc => disc.IsReadyFor(target)))
        {
            string use = target == Destination.Emulator ? "模拟器" :
                images[0].Platform == GamePlatform.Ps2 ? "PS2 实机" : "PSP / PSV 实机";
            string format = images[0].Extension.Substring(1).ToUpperInvariant();
            SetStatus("这个格式已是 " + use + (format == "ISO" ? "可用的 ISO" : "可用压缩格式"),
                images[0].FilePath);
            return;
        }

        var reserved = new HashSet<string>(images.Select(disc => disc.FilePath), StringComparer.OrdinalIgnoreCase);
        string combined = null;
        var outputs = new string[images.Length];
        if (makePbp || makePlaylist)
        {
            combined = ReserveDestination(current.CombinedOutput(target, outputFolder), reserved);
            if (combined == null)
                return;
        }
        if (!makePbp)
        {
            for (int i = 0; i < images.Length; i++)
            {
                outputs[i] = images[i].IsReadyFor(target) ? images[i].FilePath :
                    ReserveDestination(ImageConversion.DefaultOutput(images[i], target, outputFolder), reserved);
                if (outputs[i] == null)
                    return;
            }
        }
        string resultPath = combined ?? outputs.First(path => !images.Any(disc => disc.FilePath == path));
        SetStatus("正在生成 " + (makePlaylist ? "换盘列表" : images[0].FormatFor(target).ToString().ToUpperInvariant()), resultPath);
        SetBusy(true);
        int written = 0;
        try
        {
            await Task.Run(() =>
            {
                if (makePbp)
                {
                    PbpConversion.Run(chdman, packager, images, combined);
                    written++;
                    return;
                }
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i].IsReadyFor(target))
                        continue;
                    ImageConversion.Run(chdman, packager, images[i], target, outputs[i]);
                    written++;
                }
                if (makePlaylist)
                {
                    DiscSet.WritePlaylist(combined, outputs);
                    written++;
                }
            });
            SetStatus(written == 1 ? "转换完成" : "转换完成，已写入 " + written + " 个文件", resultPath);
        }
        catch (Exception error)
        {
            SetStatus(written == 0 ? "转换失败" : "转换中断，已写入 " + written + " 个文件", error.Message, true);
            MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { SetBusy(false); }
    }

    private string ReserveDestination(string path, HashSet<string> reserved)
    {
        string extension = Path.GetExtension(path);
        while (File.Exists(path) || Directory.Exists(path) || reserved.Contains(path))
        {
            SetStatus("输出已存在", path);
            using (var dialog = new SaveFileDialog
            {
                Title = "输出已存在",
                InitialDirectory = Path.GetDirectoryName(path),
                FileName = Path.GetFileName(path),
                DefaultExt = extension.TrimStart('.'),
                Filter = "目标文件|*" + extension,
                OverwritePrompt = false,
                CheckPathExists = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return null;
                path = Path.ChangeExtension(dialog.FileName, extension);
            }
        }
        reserved.Add(path);
        return path;
    }

    private async Task ExtractChd()
    {
        GameImage current = selection.Images[0];
        string folder = outputFolder ?? Path.GetDirectoryName(current.FilePath);
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { current.FilePath };
        string bin = ReserveDestination(Path.Combine(folder, Path.GetFileNameWithoutExtension(current.FilePath) + ".bin"), reserved);
        if (bin == null) return;
        string cue = ReserveDestination(Path.ChangeExtension(bin, ".cue"), reserved);
        if (cue == null) return;
        SetBusy(true); SetStatus("正在导出 BIN + CUE", bin);
        try
        {
            await Task.Run(() => ChdExport.ToBinCue(chdman, current.FilePath, bin, cue));
            SetStatus("BIN + CUE 导出完成", bin + "  /  " + Path.GetFileName(cue));
        }
        catch (Exception error)
        {
            SetStatus("导出失败", error.Message, true);
            MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool working)
    {
        busy = working;
        browse.Enabled = outputLink.Enabled = firstChoice.Enabled = secondChoice.Enabled = extractChoice.Enabled = combine.Enabled = !working;
        progress.Visible = working;
    }

    private void SetStatus(string message, string description, bool error = false)
    {
        status.Text = message;
        status.ForeColor = error ? ErrorInk : Ink;
        detail.Text = description;
        tips.SetToolTip(detail, description);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            tips.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int dark = 1;
        if (DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)) != 0)
            DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
    }
}

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try { Application.Run(new ConverterForm()); }
        catch (Exception error) { MessageBox.Show(error.Message, "PlayStation万能转换姬", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
