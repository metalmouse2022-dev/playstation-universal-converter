using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

internal sealed class GameArtwork
{
    internal string Path;
    internal byte[] Icon, Background;
    internal bool HasAnimation;
    private const int Sector = 2048;
    private sealed class Entry
    {
        internal long Record, Offset;
        internal uint Length;
        internal string Name;
        internal bool Directory;
        internal byte[] Raw;
    }
    private sealed class Iso
    {
        internal long Pvd;
        internal Entry Game;
        internal List<Entry> Entries;
    }
    private static byte[] Read(Stream stream, long offset, int count)
    {
        if (offset < 0 || count < 0 || offset > stream.Length - count) throw new InvalidDataException("文件长度或资源偏移无效。");
        var bytes = new byte[count]; stream.Position = offset; int done = 0;
        while (done < count) { int got = stream.Read(bytes, done, count - done); if (got == 0) throw new EndOfStreamException(); done += got; }
        return bytes;
    }
    private static long[] PbpOffsets(Stream stream)
    {
        byte[] header = Read(stream, 0, 40);
        if (BitConverter.ToUInt32(header, 0) != 0x50425000) throw new InvalidDataException("不是有效的 EBOOT.PBP。");
        long[] offsets = new long[9]; offsets[8] = stream.Length;
        for (int i = 0; i < 8; i++) offsets[i] = BitConverter.ToUInt32(header, 8 + 4 * i);
        if (offsets[0] < 40 || offsets.Where((value, i) => i > 0 && value < offsets[i - 1]).Any())
            throw new InvalidDataException("PBP 资源表无效。");
        return offsets;
    }
    private static byte[] Resource(Stream stream, long offset, long size)
    {
        if (size == 0) return null;
        if (size > 16 * 1024 * 1024) throw new InvalidDataException("图片资源异常大，已停止读取。");
        return Read(stream, offset, checked((int)size));
    }
    private static Entry Record(byte[] data, int start, long absolute)
    {
        int length = data[start], nameLength = data[start + 32];
        if (length < 34 || start + length > data.Length || 33 + nameLength > length)
            throw new InvalidDataException("ISO 目录记录损坏。");
        uint lba = BitConverter.ToUInt32(data, start + 2), size = BitConverter.ToUInt32(data, start + 10);
        if (data[start + 1] != 0 || (data[start + 25] & 0x80) != 0 || data[start + 26] != 0 || data[start + 27] != 0)
            throw new InvalidDataException("暂不支持分段、交错或扩展属性的 ISO 资源。");
        return new Entry { Record = absolute, Offset = (long)lba * Sector, Length = size,
            Name = Encoding.ASCII.GetString(data, start + 33, nameLength).Split(';')[0], Directory = (data[start + 25] & 2) != 0,
            Raw = data.Skip(start).Take(length).ToArray() };
    }
    private static List<Entry> DirectoryEntries(Stream stream, Entry directory)
    {
        if (!directory.Directory || directory.Length > 16 * 1024 * 1024) throw new InvalidDataException("ISO 目录无效。");
        byte[] data = Read(stream, directory.Offset, checked((int)directory.Length)); var list = new List<Entry>();
        int i = 0;
        while (i < data.Length)
        {
            if (data[i] == 0) { i = (i / Sector + 1) * Sector; continue; }
            if (i + 34 > data.Length || i % Sector + data[i] > Sector) throw new InvalidDataException("ISO 目录记录越界。");
            Entry entry = Record(data, i, directory.Offset + i);
            if (entry.Offset > stream.Length - entry.Length) throw new InvalidDataException("ISO 资源超出文件范围。");
            list.Add(entry); i += data[i];
        }
        return list;
    }
    private static Iso ReadIso(Stream stream)
    {
        if (stream.Length % Sector != 0) throw new InvalidDataException("请选择 2048 字节扇区的 PSP ISO。");
        Entry root = null; long pvd = 0;
        for (int s = 16; s < 80; s++)
        {
            byte[] data = Read(stream, s * (long)Sector, Sector);
            if (Encoding.ASCII.GetString(data, 1, 5) != "CD001") throw new InvalidDataException("不是受支持的 ISO9660 镜像。");
            if (data[0] == 255) break;
            if (data[0] == 2) throw new InvalidDataException("该镜像包含额外目录树，暂不直接写入。可以导出 PNG 后使用镜像编辑工具导入。");
            if (data[0] == 1)
            {
                if (BitConverter.ToUInt16(data, 128) != Sector) throw new InvalidDataException("ISO 扇区大小不受支持。");
                pvd = s * (long)Sector; root = Record(data, 156, pvd + 156);
            }
        }
        if (root == null) throw new InvalidDataException("ISO 没有主卷描述符。");
        Entry game = DirectoryEntries(stream, root).FirstOrDefault(e => e.Directory && e.Name == "PSP_GAME");
        if (game == null) throw new InvalidDataException("镜像中没有 PSP_GAME 目录，请选择 PSP 游戏 ISO。");
        return new Iso { Pvd = pvd, Game = game, Entries = DirectoryEntries(stream, game) };
    }
    internal static GameArtwork Open(string path)
    {
        var result = new GameArtwork { Path = System.IO.Path.GetFullPath(path) };
        using (Stream stream = File.OpenRead(path))
        {
            if (System.IO.Path.GetExtension(path).Equals(".pbp", StringComparison.OrdinalIgnoreCase))
            {
                long[] offsets = PbpOffsets(stream);
                result.Icon = Resource(stream, offsets[1], offsets[2] - offsets[1]);
                result.Background = Resource(stream, offsets[4], offsets[5] - offsets[4]);
                result.HasAnimation = offsets[3] > offsets[2];
            }
            else if (System.IO.Path.GetExtension(path).Equals(".iso", StringComparison.OrdinalIgnoreCase))
            {
                Iso iso = ReadIso(stream);
                foreach (Entry e in iso.Entries)
                {
                    if (e.Directory) continue;
                    if (e.Name == "ICON0.PNG") result.Icon = Resource(stream, e.Offset, e.Length);
                    if (e.Name == "PIC1.PNG") result.Background = Resource(stream, e.Offset, e.Length);
                    if (e.Name == "ICON1.PMF" && e.Length > 0) result.HasAnimation = true;
                }
            }
            else throw new InvalidDataException("设计页支持 PSP ISO 和 EBOOT.PBP；压缩镜像请先转换。");
        }
        return result;
    }
    internal void SaveAs(string output, byte[] icon, byte[] background, Action<int> progress)
    {
        output = System.IO.Path.GetFullPath(output);
        if (string.Equals(output, Path, StringComparison.OrdinalIgnoreCase) || File.Exists(output) || Directory.Exists(output))
            throw new IOException("输出已存在，请另存到新的文件名。");
        string temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (Stream input = File.OpenRead(Path))
            using (Stream dest = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                if (System.IO.Path.GetExtension(Path).Equals(".pbp", StringComparison.OrdinalIgnoreCase)) WritePbp(input, dest, icon, background, progress);
                else WriteIso(input, dest, icon, background, progress);
                dest.Flush();
            }
            // Re-open the staged container and confirm both directory/header pointers and payload bytes.
            using (Stream check = File.OpenRead(temporary))
            {
                if (System.IO.Path.GetExtension(Path).Equals(".pbp", StringComparison.OrdinalIgnoreCase))
                {
                    long[] o = PbpOffsets(check);
                    if (icon != null && !Resource(check, o[1], o[2] - o[1]).SequenceEqual(icon) ||
                        background != null && !Resource(check, o[4], o[5] - o[4]).SequenceEqual(background)) throw new InvalidDataException("写入校验失败。");
                }
                else
                {
                    Iso iso = ReadIso(check);
                    foreach (var item in new[] { new { Name = "ICON0.PNG", Data = icon }, new { Name = "PIC1.PNG", Data = background } })
                    {
                        if (item.Data == null) continue;
                        Entry e = iso.Entries.Single(x => x.Name == item.Name);
                        if (!Resource(check, e.Offset, e.Length).SequenceEqual(item.Data)) throw new InvalidDataException("写入校验失败。");
                    }
                }
            }
            File.Move(temporary, output);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void Copy(Stream input, Stream output, long size, Action<int> progress)
    {
        var buffer = new byte[1024 * 1024]; long done = 0; int previous = -1;
        while (done < size)
        {
            int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, size - done));
            if (read == 0) throw new EndOfStreamException(); output.Write(buffer, 0, read); done += read;
            int percent = (int)(100 * input.Position / input.Length);
            if (percent != previous && progress != null) { progress(percent); previous = percent; }
        }
    }
    private static void WritePbp(Stream input, Stream output, byte[] icon, byte[] background, Action<int> progress)
    {
        long[] offsets = PbpOffsets(input); byte[] header = Read(input, 0, 40); output.Write(header, 0, header.Length);
        for (int i = 0; i < 8; i++)
        {
            if (output.Position > uint.MaxValue) throw new InvalidDataException("PBP 大小超过格式上限。");
            Array.Copy(BitConverter.GetBytes((uint)output.Position), 0, header, 8 + 4 * i, 4);
            byte[] replacement = i == 1 ? icon : i == 4 ? background : null;
            if (replacement != null) output.Write(replacement, 0, replacement.Length);
            else { input.Position = offsets[i]; Copy(input, output, offsets[i + 1] - offsets[i], progress); }
        }
        output.Position = 0; output.Write(header, 0, header.Length);
    }
    private static void Both(byte[] bytes, int index, uint value)
    {
        byte[] little = BitConverter.GetBytes(value); Array.Copy(little, 0, bytes, index, 4); Array.Reverse(little); Array.Copy(little, 0, bytes, index + 4, 4);
    }
    private static byte[] OrderedDirectory(byte[] directory)
    {
        var records = new List<byte[]>();
        for (int i = 0; i < directory.Length;)
        {
            int size = directory[i]; if (size == 0) { i = (i / Sector + 1) * Sector; continue; }
            records.Add(directory.Skip(i).Take(size).ToArray()); i += size;
        }
        var result = new byte[directory.Length]; int cursor = 0;
        foreach (byte[] record in records.OrderBy(r => Encoding.ASCII.GetString(r, 33, r[32]), StringComparer.Ordinal))
        {
            if (cursor % Sector + record.Length > Sector) cursor = (cursor / Sector + 1) * Sector;
            if (cursor + record.Length > result.Length) throw new InvalidDataException("目录空间不足，无法加入图片记录。请导出 PNG 后使用镜像编辑工具导入。");
            Array.Copy(record, 0, result, cursor, record.Length); cursor += record.Length;
        }
        return result;
    }
    private static void WriteIso(Stream input, Stream output, byte[] icon, byte[] background, Action<int> progress)
    {
        Iso iso = ReadIso(input); byte[] directory = Read(input, iso.Game.Offset, checked((int)iso.Game.Length));
        var updates = new List<KeyValuePair<int, byte[]>>();
        foreach (var item in new[] { new { Name = "ICON0.PNG", Data = icon }, new { Name = "PIC1.PNG", Data = background } })
        {
            if (item.Data == null) continue;
            Entry e = iso.Entries.SingleOrDefault(x => x.Name == item.Name && !x.Directory);
            int position;
            if (e == null)
            {
                byte[] name = Encoding.ASCII.GetBytes(item.Name + ";1"); int length = 33 + name.Length + (name.Length % 2 == 0 ? 1 : 0);
                position = -1;
                // Append to a sector's zero padding without relocating any existing directory or game extent.
                for (int i = 0; i < directory.Length;)
                {
                    if (directory[i] > 0) { i += directory[i]; continue; }
                    int end = Math.Min(directory.Length, (i / Sector + 1) * Sector);
                    if (end - i >= length && directory.Skip(i).Take(end - i).All(b => b == 0)) { position = i; break; }
                    i = end;
                }
                if (position < 0) throw new InvalidDataException("PSP_GAME 目录没有可用记录空间，请先导出 PNG，再用镜像编辑工具导入。");
                var record = new byte[length]; record[0] = (byte)length; record[28] = 1; record[31] = 1; record[32] = (byte)name.Length;
                Array.Copy(iso.Game.Raw, 18, record, 18, 7); Array.Copy(name, 0, record, 33, name.Length);
                Array.Copy(record, 0, directory, position, length);
            }
            else position = checked((int)(e.Record - iso.Game.Offset));
            updates.Add(new KeyValuePair<int, byte[]>(position, item.Data));
        }
        input.Position = 0; Copy(input, output, input.Length, progress);
        foreach (var update in updates)
        {
            long offset = (output.Length + Sector - 1) / Sector * Sector;
            output.SetLength(offset); output.Position = offset; output.Write(update.Value, 0, update.Value.Length);
            Both(directory, update.Key + 2, checked((uint)(offset / Sector))); Both(directory, update.Key + 10, (uint)update.Value.Length);
        }
        output.SetLength((output.Length + Sector - 1) / Sector * Sector);
        directory = OrderedDirectory(directory);
        output.Position = iso.Game.Offset; output.Write(directory, 0, directory.Length);
        byte[] volume = new byte[8]; Both(volume, 0, checked((uint)(output.Length / Sector)));
        output.Position = iso.Pvd + 80; output.Write(volume, 0, 8);
    }
}
