namespace Relay.Features.Clipboard;

/// <summary>
/// Chooses what a clipboard tick records. Files outrank inline content; a bitmap sitting
/// beside a URL or filename is an image, while prose beside a DIB stays text.
/// </summary>
public static class ClipboardCapture
{
    public const int MaxTextLength = 32_000;
    public const int MaxCapturedFiles = 32;

    public static ClipboardKind? Decide(
        bool hasDurableFiles,
        bool hasBitmap,
        bool hasText,
        string? text)
    {
        if (hasDurableFiles)
            return ClipboardKind.File;
        var body = text ?? "";
        var meaningful = hasText && !string.IsNullOrWhiteSpace(body);
        if (hasBitmap && (!meaningful || IsImageAdjacentText(body)))
            return ClipboardKind.Image;
        if (meaningful)
            return ClipboardKind.Text;
        if (hasBitmap)
            return ClipboardKind.Image;
        return null;
    }

    public static bool IsImageAdjacentText(string text)
    {
        var t = text.Trim();
        if (t.Length == 0)
            return true;
        if (t.Contains('\n') || t.Contains('\r'))
            return false;
        if (t.Length > 2048)
            return false;
        if (t.Contains(' ') && t.Length > 64)
            return false;
        if (Uri.TryCreate(t, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https" or "file" or "ftp")
            return true;
        return IsImagePath(t);
    }

    public static bool IsVolatilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;
        try
        {
            var full = Path.GetFullPath(path);
            if (!File.Exists(full) && !Directory.Exists(full))
                return true;
            foreach (var root in VolatileRoots())
            {
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    public static bool IsImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp"
            or ".tif" or ".tiff" or ".ico" or ".heic";
    }

    static IEnumerable<string> VolatileRoots()
    {
        yield return WithSep(Path.GetTempPath());
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
            yield return WithSep(Path.Combine(local, "Temp"));
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windows))
            yield return WithSep(Path.Combine(windows, "Temp"));
    }

    static string WithSep(string path)
    {
        var full = Path.GetFullPath(path);
        return full.EndsWith(Path.DirectorySeparatorChar) ? full : full + Path.DirectorySeparatorChar;
    }
}
