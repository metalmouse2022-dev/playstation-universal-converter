using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

internal sealed class DiscSet
{
    private static readonly Regex DiscNumber = new Regex(
        @"[\(\[（]?\s*(?:(?<![A-Z])(?:CD|DISC|DISK)[\s._-]*(?<number>\d+)(?![A-Z0-9])(?:\s+OF\s+\d+)?|第?\s*(?<number>\d+)\s*[碟盘])\s*[\)\]）]?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal readonly GameImage[] Images;
    internal readonly string Title;

    private DiscSet(GameImage[] images, string title)
    {
        Images = images;
        Title = title;
    }

    internal static DiscSet Read(string[] files, string chdman)
    {
        string[] paths = files.Select(DiscInput.Resolve).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (paths.Length > 1)
        {
            var tracks = new HashSet<string>(paths.Where(path => Path.GetExtension(path).Equals(".cue",
                StringComparison.OrdinalIgnoreCase)).SelectMany(DiscProbe.CueTracks), StringComparer.OrdinalIgnoreCase);
            paths = paths.Where(path => !tracks.Contains(path)).ToArray();
        }
        if (paths.Length == 1)
            return new DiscSet(new[] { DiscProbe.Read(paths[0], chdman) }, Path.GetFileNameWithoutExtension(paths[0]));
        if (paths.Length < 2 || paths.Length > 5)
            throw InvalidSet();

        var numbered = new SortedDictionary<int, GameImage>();
        string title = null;
        string titleKey = null;
        foreach (string path in paths)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            Match match = DiscNumber.Match(name);
            int number;
            if (!match.Success || !int.TryParse(match.Groups["number"].Value, out number) ||
                number < 1 || number > 5 || numbered.ContainsKey(number))
                throw InvalidSet();

            string group = (name.Substring(0, match.Index) + " " + name.Substring(match.Index + match.Length))
                .Trim(' ', '.', '-', '_');
            string key = Regex.Replace(group, @"[\s._-]+", " ");
            if (key.Length == 0 || (titleKey != null && !key.Equals(titleKey, StringComparison.OrdinalIgnoreCase)))
                throw InvalidSet();
            title = title ?? group;
            titleKey = titleKey ?? key;
            GameImage image = DiscProbe.Read(path, chdman);
            if (image.Platform != GamePlatform.Ps1 || image.Extension == ".pbp")
                throw InvalidSet();
            numbered.Add(number, image);
        }
        if (!numbered.Keys.SequenceEqual(Enumerable.Range(1, numbered.Count)))
            throw new InvalidDataException("合盘需要从 CD1 开始的连续盘号，请补齐缺少的碟。");
        return new DiscSet(numbered.Values.ToArray(), title);
    }

    internal string CombinedOutput(Destination target, string outputFolder)
    {
        string folder = outputFolder ?? Path.GetDirectoryName(Images[0].FilePath);
        return target == Destination.Hardware ? Path.Combine(folder, Title + "_PSP", "EBOOT.PBP") :
            Path.Combine(folder, Title + ".m3u");
    }

    internal static void WritePlaylist(string playlist, IEnumerable<string> discs)
    {
        string directory = Path.GetDirectoryName(playlist);
        string temporary = Path.Combine(directory, ".playlist-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllLines(temporary, discs.Select(disc =>
                string.Equals(directory, Path.GetDirectoryName(disc), StringComparison.OrdinalIgnoreCase)
                    ? Path.GetFileName(disc) : disc), new UTF8Encoding(false));
            File.Move(temporary, playlist);
        }
        finally { File.Delete(temporary); }
    }

    private static InvalidDataException InvalidSet()
    {
        return new InvalidDataException("请选择单个镜像，或同一个 PS1 游戏的 2–5 张盘。请在文件名标出 CD1、CD2 等盘号。");
    }
}
