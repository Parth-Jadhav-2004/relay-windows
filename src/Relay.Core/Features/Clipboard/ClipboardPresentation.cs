using System.Buffers.Binary;

namespace Relay.Features.Clipboard;

public static class ClipboardPresentation
{
    static ReadOnlySpan<byte> PngSignature => [137, 80, 78, 71, 13, 10, 26, 10];

    public static string ListTitle(ClipboardItem item) => item.Kind switch
    {
        ClipboardKind.Image => "Image",
        ClipboardKind.File => FileTitle(item.FilePath),
        _ => ClipText(item.Text),
    };

    public static string TypeLabel(ClipboardItem item) => item.Kind switch
    {
        ClipboardKind.Image => "Image",
        ClipboardKind.File => "File",
        _ => "Text",
    };

    public static string Glyph(ClipboardItem item) => item.Kind switch
    {
        ClipboardKind.Image => "\uE91B",
        ClipboardKind.File => "\uE8A5",
        _ => "\uE8A5",
    };

    public static string Section(ClipboardItem item, DateTime now) =>
        item.Pinned ? "Pinned" : DateBuckets.From(item.CreatedAt, now).Title();

    public static string? ThumbnailPath(ClipboardItem item)
    {
        if (item.Kind == ClipboardKind.Image && !string.IsNullOrWhiteSpace(item.ImagePath))
            return item.ImagePath;
        if (item.Kind == ClipboardKind.File && ClipboardCapture.IsImagePath(item.FilePath))
            return item.FilePath;
        return null;
    }

    public static string? SourceTitle(string? sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            return null;
        try
        {
            if (sourceId.Contains('\\') || sourceId.Contains('/'))
            {
                var name = Path.GetFileNameWithoutExtension(sourceId);
                return string.IsNullOrWhiteSpace(name) ? sourceId : name;
            }
        }
        catch (Exception)
        {
        }

        return sourceId;
    }

    public static string CopiedLabel(DateTime createdAt, DateTime now)
    {
        var local = createdAt.Kind == DateTimeKind.Utc ? createdAt.ToLocalTime() : createdAt;
        var current = now.Kind == DateTimeKind.Utc ? now.ToLocalTime() : now;
        var time = local.ToString("h:mm:ss tt");
        if (local.Date == current.Date)
            return "Today at " + time;
        if (local.Date == current.Date.AddDays(-1))
            return "Yesterday at " + time;
        return local.ToString("MMM d, yyyy") + " at " + time;
    }

    public static string FileSizeLabel(long bytes)
    {
        if (bytes < 1000)
            return bytes + " bytes";
        if (bytes < 1_000_000)
            return (bytes / 1000.0).ToString("0.#") + " KB";
        if (bytes < 1_000_000_000)
            return (bytes / 1_000_000.0).ToString("0.#") + " MB";
        return (bytes / 1_000_000_000.0).ToString("0.#") + " GB";
    }

    public static string DimensionsLabel(int width, int height) => width + "\u00d7" + height;

    public static int WordCount(string text)
    {
        var count = 0;
        var inWord = false;
        foreach (var c in text)
        {
            var sep = char.IsWhiteSpace(c);
            if (!sep && !inWord)
                count++;
            inWord = !sep;
        }

        return count;
    }

    public static (int Width, int Height)? PngPixelSize(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[24];
            var read = stream.Read(header);
            return PngPixelSize(header[..read]);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static (int Width, int Height)? PngPixelSize(ReadOnlySpan<byte> data)
    {
        if (data.Length < 24)
            return null;
        if (!data[..8].SequenceEqual(PngSignature))
            return null;
        if (data[12] != (byte)'I' || data[13] != (byte)'H' || data[14] != (byte)'D' || data[15] != (byte)'R')
            return null;
        var width = (int)BinaryPrimitives.ReadUInt32BigEndian(data[16..20]);
        var height = (int)BinaryPrimitives.ReadUInt32BigEndian(data[20..24]);
        if (width <= 0 || height <= 0)
            return null;
        return (width, height);
    }

    public static long? FileBytes(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    static string FileTitle(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "File";
        var name = Path.GetFileName(path.TrimEnd('\\', '/'));
        return string.IsNullOrWhiteSpace(name) ? "File" : name;
    }

    static string ClipText(string text)
    {
        var t = text.Trim();
        return t.Length <= 200 ? t : t[..200];
    }
}
