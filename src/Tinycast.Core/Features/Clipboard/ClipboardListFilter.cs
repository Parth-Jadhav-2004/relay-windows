namespace Tinycast.Features.Clipboard;

public enum ClipboardListFilter
{
    All,
    Text,
    Image,
    File,
    Link,
    Color,
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
        ClipboardListFilter.Color,
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
        ClipboardListFilter.Color => "Colors",
        _ => "All",
    };

    public static bool Matches(ClipboardItem item, ClipboardListFilter filter) => filter switch
    {
        ClipboardListFilter.All => true,
        ClipboardListFilter.Text => item.Kind == ClipboardKind.Text && !IsLink(item.Text),
        ClipboardListFilter.Image => item.Kind == ClipboardKind.Image,
        ClipboardListFilter.File => item.Kind == ClipboardKind.File,
        ClipboardListFilter.Link => item.Kind == ClipboardKind.Text && IsLink(item.Text),
        ClipboardListFilter.Color => item.Kind == ClipboardKind.Text && ClipboardColor.IsColor(item.Text),
        _ => true,
    };

    public static bool IsLink(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var t = text.Trim();
        if (t.Contains('\n') || t.Contains('\r'))
            return false;
        if (!Uri.TryCreate(t, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme is "http" or "https")
            return !string.IsNullOrEmpty(uri.Host);
        if (uri.Scheme == "mailto")
        {
            var path = uri.AbsolutePath.TrimStart('/');
            return !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(path);
        }

        return false;
    }
}
