using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class PaintRegression
{
    private static void Paint(Control control, Graphics graphics, bool background)
    {
        if (background) typeof(Control).GetMethod("OnPaintBackground",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(control,new object[]{new PaintEventArgs(graphics,control.ClientRectangle)});
        typeof(Control).GetMethod("OnPaint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(control,new object[]{new PaintEventArgs(graphics,control.ClientRectangle)});
    }
    private static int OpaqueCheck(Control control, string output, string name)
    {
        using (var first=new Bitmap(control.Width,control.Height))
        using (var second=new Bitmap(control.Width,control.Height))
        {
            using(Graphics g=Graphics.FromImage(first)) { g.Clear(Color.Magenta);Paint(control,g,false); }
            using(Graphics g=Graphics.FromImage(second))
            {
                g.Clear(Color.Lime);
                g.DrawString("旧文字 字体/字号 PSP图标/背景设计",control.Font,Brushes.Red,Point.Empty);
                Paint(control,g,false);
            }
            int difference=0;
            for(int y=0;y<first.Height;y++)for(int x=0;x<first.Width;x++)
                if(first.GetPixel(x,y).ToArgb()!=second.GetPixel(x,y).ToArgb())difference++;
            first.Save(Path.Combine(output,name+"-fresh.png"));second.Save(Path.Combine(output,name+"-repaint.png"));
            Console.WriteLine(name+" pixels dependent on previous background: "+difference);
            return difference==0?0:1;
        }
    }
    private static int OpaqueControls(Control control)
    {
        int failures=0;
        if(control is Button)
        {
            bool opaque=(bool)typeof(Control).GetMethod("GetStyle",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(control,new object[]{ControlStyles.Opaque});
            if(!opaque||control.BackColor.A!=255) { Console.WriteLine("Non-opaque button: "+control.Text);failures++; }
        }
        foreach(Control child in control.Controls)failures+=OpaqueControls(child);
        return failures;
    }
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        Directory.CreateDirectory(args[0]); int failures=0;
        using (var form=new GlassForm { ClientSize=new Size(720,300) })
        using (var button=new GlassButton { Text="PSP图标/背景设计",Size=new Size(260,50),Location=new Point(180,80),Font=new Font("Microsoft YaHei UI",12F),Selected=true })
        {
            form.Controls.Add(button);
            using (var image=new Bitmap(720,300))
            using (Graphics g=Graphics.FromImage(image))
            {
                g.Clear(Color.Magenta); g.TranslateTransform(180,80);g.SetClip(button.ClientRectangle);
                Paint(button,g,false);image.Save(Path.Combine(args[0],"translated.png"));
                int outside=0;
                for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)
                    if(!new Rectangle(180,80,260,50).Contains(x,y)&&image.GetPixel(x,y).ToArgb()!=Color.Magenta.ToArgb())outside++;
                Console.WriteLine("Translated text pixels outside own control: "+outside);
                if(outside!=0)failures++;
            }
            using(var image=new Bitmap(720,300))
            using(Graphics g=Graphics.FromImage(image))
            {
                g.Clear(Color.Magenta);g.TranslateTransform(180,80);g.SetClip(new Rectangle(0,0,100,50));
                Paint(button,g,false);image.Save(Path.Combine(args[0],"partial.png"));
                int outside=0;
                for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)
                    if(!new Rectangle(180,80,100,50).Contains(x,y)&&image.GetPixel(x,y).ToArgb()!=Color.Magenta.ToArgb())outside++;
                Console.WriteLine("Partial repaint pixels outside dirty region: "+outside);
                if(outside!=0)failures++;
            }
        }
        using (var form=new GlassForm { ClientSize=new Size(720,500) })
        using (var panel=new GlassPanel { Location=new Point(160,80),Size=new Size(400,300) })
        using (var image=new Bitmap(720,500))
        using (Graphics g=Graphics.FromImage(image))
        {
            form.Controls.Add(panel);g.Clear(Color.Magenta);g.TranslateTransform(160,80);g.SetClip(new Rectangle(0,0,100,50));
            Paint(panel,g,true);image.Save(Path.Combine(args[0],"panel-partial.png"));
            int outside=0;
            for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)
                if(!new Rectangle(160,80,100,50).Contains(x,y)&&image.GetPixel(x,y).ToArgb()!=Color.Magenta.ToArgb())outside++;
            Console.WriteLine("Glass panel pixels outside dirty region: "+outside);if(outside!=0)failures++;
        }
        using (var a=new Artwork(true))
        using (var b=new Artwork(false))
        using (var preview=new ArtPreview(new[]{a,b}) { Size=new Size(400,300), Font=new Font("Microsoft YaHei UI",10F) })
        using (var image=new Bitmap(720,500))
        using (Graphics g=Graphics.FromImage(image))
        {
            g.Clear(Color.Magenta);g.TranslateTransform(160,80);g.SetClip(preview.ClientRectangle);
            Paint(preview,g,false);image.Save(Path.Combine(args[0],"preview-translated.png"));
            int outside=0;
            for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)
                if(!new Rectangle(160,80,400,300).Contains(x,y)&&image.GetPixel(x,y).ToArgb()!=Color.Magenta.ToArgb())outside++;
            Console.WriteLine("Preview text pixels outside own control: "+outside);if(outside!=0)failures++;
        }
        using(var button=new GlassButton { Text="PSP图标/背景设计", Size=new Size(300,50), Font=new Font("Microsoft YaHei UI",12F) })
        {
            failures+=OpaqueCheck(button,args[0],"button-normal");button.Selected=true;
            failures+=OpaqueCheck(button,args[0],"button-selected");
            typeof(GlassButton).GetMethod("OnMouseEnter",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(button,new object[]{EventArgs.Empty});
            failures+=OpaqueCheck(button,args[0],"button-hover");
            typeof(GlassButton).GetMethod("OnMouseDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(button,new object[]{new MouseEventArgs(MouseButtons.Left,1,20,20,0)});
            failures+=OpaqueCheck(button,args[0],"button-pressed");
            button.Enabled=false;button.Text="选择文件…";
            failures+=OpaqueCheck(button,args[0],"button-disabled");
        }
        using(var choice=new ChoiceBox { Size=new Size(300,50),Font=new Font("Microsoft YaHei UI",12F) })
        {
            choice.Items.AddRange(new object[]{"Microsoft YaHei UI","宋体"});choice.SelectedIndex=0;
            failures+=OpaqueCheck(choice,args[0],"choice-long");choice.SelectedIndex=1;
            failures+=OpaqueCheck(choice,args[0],"choice-short");
        }
        using(var form=new ConverterForm())
        {
            int failed=OpaqueControls(form);Console.WriteLine("Non-opaque buttons in actual form: "+failed);failures+=failed;
        }
        Console.WriteLine(failures==0?"PASS paint translation and clipping":"FAIL "+failures+" paint checks");return failures==0?0:1;
    }
}
