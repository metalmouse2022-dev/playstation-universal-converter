using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

internal static class ChdExport
{
    internal static void ToPs2Iso(string chdman, GameImage image, string output)
    {
        if (image.Platform != GamePlatform.Ps2 || image.Extension != ".chd")
            throw new InvalidDataException("请选择 PS2 CHD 镜像。");
        if (image.Medium == DiscMedium.Unknown)
            throw new InvalidDataException("CHD 缺少可确认的 CD/DVD 信息，无法安全导出 ISO。");
        if (File.Exists(output) || Directory.Exists(output))
            throw new IOException("输出已存在，请另存。");
        string work = Path.Combine(Path.GetDirectoryName(output), ".iso-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            string iso = Path.Combine(work, "disc.iso");
            if (image.Cd)
            {
                DiscProcess.Run(chdman, "extractcd -i \"" + image.FilePath + "\" -o disc.cue -ob disc.bin", work);
                CdTrackToIso(Path.Combine(work, "disc.cue"), Path.Combine(work, "disc.bin"), iso);
            }
            else
                DiscProcess.Run(chdman, "extractdvd -i \"" + image.FilePath + "\" -o disc.iso", work);
            GameImage result = DiscProbe.Read(iso, chdman);
            if (result.Platform != GamePlatform.Ps2 || result.TrackMode != "MODE1/2048" || new FileInfo(iso).Length % 2048 != 0)
                throw new InvalidDataException("CHD 未还原为有效的 PS2 2048 字节扇区 ISO。");
            // File.Move also refuses outputs created by another window during extraction.
            File.Move(iso, output);
        }
        finally { Directory.Delete(work, true); }
    }

    private static void CdTrackToIso(string cue, string bin, string iso)
    {
        string descriptor = File.ReadAllText(cue);
        MatchCollection tracks = Regex.Matches(descriptor, @"(?m)^\s*TRACK\s+\d+\s+(\S+)");
        Match index = Regex.Match(descriptor, @"(?m)^\s*INDEX\s+01\s+(\S+)");
        if (tracks.Count != 1 || tracks[0].Groups[1].Value == "AUDIO")
            throw new InvalidDataException("该 PS2 CD 含多轨或音轨，ISO 无法完整保留，未输出文件。");
        if (!index.Success || index.Groups[1].Value != "00:00:00")
            throw new InvalidDataException("该 PS2 CD 的数据轨起始位置不是零，暂不支持无损导出 ISO。");
        string mode = tracks[0].Groups[1].Value;
        int stride, offset;
        switch (mode)
        {
            case "MODE1/2048": case "MODE2/2048": stride = 2048; offset = 0; break;
            case "MODE1/2352": stride = 2352; offset = 16; break;
            case "MODE2/2352": stride = 2352; offset = 24; break;
            case "MODE2/2336": stride = 2336; offset = 8; break;
            default: throw new InvalidDataException("无法将 CD 轨道 " + mode + " 完整转换为 2048 字节扇区 ISO。");
        }
        using (var input = new BinaryReader(File.OpenRead(bin)))
        using (var output = new FileStream(iso, FileMode.CreateNew, FileAccess.Write))
        {
            if (input.BaseStream.Length == 0 || input.BaseStream.Length % stride != 0)
                throw new InvalidDataException("CD 数据轨的长度不是完整扇区。");
            for (long sector = 0; input.BaseStream.Position < input.BaseStream.Length; sector++)
            {
                byte[] data = input.ReadBytes(stride);
                if (data.Length != stride)
                    throw new InvalidDataException("CD 数据轨意外结束。");
                if (stride == 2352)
                {
                    bool sync = data[0] == 0 && data[11] == 0;
                    for (int i = 1; i < 11; i++) sync &= data[i] == 255;
                    if (!sync || data[15] != (mode == "MODE1/2352" ? 1 : 2))
                        throw new InvalidDataException("CD 扇区 " + sector + " 的原始扇区头不符合轨道格式。");
                }
                if (mode == "MODE2/2352" || mode == "MODE2/2336")
                {
                    int subheader = offset - 8;
                    for (int i = 0; i < 4; i++)
                        if (data[subheader + i] != data[subheader + 4 + i])
                            throw new InvalidDataException("CD 扇区 " + sector + " 的 XA 子头不一致，未输出 ISO。");
                    if ((data[subheader + 2] & 0x20) != 0)
                        throw new InvalidDataException("该 PS2 CD 含 Mode 2 Form 2 扇区，ISO 无法完整保留，未输出文件。");
                }
                output.Write(data, offset, 2048);
            }
        }
    }

    internal static void ToBinCue(string chdman, string input, string bin, string cue)
    {
        if (File.Exists(bin) || File.Exists(cue) || Directory.Exists(bin) || Directory.Exists(cue))
            throw new IOException("输出已存在，请另存。");
        string work = Path.Combine(Path.GetDirectoryName(bin), ".chd-export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work); bool committedBin = false;
        try
        {
            DiscProcess.Run(chdman, "extractcd -i \"" + input + "\" -o disc.cue -ob disc.bin", work);
            string stagedBin = Path.Combine(work, "disc.bin"), stagedCue = Path.Combine(work, "disc.cue");
            if (!File.Exists(stagedBin) || new FileInfo(stagedBin).Length == 0 || !File.Exists(stagedCue))
                throw new InvalidDataException("未生成有效的 BIN/CUE 文件。");
            string descriptor = File.ReadAllText(stagedCue);
            if (!descriptor.Contains("\"disc.bin\"")) throw new InvalidDataException("导出的 CUE 没有预期的 BIN 引用。");
            string reference = string.Equals(Path.GetDirectoryName(bin), Path.GetDirectoryName(cue), StringComparison.OrdinalIgnoreCase) ? Path.GetFileName(bin) : bin;
            descriptor = descriptor.Replace("\"disc.bin\"", "\"" + reference + "\"");
            File.WriteAllText(stagedCue, descriptor, new UTF8Encoding(false));
            File.Move(stagedBin, bin); committedBin = true;
            // File.Move never overwrites a file created by another window during conversion.
            File.Move(stagedCue, cue); committedBin = false;
        }
        catch { if (committedBin) File.Delete(bin); throw; }
        finally { Directory.Delete(work, true); }
    }
}
