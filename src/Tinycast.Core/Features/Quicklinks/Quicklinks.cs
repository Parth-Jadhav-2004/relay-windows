namespace Tinycast.Features.Quicklinks;

public enum QuicklinkKind { Url, Path, Deeplink }

public sealed record Quicklink(string Id, string Name, string Destination, QuicklinkKind Kind, string? Keyword);

public static class QuicklinkDestination
{
    public static QuicklinkKind Classify(string destination)
    {
        var value = destination.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            return QuicklinkKind.Url;
        if (RegexDrive(value) || value.StartsWith("\\\\", StringComparison.Ordinal) || Directory.Exists(value) || File.Exists(value))
            return QuicklinkKind.Path;
        return QuicklinkKind.Deeplink;
    }

    public static bool RegexDrive(string value) =>
        value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':';

    public static string Expand(string destination, string argument, string selection = "", string clipboard = "", DateTime? now = null)
    {
        var when = now ?? DateTime.Now;
        var encoded = Uri.EscapeDataString(argument);
        return destination
            .Replace("{argument}", argument, StringComparison.OrdinalIgnoreCase)
            .Replace("{query}", encoded, StringComparison.OrdinalIgnoreCase)
            .Replace("{selection}", selection, StringComparison.OrdinalIgnoreCase)
            .Replace("{clipboard}", clipboard, StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", when.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
    }
}
