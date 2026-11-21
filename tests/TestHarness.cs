using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;

internal static class TestHarness
{
    private static int passed;
    private static string fixtures, run;
    private static void Assert(bool condition, string label) { if (!condition) throw new Exception("FAIL " + label); passed++; Console.WriteLine("PASS " + label); }
    private static string Hash(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)); }
    private static T Field<T>(object owner, string name) { return (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner); }
    private static void Invoke(object owner, string name, params object[] args) { owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args); }
    private static byte[][] PbpParts(string path)
    {
        byte[] data = File.ReadAllBytes(path); long[] offsets = new long[9]; offsets[8] = data.Length;
        for (int i = 0; i < 8; i++) offsets[i] = BitConverter.ToUInt32(data, 8 + 4 * i);
        return Enumerable.Range(0, 8).Select(i => data.Skip((int)offsets[i]).Take((int)(offsets[i + 1] - offsets[i])).ToArray()).ToArray();
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string root = args[0]; fixtures = Path.Combine(root, "tests", "fixtures"); run = Path.Combine(root, "tests", "run-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(run); File.WriteAllText(Path.Combine(root, "tests", "last-run.txt"), run);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string tools = ToolBundle.Root; Console.WriteLine("TOOLS " + tools);
            Assert(File.Exists(ToolBundle.Chdman) && File.Exists(ToolBundle.Packager), "embedded tools extracted");
            Assert(File.Exists(Path.Combine(tools, "dotnet", "shared", "Microsoft.NETCore.App", "8.0.30", "coreclr.dll")), "private runtime included");
            string version = DiscProcess.Run(Path.Combine(tools, "dotnet", "dotnet.exe"), "--list-runtimes", run);
            Assert(version.Contains(tools) && version.Contains("8.0.30"), "private dotnet runtime resolves locally");
            Environment.SetEnvironmentVariable("COREHOST_TRACE", "1"); Environment.SetEnvironmentVariable("COREHOST_TRACEFILE", Path.Combine(run, "runtime-trace.txt"));
            DiscProcess.Run(ToolBundle.Packager, "--help", run);
            Environment.SetEnvironmentVariable("COREHOST_TRACE", null); Environment.SetEnvironmentVariable("COREHOST_TRACEFILE", null);
            string trace = File.ReadAllText(Path.Combine(run, "runtime-trace.txt"));
            Assert(trace.Contains(Path.Combine(tools, "dotnet", "shared")), "PSXPackager uses embedded .NET 8");

            byte[] newIcon, newBackground;
            using (var a = new Artwork(true))
            using (var b = new Artwork(false))
            {
                a.SetImage(ToolBundle.Bitmap("character.png"), true); a.Text = "封面测试"; a.TextY = 80;
                b.SetImage(ToolBundle.Bitmap("character.png"), true); b.Text = "PSP GAME"; b.TextY = 75;
                newIcon = a.Png(); newBackground = b.Png();
                using (var image = Artwork.Load(newIcon)) Assert(image.Size == new Size(144, 80), "ICON0 dimensions");
                using (var image = Artwork.Load(newBackground)) Assert(image.Size == new Size(480, 272), "PIC1 dimensions");
                File.WriteAllBytes(Path.Combine(run, "ICON0.PNG"), newIcon); File.WriteAllBytes(Path.Combine(run, "PIC1.PNG"), newBackground);
            }
            foreach (string name in new[] { "psp.iso", "psp-missing.iso", "source.pbp" })
            {
                string original = Path.Combine(fixtures, name), output = Path.Combine(run, name); string before = Hash(original);
                GameArtwork art = GameArtwork.Open(original); art.SaveAs(output, newIcon, newBackground, null);
                GameArtwork read = GameArtwork.Open(output);
                Assert(read.Icon.SequenceEqual(newIcon) && read.Background.SequenceEqual(newBackground), name + " artwork roundtrip");
                Assert(Hash(original) == before, name + " source unchanged");
                string outputHash = Hash(output); bool conflict = false;
                try { art.SaveAs(output, newIcon, newBackground, null); } catch (IOException) { conflict = true; }
                Assert(conflict && Hash(output) == outputHash, name + " existing destination protected");
            }
            byte[][] oldParts = PbpParts(Path.Combine(fixtures, "source.pbp")), newParts = PbpParts(Path.Combine(run, "source.pbp"));
            Assert(Enumerable.Range(0,8).Where(i => i != 1 && i != 4).All(i => oldParts[i].SequenceEqual(newParts[i])), "PBP all non-image sections preserved byte for byte");
            var only = GameArtwork.Open(Path.Combine(fixtures, "source.pbp"));
            only.SaveAs(Path.Combine(run, "icon-only.pbp"), newIcon, null, null);
            Assert(PbpParts(Path.Combine(run, "icon-only.pbp"))[4].SequenceEqual(oldParts[4]), "unedited background preserved");
            bool malformed = false; try { GameArtwork.Open(Path.Combine(fixtures, "bad.pbp")); } catch (InvalidDataException) { malformed = true; }
            Assert(malformed, "malformed PBP rejected");

            string psp = Path.Combine(fixtures, "psp.iso"), pspChd = Path.Combine(run, "psp.chd");
            Assert(DiscProbe.Read(psp, ToolBundle.Chdman).Platform == GamePlatform.Psp, "PSP platform detection");
            ChdConversion.Run(ToolBundle.Chdman, psp, pspChd, DiscMode.Psp);
            GameImage compressedPsp = DiscProbe.Read(pspChd, ToolBundle.Chdman);
            string pspRestored = Path.Combine(run, "psp-restored.iso"); ImageConversion.Run(ToolBundle.Chdman, ToolBundle.Packager, compressedPsp, Destination.Hardware, pspRestored);
            Assert(Hash(psp) == Hash(pspRestored), "PSP ISO CHD ISO roundtrip");
            var ps2 = DiscProbe.Read(Path.Combine(fixtures,"ps2-small.iso"),ToolBundle.Chdman);
            Assert(ps2.Platform == GamePlatform.Ps2 && ps2.Medium == DiscMedium.Unknown, "ambiguous PS2 medium stays blocked");

            string cue = Path.Combine(fixtures, "multitrack.cue"), chd = Path.Combine(run, "ps1.chd");
            ChdConversion.Run(ToolBundle.Chdman, cue, chd, DiscMode.Cd);
            var imagePs1 = DiscProbe.Read(chd, ToolBundle.Chdman);
            Assert(imagePs1.Platform == GamePlatform.Ps1, "PS1 CHD detection");
            string bin = Path.Combine(run, "导出测试.bin"), outCue = Path.ChangeExtension(bin, ".cue");
            ChdExport.ToBinCue(ToolBundle.Chdman, chd, bin, outCue);
            Assert(File.Exists(bin) && File.ReadAllText(outCue).Contains("AUDIO") && File.ReadAllText(outCue).Contains("导出测试.bin"), "CHD export preserves audio track and CUE filename");
            string roundtrip = Path.Combine(run, "ps1-roundtrip.chd"); ChdConversion.Run(ToolBundle.Chdman, outCue, roundtrip, DiscMode.Cd);
            string firstInfo = DiscProcess.Run(ToolBundle.Chdman, "info -i \"" + chd + "\"", run), lastInfo = DiscProcess.Run(ToolBundle.Chdman, "info -i \"" + roundtrip + "\"", run);
            string shaFirst = firstInfo.Split('\n').First(x => x.StartsWith("Data SHA1:")), shaLast = lastInfo.Split('\n').First(x => x.StartsWith("Data SHA1:"));
            Assert(shaFirst == shaLast, "multi-track CHD BIN/CUE CHD data hash matches");
            bool binConflict = false; string binHash = Hash(bin);
            try { ChdExport.ToBinCue(ToolBundle.Chdman, chd, bin, outCue); } catch (IOException) { binConflict = true; }
            Assert(binConflict && Hash(bin) == binHash, "existing BIN/CUE protected");

            GameImage disc1 = DiscProbe.Read(Path.Combine(fixtures, "Test Game (CD1).bin"), ToolBundle.Chdman), disc2 = DiscProbe.Read(Path.Combine(fixtures,"Test Game (CD2).bin"),ToolBundle.Chdman);
            string pbp = Path.Combine(run, "EBOOT.PBP"); PbpConversion.Run(ToolBundle.Chdman, ToolBundle.Packager, new[] { disc1, disc2 }, pbp);
            Assert(DiscProbe.Read(pbp, ToolBundle.Chdman).Platform == GamePlatform.Ps1, "multi-disc PS1 PBP packaging with private runtime");
            string patchedPbp = Path.Combine(run, "artwork-EBOOT.PBP"); GameArtwork.Open(pbp).SaveAs(patchedPbp, newIcon, newBackground, null);
            Assert(PbpParts(pbp)[7].SequenceEqual(PbpParts(patchedPbp)[7]), "multi-disc DATA.PSAR remains byte-identical after artwork edit");

            using (var form = new ConverterForm())
            {
                form.CreateControl();
                using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(run, "converter.png")); }
                Field<TableLayoutPanel>(form,"layout").Visible = false;
                var designer = Field<DesignerPanel>(form,"designer"); designer.Visible = true; designer.BringToFront(); designer.CreateControl();
                Artwork[] artwork = Field<Artwork[]>(designer,"art");
                artwork[0].SetImage(ToolBundle.Bitmap("character.png"),true); artwork[0].Text = "PSP GAME";
                artwork[1].SetImage(ToolBundle.Bitmap("character.png"),true); artwork[1].Text = "MY COLLECTION";
                Invoke(designer,"SelectCanvas",1);
                using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(run, "designer.png")); }
                Assert(form.Text.Contains("bilibili: metalmouse制作"), "title signature");
                Assert(form.Icon != null, "PlayStation icon assigned");
            }
            Console.WriteLine("RESULT " + passed + " passed\nRUN " + run); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
