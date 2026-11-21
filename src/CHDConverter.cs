using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal enum DiscMode
{
    Psp,
    Dvd,
    Cd
}

internal static class DiscInput
{
    internal static string Resolve(string path)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("找不到输入文件。", path);

        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".iso":
            case ".cue":
            case ".chd":
            case ".pbp":
                return path;
            case ".bin":
                string cue = Path.ChangeExtension(path, ".cue");
                return File.Exists(cue) ? cue : path;
            default:
                throw new ArgumentException("请选择 ISO、CUE、BIN、CHD 或 PBP 文件。");
        }
    }
}

internal static class DiscProcess
{
    internal static string Run(string executable, string arguments, string workingDirectory, bool useLocalTemp = false)
    {
        if (!File.Exists(executable))
            throw new FileNotFoundException("找不到后端工具 " + Path.GetFileName(executable) + "，请检查安装目录。", executable);

        var start = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        ToolBundle.Configure(start);
        if (useLocalTemp)
        {
            start.EnvironmentVariables["TEMP"] = workingDirectory;
            start.EnvironmentVariables["TMP"] = workingDirectory;
        }

        using (Process process = Process.Start(start))
        {
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            string error = stderr.GetAwaiter().GetResult().Trim();
            string log = stdout.GetAwaiter().GetResult().Trim();
            if (process.ExitCode != 0)
            {
                string detail = error.Length == 0 ? log : error;
                if (detail.Length > 1600)
                    detail = detail.Substring(detail.Length - 1600);
                throw new InvalidOperationException(Path.GetFileName(executable) + " 退出代码 "
                    + process.ExitCode + Environment.NewLine + detail);
            }
            return log;
        }
    }
}

internal static class ChdConversion
{
    internal static void Run(string chdman, string input, string output, DiscMode mode, string trackMode = null)
    {
        input = DiscInput.Resolve(input);
        string extension = Path.GetExtension(input).ToLowerInvariant();
        bool isCue = extension == ".cue";
        if (isCue)
            mode = DiscMode.Cd;
        if (File.Exists(output))
            throw new IOException("输出文件已存在，请另选文件名。");

        string inputDirectory = Path.GetDirectoryName(input);
        string directory = Path.GetDirectoryName(output);
        string temporary = Path.Combine(directory, ".chd-" + Guid.NewGuid().ToString("N") + ".tmp");
        string temporaryCue = null;
        string command = "createdvd";
        if (mode == DiscMode.Psp)
            command += " -hs 2048";
        if (mode == DiscMode.Dvd)
            command += " -c zlib";
        if (mode == DiscMode.Cd)
            command = "createcd";

        try
        {
            string inputArgument = input;
            if (mode == DiscMode.Cd)
            {
                // chdman 0.289 uses a narrow file open for CD descriptors on Windows.
                // Keep the working directory beside the descriptor to preserve relative track paths.
                inputArgument = Path.GetFileName(input);
                if (!isCue && trackMode != null)
                {
                    inputDirectory = directory;
                    inputArgument = "chd-cue-" + Guid.NewGuid().ToString("N") + ".cue";
                    temporaryCue = Path.Combine(inputDirectory, inputArgument);
                    WriteTrackCue(temporaryCue, input, trackMode);
                }
                else if (inputArgument.Any(character => character > 127))
                {
                    string cueName = "chd-cue-" + Guid.NewGuid().ToString("N") + ".cue";
                    temporaryCue = Path.Combine(inputDirectory, cueName);
                    if (isCue)
                        File.Copy(input, temporaryCue);
                    else
                        WriteIsoCue(temporaryCue, inputArgument, new FileInfo(input).Length);
                    inputArgument = cueName;
                }
            }
            string arguments = string.Format("{0} -i \"{1}\" -o \"{2}\"", command, inputArgument, temporary);
            DiscProcess.Run(chdman, arguments, inputDirectory);
            File.Move(temporary, output);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
            if (temporaryCue != null && File.Exists(temporaryCue))
                File.Delete(temporaryCue);
        }
    }

    private static void WriteIsoCue(string cue, string isoName, long bytes)
    {
        // Match chdman's parse_iso sector-size precedence.
        string trackMode;
        if (bytes % 2048 == 0)
            trackMode = "MODE1/2048";
        else if (bytes % 2336 == 0)
            trackMode = "MODE2/2336";
        else if (bytes % 2352 == 0)
            trackMode = "MODE2/2352";
        else
            throw new InvalidOperationException("这个 CD ISO 的文件大小与受支持的扇区大小不匹配。");

        WriteTrackCue(cue, isoName, trackMode);
    }

    internal static void WriteTrackCue(string path, string track, string trackMode)
    {
        File.WriteAllText(path, string.Format(
            "FILE \"{0}\" BINARY\r\n  TRACK 01 {1}\r\n    INDEX 01 00:00:00\r\n", track, trackMode));
    }
}

internal static class PbpConversion
{
    internal static void Run(string chdman, string packager, IReadOnlyList<GameImage> images, string output)
    {
        if (File.Exists(output))
            throw new IOException("输出文件已存在，请另选位置。");

        string outputFolder = Path.GetDirectoryName(output);
        string workParent = Directory.Exists(outputFolder) ? outputFolder : Path.GetDirectoryName(outputFolder);
        string work = Path.Combine(workParent, ".pbp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            string[] inputs = images.Select((image, i) => PrepareInput(chdman, image, work, i)).ToArray();
            string input = inputs[0];
            if (inputs.Length > 1)
            {
                input = Path.Combine(work, "discs.m3u");
                File.WriteAllLines(input, inputs, new UTF8Encoding(false));
            }

            string stagedPbp = Path.Combine(work, "EBOOT.pbp");
            // The Windows CLI expects an output directory. Low verbosity avoids console cursor calls.
            string log = DiscProcess.Run(packager, string.Format("-v 1 -f EBOOT -i \"{0}\" -o \"{1}\"", input, work), work, true);
            if (!File.Exists(stagedPbp))
                throw new InvalidOperationException("打包工具未生成 PBP，请检查 PS1 镜像。" + Environment.NewLine + log);
            using (var reader = new BinaryReader(File.OpenRead(stagedPbp)))
            {
                if (reader.BaseStream.Length <= 40 || reader.ReadUInt32() != 0x50425000)
                    throw new InvalidDataException("打包工具生成的文件没有有效的 PBP 文件头。");
            }
            Directory.CreateDirectory(outputFolder);
            File.Move(stagedPbp, output);
        }
        finally
        {
            Directory.Delete(work, true);
        }
    }

    private static string PrepareInput(string chdman, GameImage image, string work, int index)
    {
        if (image.TrackMode != null && !image.TrackMode.EndsWith("/2352", StringComparison.Ordinal))
            throw new InvalidDataException("PBP 打包需要 2352 字节原始扇区。请使用原始 BIN/CUE 或由它生成的 CHD。");
        string name = "disc" + (index + 1);
        string cue = Path.Combine(work, name + ".cue");
        if (image.Extension == ".chd")
        {
            DiscProcess.Run(chdman, string.Format("extractcd -i \"{0}\" -o {1}.cue -ob {1}.bin",
                image.FilePath, name), work);
            return cue;
        }
        if (image.Extension == ".iso" || image.Extension == ".bin")
        {
            ChdConversion.WriteTrackCue(cue, image.FilePath, image.TrackMode);
            return cue;
        }
        return image.FilePath;
    }
}

internal static class ImageConversion
{
    internal static string DefaultOutput(GameImage image, Destination target, string outputFolder)
    {
        string folder = outputFolder ?? Path.GetDirectoryName(image.FilePath);
        OutputFormat format = image.FormatFor(target);
        if (format == OutputFormat.Pbp)
            return Path.Combine(folder, Path.GetFileNameWithoutExtension(image.FilePath) + "_PSP", "EBOOT.PBP");
        return Path.Combine(folder, Path.ChangeExtension(Path.GetFileName(image.FilePath),
            "." + format.ToString().ToLowerInvariant()));
    }

    internal static void Run(string chdman, string packager, GameImage image, Destination target, string output)
    {
        if (image.IsReadyFor(target))
            throw new InvalidOperationException("当前文件已经可用于所选用途。");
        if (image.Platform == GamePlatform.Ps2 && image.Medium == DiscMedium.Unknown)
            throw new InvalidDataException("无法确认 PS2 镜像来自 CD 还是 DVD。");

        switch (image.FormatFor(target))
        {
            case OutputFormat.Pbp:
                PbpConversion.Run(chdman, packager, new[] { image }, output);
                break;
            case OutputFormat.Iso:
                if (image.Platform == GamePlatform.Ps2)
                    ChdExport.ToPs2Iso(chdman, image, output);
                else
                    ExtractPspIso(chdman, image, output);
                break;
            case OutputFormat.Chd:
                DiscMode mode = image.Platform == GamePlatform.Psp ? DiscMode.Psp :
                    image.Cd ? DiscMode.Cd : DiscMode.Dvd;
                ChdConversion.Run(chdman, image.FilePath, output, mode, image.TrackMode);
                break;
        }
    }

    private static void ExtractPspIso(string chdman, GameImage image, string output)
    {
        if (File.Exists(output))
            throw new IOException("输出文件已存在，请另选位置。");
        string folder = Path.GetDirectoryName(output);
        string work = Path.Combine(folder, ".iso-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            string arguments = image.Cd ? "extractcd -i \"{0}\" -o disc.cue -ob disc.iso" :
                "extractdvd -i \"{0}\" -o disc.iso";
            DiscProcess.Run(chdman, string.Format(arguments, image.FilePath), work);
            string iso = Path.Combine(work, "disc.iso");
            GameImage result = DiscProbe.Read(iso, chdman);
            if (result.Platform != GamePlatform.Psp || result.TrackMode != "MODE1/2048")
                throw new InvalidDataException("CHD 未还原为 PSP 可用的 2048 字节扇区 ISO。");
            File.Move(iso, output);
        }
        finally { Directory.Delete(work, true); }
    }
}
