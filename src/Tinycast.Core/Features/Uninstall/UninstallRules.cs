namespace Tinycast.Features.Uninstall;

public sealed record UninstallIdentity(string DisplayName, string? BundleId, IReadOnlyList<string> OtherBundleIds, bool AllowsBundleIdPrefixMatch = true);

public static class UninstallRules
{
    public static readonly HashSet<string> StrippedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "plist", "savedstate", "binarycookies", "lockfile", "lock", "lnk", "url",
    };

    public static IReadOnlyList<string> MatchableForms(string name)
    {
        var forms = new List<string> { name };
        var current = name;
        for (var i = 0; i < 3; i++)
        {
            var ext = Path.GetExtension(current).TrimStart('.').ToLowerInvariant();
            if (ext.Length == 0 || !StrippedExtensions.Contains(ext))
                break;
            var stripped = Path.GetFileNameWithoutExtension(current);
            if (string.IsNullOrEmpty(stripped))
                break;
            forms.Add(stripped);
            current = stripped;
        }

        return forms;
    }

    public static bool MatchesBundleId(string component, UninstallIdentity identity)
    {
        if (identity.BundleId is null)
            return false;
        var id = Folded(identity.BundleId);
        return MatchableForms(component).Any(form =>
        {
            var folded = Folded(form);
            if (!Owns(folded, id, identity.AllowsBundleIdPrefixMatch))
                return false;
            return !identity.OtherBundleIds.Any(other =>
                Folded(other).Length > id.Length && Owns(folded, Folded(other), true));
        });
    }

    public static bool MatchesName(string component, UninstallIdentity identity)
    {
        var needle = Folded(identity.DisplayName);
        if (needle.Length < 3 || IsLibraryWell(needle))
            return false;
        return MatchableForms(component).Any(form => Folded(form) == needle);
    }

    public static bool IsLibraryWell(string folded) => folded is
        "preferences" or "caches" or "containers" or "logs" or "application support"
        or "saved application state" or "webkit" or "httpstorages" or "cookies";

    public static bool IsProtected(string path)
    {
        var full = Path.GetFullPath(path);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var tinycast = Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? "";
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tinycast");
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tinycast");
        return full.StartsWith(windows, StringComparison.OrdinalIgnoreCase)
               || (tinycast.Length > 0 && full.StartsWith(tinycast, StringComparison.OrdinalIgnoreCase))
               || full.StartsWith(appData, StringComparison.OrdinalIgnoreCase)
               || full.StartsWith(local, StringComparison.OrdinalIgnoreCase);
    }

    public static string Folded(string value) => value.Trim().ToLowerInvariant();

    static bool Owns(string folded, string id, bool allowingPrefix)
    {
        if (folded == id)
            return true;
        if (!allowingPrefix || folded.Length <= id.Length || !folded.StartsWith(id, StringComparison.Ordinal))
            return false;
        var next = folded[id.Length];
        return next is '.' or '-';
    }
}

public sealed record UninstallPlan(string AppName, IReadOnlyList<string> Paths)
{
    public IReadOnlyList<string> SafePaths => Paths.Where(p => !UninstallRules.IsProtected(p)).ToList();
}
