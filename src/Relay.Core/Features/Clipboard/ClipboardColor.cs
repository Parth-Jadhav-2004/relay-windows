using System.Globalization;
using System.Text.RegularExpressions;

namespace Relay.Features.Clipboard;

public sealed record ParsedColor(byte R, byte G, byte B, string Hex)
{
    public string Css => $"rgb({R}, {G}, {B})";
}

public static class ClipboardColor
{
    public static bool IsColor(string? text) => TryParse(text, out _);

    public static bool TryParse(string? text, out ParsedColor color)
    {
        color = new ParsedColor(0, 0, 0, "#000000");
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var t = text.Trim();
        if (t.Contains('\n') || t.Contains('\r') || t.Length > 64)
            return false;

        if (t.StartsWith('#') && TryHex(t[1..], out var hex))
        {
            color = hex;
            return true;
        }

        var rgb = Regex.Match(t, @"^rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})", RegexOptions.IgnoreCase);
        if (rgb.Success
            && byte.TryParse(rgb.Groups[1].Value, out var r)
            && byte.TryParse(rgb.Groups[2].Value, out var g)
            && byte.TryParse(rgb.Groups[3].Value, out var b))
        {
            color = new ParsedColor(r, g, b, $"#{r:X2}{g:X2}{b:X2}");
            return true;
        }

        return false;
    }

    static bool TryHex(string body, out ParsedColor color)
    {
        color = new ParsedColor(0, 0, 0, "#000000");
        if (body.Length == 3
            && byte.TryParse(body[0].ToString() + body[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            && byte.TryParse(body[1].ToString() + body[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            && byte.TryParse(body[2].ToString() + body[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            color = new ParsedColor(r, g, b, $"#{r:X2}{g:X2}{b:X2}");
            return true;
        }

        if (body.Length == 6
            && byte.TryParse(body[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
            && byte.TryParse(body[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
            && byte.TryParse(body[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
        {
            color = new ParsedColor(r, g, b, $"#{r:X2}{g:X2}{b:X2}");
            return true;
        }

        return false;
    }
}
