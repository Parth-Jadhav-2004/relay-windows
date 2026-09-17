using System.Runtime.InteropServices;

namespace Tinycast.Features.Updates;

public static class UpdateRelease
{
    public const string NotesMarker = "<!-- tinycast:install -->";

    public static string ZipAssetName(Architecture architecture) => architecture switch
    {
        Architecture.Arm64 => "Tinycast-windows-arm64.zip",
        _ => "Tinycast-windows-x64.zip",
    };

    public static string DisplayVersion(string? informational)
    {
        if (string.IsNullOrWhiteSpace(informational))
            return "";
        var plus = informational.IndexOf('+');
        return (plus >= 0 ? informational[..plus] : informational).Trim();
    }

    public static Version? ParseTag(string tag)
    {
        var trimmed = tag.Trim().TrimStart('v', 'V');
        var dash = trimmed.IndexOf('-');
        if (dash > 0)
            trimmed = trimmed[..dash];
        return Version.TryParse(trimmed, out var version) ? version : null;
    }

    public static string NotesSummary(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "";
        var text = body.Replace("\r\n", "\n");
        var marker = text.IndexOf(NotesMarker, StringComparison.Ordinal);
        if (marker >= 0)
            text = text[..marker];
        return text.Trim();
    }

    public static string? PickAsset(IEnumerable<string> names, Architecture architecture)
    {
        var wanted = ZipAssetName(architecture);
        var exact = names.FirstOrDefault(n => n.Equals(wanted, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;
        return names.FirstOrDefault(n =>
            n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            && n.Contains("windows", StringComparison.OrdinalIgnoreCase)
            && n.Contains(architecture == Architecture.Arm64 ? "arm64" : "x64", StringComparison.OrdinalIgnoreCase));
    }

    public readonly record struct ReleaseListing(
        string Tag,
        Version Version,
        bool Draft,
        bool Prerelease,
        IReadOnlyList<string> AssetNames);

    public static ReleaseListing? SelectLatest(
        IEnumerable<ReleaseListing> releases,
        Architecture architecture,
        bool includePrerelease = false)
    {
        ReleaseListing? best = null;
        foreach (var release in releases)
        {
            if (release.Draft)
                continue;
            if (release.Prerelease && !includePrerelease)
                continue;
            if (PickAsset(release.AssetNames, architecture) is null)
                continue;
            if (best is null || release.Version > best.Value.Version)
                best = release;
        }

        return best;
    }
}
