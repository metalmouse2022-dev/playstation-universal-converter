using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Ps2IsoTest
{
    private static Assembly app;
    private static string run, chdman;
    private static int passed;
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
    private static object Call(string type, string method, params object[] args)
    {
        try { return app.GetType(type).GetMethod(method, Hidden).Invoke(null, args); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }
    private static object Target(string value) { return Enum.Parse(app.GetType("Destination"), value); }
    private static void Assert(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        passed++; Console.WriteLine("PASS: " + label);
    }
    private static string Hash(string path)
    {
        using (var file = File.OpenRead(path)) using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(file));
    }
    private static void Backend(string command) { Call("DiscProcess", "Run", chdman, command, run, false); }
    private static object Probe(string path) { return Call("DiscProbe", "Read", path, chdman); }
    private static string Convert(string name)
    {
        string input = Path.Combine(run, name + ".chd"), output = Path.Combine(run, name + ".iso");
        object image = Probe(input);
        Assert(Call("ImageConversion", "DefaultOutput", image, Target("Hardware"), null).Equals(output), name + " defaults beside CHD with .iso extension");
        Call("ImageConversion", "Run", chdman, "unused", image, Target("Hardware"), output);
        return output;
    }
    private static void Reject(string name, string expected)
    {
        string output = Path.Combine(run, name + ".iso"); bool rejected = false;
        try { Call("ImageConversion", "Run", chdman, "unused", Probe(Path.Combine(run, name + ".chd")), Target("Hardware"), output); }
        catch (InvalidDataException error) { rejected = error.Message.Contains(expected); }
        Assert(rejected && !File.Exists(output), name + " rejected with cause and no final ISO");
    }
    private static void Cue(string name, string file, string mode, string extra)
    {
        File.WriteAllText(Path.Combine(run, name + ".cue"), "FILE \"" + file + "\" BINARY\n  TRACK 01 " + mode + "\n    INDEX 01 00:00:00\n" + extra);
        Backend("createcd -i " + name + ".cue -o " + name + ".chd -c none");
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            app = Assembly.LoadFrom(args[0]); run = Path.GetFullPath(args[2]); Directory.CreateDirectory(run);
            chdman = (string)app.GetType("ToolBundle").GetProperty("Chdman", Hidden).GetValue(null, null);
            string source = Path.Combine(run, "source.iso"); File.Copy(args[1], source);
            string expected = Hash(source);
            Backend("createdvd -i source.iso -o dvd.chd -c zlib");
            string dvd = Convert("dvd"); Assert(Hash(dvd) == expected, "DVD CHD restores original ISO byte for byte");
            Cue("cd2048", "source.iso", "MODE1/2048", "");
            Assert(Hash(Convert("cd2048")) == expected, "CD MODE1/2048 restores original data");
            byte[] payload = File.ReadAllBytes(source);
            byte[] raw = new byte[payload.Length / 2048 * 2352];
            for (int sector = 0; sector < payload.Length / 2048; sector++)
            {
                int offset = sector * 2352;
                for (int i = 1; i <= 10; i++) raw[offset + i] = 255;
                raw[offset + 15] = 2;
                Buffer.BlockCopy(payload, sector * 2048, raw, offset + 24, 2048);
            }
            File.WriteAllBytes(Path.Combine(run, "raw.bin"), raw);
            Cue("cd2352", "raw.bin", "MODE2/2352", "");
            Assert(Hash(Convert("cd2352")) == expected, "CD MODE2/2352 strips headers, preserves every data sector");
            string before = Hash(dvd); bool conflict = false;
            try { Convert("dvd"); } catch (IOException) { conflict = true; }
            Assert(conflict && Hash(dvd) == before, "existing ISO refused and unchanged");
            File.WriteAllBytes(Path.Combine(run, "audio.bin"), new byte[75 * 2352]);
            Cue("mixed", "raw.bin", "MODE2/2352", "FILE \"audio.bin\" BINARY\n  TRACK 02 AUDIO\n    INDEX 01 00:00:00\n");
            Reject("mixed", "多轨或音轨");
            raw[raw.Length - 2352 + 18] = raw[raw.Length - 2352 + 22] = 0x20;
            File.WriteAllBytes(Path.Combine(run, "form2.bin"), raw);
            Cue("form2", "form2.bin", "MODE2/2352", ""); Reject("form2", "Form 2");
            object unknown = Activator.CreateInstance(app.GetType("GameImage"), Hidden, null,
                new object[] { Path.Combine(run, "dvd.chd"), Enum.Parse(app.GetType("GamePlatform"), "Ps2"), Enum.Parse(app.GetType("DiscMedium"), "Unknown"), null }, null);
            bool blocked = false;
            try { Call("ImageConversion", "Run", chdman, "unused", unknown, Target("Hardware"), Path.Combine(run, "unknown.iso")); }
            catch (InvalidDataException) { blocked = true; }
            Assert(blocked && !File.Exists(Path.Combine(run, "unknown.iso")), "unknown PS2 medium stays blocked");
            Assert(Directory.GetDirectories(run, ".iso-*").Length == 0, "temporary ISO work removed after success and failure");

            // Own controls only: no shown window, screenshots, desktop input, or emulator.
            WindowsFormsSynchronizationContext.AutoInstall = false;
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using (var form = (Form)Activator.CreateInstance(app.GetType("ConverterForm"), true))
            {
                var type = form.GetType();
                ((Task)type.GetMethod("LoadImages", Hidden).Invoke(form, new object[] { new[] { Path.Combine(run, "dvd.chd") } })).GetAwaiter().GetResult();
                var first = (Button)type.GetField("firstChoice", Hidden).GetValue(form);
                var second = (Button)type.GetField("secondChoice", Hidden).GetValue(form);
                var status = (Label)type.GetField("status", Hidden).GetValue(form);
                var row = (RowStyle)type.GetField("actionRow", Hidden).GetValue(form);
                Assert(first.Text.Contains("CHD（已可用）") && second.Text.Contains("PS2 实机用") && second.Text.Contains("ISO") && row.Height == 78 && status.Text == "请选择用途",
                    "loading PS2 CHD exposes ISO choice without automatic conversion");
                ((Task)type.GetMethod("ConvertSelection", Hidden).Invoke(form, new[] { Target("Emulator") })).GetAwaiter().GetResult();
                Assert(status.Text.Contains("模拟器可用压缩格式"), "emulator choice reports CHD already usable");
            }
            Console.WriteLine("Passed " + passed + " focused checks; no emulator or physical hardware test.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
