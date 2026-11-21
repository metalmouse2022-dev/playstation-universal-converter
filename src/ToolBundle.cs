using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

internal static class ToolBundle
{
    private static string root;
    internal static string Root { get { return root ?? (root = Extract()); } }
    internal static string Chdman { get { return Path.Combine(Root, "chdman.exe"); } }
    internal static string Packager { get { return Path.Combine(Root, "psxpackager", "psxpackager.exe"); } }
    internal static Stream Resource(string name)
    {
        Stream result = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (result == null) throw new InvalidDataException("缺少内置资源：" + name);
        return result;
    }
    internal static Bitmap Bitmap(string name)
    {
        using (Stream stream = Resource(name))
        using (var image = Image.FromStream(stream)) return new Bitmap(image);
    }
    private static string Hash(Stream stream)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    private static string Extract()
    {
        string manifest;
        using (var reader = new StreamReader(Resource("tools.manifest"))) manifest = reader.ReadToEnd();
        string id;
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(manifest))) id = Hash(stream).Substring(0, 20);
        string parent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CHDConverter", "tools");
        string destination = Path.Combine(parent, id);
        using (var mutex = new Mutex(false, "Local\\CHDConverter-" + id))
        {
            bool acquired = false;
            try
            {
                try { acquired = mutex.WaitOne(TimeSpan.FromMinutes(2)); }
                catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) throw new IOException("另一窗口正在准备工具，请稍后重试。");
                bool valid = Directory.Exists(destination);
                foreach (string line in manifest.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string name = line.Substring(65).TrimEnd('\r');
                    string path = Path.Combine(destination, name.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(path)) { valid = false; break; }
                    using (Stream stream = File.OpenRead(path))
                        if (Hash(stream) != line.Substring(0, 64)) { valid = false; break; }
                }
                if (valid) return destination;
                Directory.CreateDirectory(parent);
                // An incomplete or modified cache is kept aside, never trusted as an executable.
                if (Directory.Exists(destination)) Directory.Move(destination, destination + "-old-" + Guid.NewGuid().ToString("N"));
                string staging = destination + "-" + Guid.NewGuid().ToString("N");
                Directory.CreateDirectory(staging);
                try
                {
                    using (Stream stream = Resource("tools.zip"))
                    using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        foreach (ZipArchiveEntry entry in zip.Entries)
                        {
                            string path = Path.GetFullPath(Path.Combine(staging, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                            if (!path.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidDataException("工具包路径无效。");
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                            using (Stream input = entry.Open())
                            using (Stream output = new FileStream(path, FileMode.CreateNew)) input.CopyTo(output);
                        }
                    }
                    Directory.Move(staging, destination);
                }
                finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
                return destination;
            }
            finally { if (acquired) mutex.ReleaseMutex(); }
        }
    }
    internal static void Configure(ProcessStartInfo start)
    {
        if (!string.Equals(Path.GetFileName(start.FileName), "psxpackager.exe", StringComparison.OrdinalIgnoreCase)) return;
        string runtime = Path.Combine(Root, "dotnet");
        start.EnvironmentVariables["DOTNET_ROOT"] = runtime;
        start.EnvironmentVariables["DOTNET_ROOT_X64"] = runtime;
        start.EnvironmentVariables["DOTNET_MULTILEVEL_LOOKUP"] = "0";
        start.EnvironmentVariables["DOTNET_ROLL_FORWARD"] = "Major";
    }
}
