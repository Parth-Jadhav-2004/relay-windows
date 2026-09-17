namespace Tinycast.Features.Clipboard;

public enum ClipboardListFilter
{
    All,
    Text,
    Image,
    File,
    Link,
}

public static class ClipboardListFilterLogic
{
    public static IReadOnlyList<ClipboardListFilter> All { get; } =
    [
        ClipboardListFilter.All,
        ClipboardListFilter.Text,
        ClipboardListFilter.Image,
        ClipboardListFilter.File,
        ClipboardListFilter.Link,
    ];

    public static ClipboardListFilter Next(ClipboardListFilter current)
    {
        var index = All.ToList().IndexOf(current);
        return All[(index + 1) % All.Count];
    }

    public static string Label(ClipboardListFilter filter) => filter switch
    {
        ClipboardListFilter.Text => "Text",
        ClipboardListFilter.Image => "Images",
        ClipboardListFilter.File => "Files",
        ClipboardListFilter.Link => "Links",
        _ => "All",
    };

    public static bool Matches(ClipboardItem item, ClipboardListFilter filter) => filter switch
    {
        ClipboardListFilter.All => true,
        ClipboardListFilter.Text => item.Kind == ClipboardKind.Text && !IsLink(item.Text),
        ClipboardListFilter.Image => item.Kind == ClipboardKind.Image,
        ClipboardListFilter.File => item.Kind == ClipboardKind.File,
        ClipboardListFilter.Link => item.Kind == ClipboardKind.Text && IsLink(item.Text),
        _ => true,
    };

    public static bool IsLink(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var t = text.Trim();
        if (t.Contains('\n') || t.Contains('\r'))
            return false;
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            return true;
        return Uri.TryCreate(t, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https" or "mailto";
    }
}
