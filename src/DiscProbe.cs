using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

internal enum GamePlatform { Ps1, Psp, Ps2 }
internal enum Destination { Emulator, Hardware }
internal enum OutputFormat { Chd, Pbp, Iso }
internal enum DiscMedium { Unknown, Cd, Dvd }

internal sealed class GameImage
{
    internal readonly string FilePath;
    internal readonly string Extension;
    internal readonly GamePlatform Platform;
    internal readonly string TrackMode;
    internal readonly DiscMedium Medium;
    internal bool Cd { get { return Medium == DiscMedium.Cd; } }

    internal GameImage(string path, GamePlatform platform, DiscMedium medium, string trackMode)
    {
        FilePath = path;
        Extension = Path.GetExtension(path).ToLowerInvariant();
        Platform = platform;
        TrackMode = trackMode;
        Medium = medium;
    }

    internal OutputFormat FormatFor(Destination target)
    {
        if (target == Destination.Emulator)
            return OutputFormat.Chd;
        return Platform == GamePlatform.Ps1 ? OutputFormat.Pbp : OutputFormat.Iso;
    }

    internal bool IsReadyFor(Destination target)
    {
        // DuckStation also accepts the unencrypted PS1 PBP that Probe validates.
        return Extension == "." + FormatFor(target).ToString().ToLowerInvariant() ||
            Extension == ".pbp";
    }
}

internal static class DiscProbe
{
    private static readonly Regex CueFile = new Regex(@"^\s*FILE\s+(?:""([^""]+)""|(\S+))\s+BINARY\s*$", RegexOptions.IgnoreCase);
    private static readonly Regex CueTrack = new Regex(@"^\s*TRACK\s+\d+\s+(\S+)\s*$", RegexOptions.IgnoreCase);
    private static readonly Regex CueIndex = new Regex(@"^\s*INDEX\s+01\s+(\d+):(\d+):(\d+)\s*$", RegexOptions.IgnoreCase);
    private static readonly string[] DataModes = { "MODE1/2048", "MODE2/2336", "MODE1/2352", "MODE2/2352" };

    internal static GameImage Read(string path, string chdman)
    {
        path = DiscInput.Resolve(path);
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".cue")
            return ReadCue(path);
        if (extension == ".chd")
            return ReadChd(path, chdman);
        if (extension == ".pbp")
            return ReadPbp(path);

        using (var file = File.OpenRead(path))
        {
            foreach (string mode in DataModes)
            {
                GamePlatform? platform = ReadTrack(file, mode, 0);
                if (platform.HasValue)
                {
                    DiscMedium medium = platform == GamePlatform.Ps1 || mode != "MODE1/2048" ? DiscMedium.Cd :
                        platform == GamePlatform.Psp || IsDvdSizedVolume(file) ? DiscMedium.Dvd : DiscMedium.Unknown;
                    return new GameImage(path, platform.Value, medium, mode);
                }
            }
        }
        throw Unrecognized();
    }

    private static GameImage ReadCue(string path)
    {
        string trackFile = null;
        string mode = null;
        foreach (string line in ReadCueLines(path))
        {
            Match file = CueFile.Match(line);
            if (file.Success)
            {
                trackFile = CueTrackPath(path, file);
                mode = null;
                continue;
            }
            Match track = CueTrack.Match(line);
            if (track.Success)
            {
                mode = track.Groups[1].Value.ToUpperInvariant();
                continue;
            }
            Match index = CueIndex.Match(line);
            if (index.Success && trackFile != null && Array.IndexOf(DataModes, mode) >= 0)
            {
                long firstSector = (long.Parse(index.Groups[1].Value) * 60 + int.Parse(index.Groups[2].Value)) * 75
                    + int.Parse(index.Groups[3].Value);
                using (var stream = File.OpenRead(trackFile))
                {
                    GamePlatform? platform = ReadTrack(stream, mode, firstSector);
                    if (platform.HasValue)
                        return new GameImage(path, platform.Value, DiscMedium.Cd, mode);
                }
            }
        }
        throw Unrecognized();
    }

    internal static System.Collections.Generic.IEnumerable<string> CueTracks(string path)
    {
        foreach (string line in ReadCueLines(path))
        {
            Match file = CueFile.Match(line);
            if (file.Success)
                yield return CueTrackPath(path, file);
        }
    }

    private static string[] ReadCueLines(string path)
    {
        try { return File.ReadAllLines(path, new UTF8Encoding(false, true)); }
        catch (DecoderFallbackException) { return File.ReadAllLines(path, Encoding.Default); }
    }

    private static string CueTrackPath(string path, Match file)
    {
        string name = file.Groups[1].Success ? file.Groups[1].Value : file.Groups[2].Value;
        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), name));
    }

    private static bool IsDvdSizedVolume(Stream file)
    {
        // Use a conservative bound well above CD-ROM capacity, and require the volume
        // descriptor to account for the size so a small image with padding stays unknown.
        const long bound = 1L << 30;
        if (file.Length <= bound)
            return false;
        var descriptor = new byte[2048];
        for (int i = 16; i < 32; i++)
        {
            file.Position = i * 2048L;
            if (file.Read(descriptor, 0, descriptor.Length) != descriptor.Length ||
                Encoding.ASCII.GetString(descriptor, 1, 5) != "CD001" || descriptor[0] == 255)
                return false;
            if (descriptor[0] == 1)
            {
                long bytes = (long)BitConverter.ToUInt32(descriptor, 80) * 2048;
                return bytes > bound && bytes <= file.Length;
            }
        }
        return false;
    }

    private static GameImage ReadChd(string path, string chdman)
    {
        string info = DiscProcess.Run(chdman, string.Format("info -i \"{0}\"", path), Path.GetDirectoryName(path));
        int stride = checked((int)InfoNumber(info, "Unit Size"));
        long total = InfoNumber(info, "Total Units");
        if (total <= 16 || (stride != 2048 && stride != 2448))
            throw Unrecognized();

        // chdman can read a range of sectors, so inspection does not extract the whole disc.
        const int chunkSectors = 64;
        string chunkFile = Path.GetTempFileName();
        long first = -chunkSectors;
        byte[] chunk = null;
        try
        {
            GamePlatform? platform = ReadVolume(sector =>
            {
                if (sector < 0 || sector >= total)
                    return null;
                if (sector < first || sector >= first + chunkSectors)
                {
                    first = sector / chunkSectors * chunkSectors;
                    long length = Math.Min(chunkSectors, total - first) * stride;
                    DiscProcess.Run(chdman,
                        string.Format("extractraw -f -i \"{0}\" -o \"{1}\" -isb {2} -ib {3}",
                            path, chunkFile, first * stride, length), Path.GetDirectoryName(chunkFile));
                    chunk = File.ReadAllBytes(chunkFile);
                }
                int offset = (int)(sector - first) * stride;
                if (stride == 2448 && chunk[offset] == 0 && chunk[offset + 1] == 255 && chunk[offset + 11] == 0)
                    offset += chunk[offset + 15] == 2 ? 24 : 16;
                else if (stride == 2448 && info.IndexOf("TYPE:MODE2 ", StringComparison.Ordinal) >= 0)
                    offset += 8;
                var data = new byte[2048];
                Buffer.BlockCopy(chunk, offset, data, 0, data.Length);
                return data;
            });
            if (platform.HasValue)
                return new GameImage(path, platform.Value, stride == 2448 ? DiscMedium.Cd :
                    info.IndexOf("Tag='DVD '", StringComparison.Ordinal) >= 0 ? DiscMedium.Dvd : DiscMedium.Unknown,
                    stride == 2448 ? ChdTrackMode(info) : null);
            throw Unrecognized();
        }
        finally { File.Delete(chunkFile); }
    }

    private static long InfoNumber(string output, string key)
    {
        Match match = Regex.Match(output, "^" + key + @":\s*([0-9,]+)", RegexOptions.Multiline);
        return match.Success ? long.Parse(match.Groups[1].Value.Replace(",", "")) : 0;
    }

    private static string ChdTrackMode(string info)
    {
        switch (Regex.Match(info, @"(?m)^\s+TRACK:1\s+TYPE:(\S+)").Groups[1].Value)
        {
            case "MODE1": return "MODE1/2048";
            case "MODE1_RAW": return "MODE1/2352";
            case "MODE2": return "MODE2/2336";
            case "MODE2_RAW": return "MODE2/2352";
            case "MODE2_FORM1": return "MODE2/2048";
            case "MODE2_FORM2": return "MODE2/2324";
            default: return null;
        }
    }

    private static GameImage ReadPbp(string path)
    {
        using (var file = new BinaryReader(File.OpenRead(path)))
        {
            if (file.BaseStream.Length >= 40 && file.ReadUInt32() == 0x50425000)
            {
                file.BaseStream.Position = 36;
                long psar = file.ReadUInt32();
                if (psar >= 40 && psar + 0x404 <= file.BaseStream.Length)
                {
                    file.BaseStream.Position = psar;
                    string magic = Encoding.ASCII.GetString(file.ReadBytes(16));
                    bool multiDisc = magic.StartsWith("PSTITLEIMG", StringComparison.Ordinal);
                    if (multiDisc || magic.StartsWith("PSISOIMG0000", StringComparison.Ordinal))
                    {
                        file.BaseStream.Position = psar + (multiDisc ? 0x200 : 0x400);
                        if (file.ReadUInt32() != 0x44475000)
                            return new GameImage(path, GamePlatform.Ps1, DiscMedium.Cd, null);
                    }
                }
            }
        }
        throw new InvalidDataException("只识别未加密的 PS1 EBOOT.PBP。");
    }

    private static GamePlatform? ReadTrack(Stream file, string mode, long firstSector)
    {
        int stride = int.Parse(mode.Substring(6));
        int offset = stride == 2048 ? 0 : stride == 2336 ? 8 : mode[4] == '2' ? 24 : 16;
        if (stride == 2336)
        {
            long header = (firstSector + 16) * stride;
            if (header < 0 || header > file.Length - 8)
                return null;
            file.Position = header;
            var subheader = new byte[8];
            if (file.Read(subheader, 0, subheader.Length) != subheader.Length)
                return null;
            for (int i = 0; i < 4; i++)
                if (subheader[i] != subheader[i + 4])
                    return null;
        }
        if (stride == 2352)
        {
            long header = (firstSector + 16) * stride;
            if (header < 0 || header > file.Length - 16)
                return null;
            file.Position = header;
            if (file.ReadByte() != 0)
                return null;
            for (int i = 0; i < 10; i++)
                if (file.ReadByte() != 255)
                    return null;
            if (file.ReadByte() != 0)
                return null;
            file.Position = header + 15;
            if (file.ReadByte() != mode[4] - '0')
                return null;
        }
        return ReadVolume(sector =>
        {
            long start = (firstSector + sector) * stride + offset;
            if (start < 0 || start > file.Length - 2048)
                return null;
            file.Position = start;
            var data = new byte[2048];
            return file.Read(data, 0, data.Length) == data.Length ? data : null;
        });
    }

    private static GamePlatform? ReadVolume(Func<long, byte[]> readSector)
    {
        for (int i = 16; i < 32; i++)
        {
            byte[] descriptor = readSector(i);
            if (descriptor == null || Encoding.ASCII.GetString(descriptor, 1, 5) != "CD001" || descriptor[0] == 255)
                return null;
            if (descriptor[0] != 1)
                continue;

            byte[] root = ReadExtent(readSector, descriptor, 156, 2 * 1024 * 1024);
            if (root == null)
                return null;
            int psp = FindEntry(root, "PSP_GAME");
            if (psp >= 0 && (root[psp + 25] & 2) != 0)
            {
                byte[] pspDirectory = ReadExtent(readSector, root, psp, 2 * 1024 * 1024);
                if (pspDirectory != null && FindEntry(pspDirectory, "PARAM.SFO") >= 0)
                    return GamePlatform.Psp;
            }

            int configEntry = FindEntry(root, "SYSTEM.CNF");
            if (configEntry >= 0)
            {
                byte[] config = ReadExtent(readSector, root, configEntry, 64 * 1024);
                if (config != null)
                {
                    string text = Encoding.ASCII.GetString(config);
                    if (Regex.IsMatch(text, @"(?im)^\s*BOOT2\s*="))
                        return GamePlatform.Ps2;
                    if (Regex.IsMatch(text, @"(?im)^\s*BOOT\s*="))
                        return GamePlatform.Ps1;
                }
            }
            return FindEntry(root, "PSX.EXE") >= 0 ? (GamePlatform?)GamePlatform.Ps1 : null;
        }
        return null;
    }

    private static byte[] ReadExtent(Func<long, byte[]> read, byte[] record, int index, uint limit)
    {
        if (index + 18 > record.Length)
            return null;
        uint sector = BitConverter.ToUInt32(record, index + 2);
        uint length = BitConverter.ToUInt32(record, index + 10);
        if (length == 0 || length > limit)
            return null;
        var extent = new byte[length];
        for (int offset = 0; offset < extent.Length; offset += 2048)
        {
            byte[] block = read((long)sector + offset / 2048);
            if (block == null)
                return null;
            Buffer.BlockCopy(block, 0, extent, offset, Math.Min(2048, extent.Length - offset));
        }
        return extent;
    }

    private static int FindEntry(byte[] directory, string target)
    {
        for (int i = 0; i < directory.Length;)
        {
            int length = directory[i];
            if (length == 0)
            {
                i = (i / 2048 + 1) * 2048;
                continue;
            }
            if (length < 34 || i + length > directory.Length || directory[i + 32] > length - 33)
                return -1;
            string name = Encoding.ASCII.GetString(directory, i + 33, directory[i + 32]);
            int version = name.IndexOf(';');
            if (version >= 0)
                name = name.Substring(0, version);
            if (name.Equals(target, StringComparison.OrdinalIgnoreCase))
                return i;
            i += length;
        }
        return -1;
    }

    private static InvalidDataException Unrecognized()
    {
        return new InvalidDataException("未识别为 PS1、PSP 或 PS2 游戏镜像。多轨 CD 请选原始 CUE。");
    }
}
