using Tinycast.Features.Launcher;

namespace Tinycast.Features.FileSearch;

public sealed record MenuSearchItem(
    string Path,
    string Shortcut,
    uint CommandId = 0,
    nint Hwnd = 0,
    string Title = "",
    string ParentPath = "",
    bool Enabled = true,
    bool Hidden = false)
{
    public string LeafTitle => string.IsNullOrEmpty(Title) ? LeafOf(Path) : Title;
    public string Section => string.IsNullOrEmpty(ParentPath) ? FirstOf(Path) : FirstOf(ParentPath);
    public string SearchPath => string.IsNullOrEmpty(ParentPath) ? Path : ParentPath;
    public bool IsEligible => Enabled && !Hidden && LeafTitle.Length > 0 && LeafTitle is not "-";

    public static string Join(IReadOnlyList<string> parts) =>
        string.Join(" → ", parts.Where(p => p.Length > 0));

    static string LeafOf(string path)
    {
        var split = path.LastIndexOf(" → ", StringComparison.Ordinal);
        if (split < 0)
            split = path.LastIndexOf(" › ", StringComparison.Ordinal);
        return split < 0 ? path : path[(split + 3)..];
    }

    static string FirstOf(string path)
    {
        var split = path.IndexOf(" → ", StringComparison.Ordinal);
        if (split < 0)
            split = path.IndexOf(" › ", StringComparison.Ordinal);
        return split < 0 ? path : path[..split];
    }
}

public enum MenuSearchTargetKind
{
    Searchable,
    Excluded,
    SelfTarget,
    MenuLess,
    NoApplication,
}

public static class MenuSearchTarget
{
    public static MenuSearchTargetKind Classify(string? processPath, string? selfPath, IEnumerable<string> excluded)
    {
        if (string.IsNullOrWhiteSpace(processPath))
            return MenuSearchTargetKind.NoApplication;
        if (!string.IsNullOrWhiteSpace(selfPath) && processPath.Equals(selfPath, StringComparison.OrdinalIgnoreCase))
            return MenuSearchTargetKind.SelfTarget;
        if (excluded.Any(ex =>
                processPath.Contains(ex.Trim(), StringComparison.OrdinalIgnoreCase)
                || Path.GetFileNameWithoutExtension(processPath).Equals(ex.Trim(), StringComparison.OrdinalIgnoreCase)))
            return MenuSearchTargetKind.Excluded;
        return MenuSearchTargetKind.Searchable;
    }

    public static string EmptyMessage(MenuSearchTargetKind kind) => kind switch
    {
        MenuSearchTargetKind.Excluded => "This app is excluded",
        MenuSearchTargetKind.SelfTarget => "Tinycast has no menu bar to search",
        MenuSearchTargetKind.MenuLess => "This app has no menu bar",
        MenuSearchTargetKind.NoApplication => "No frontmost app to search",
        _ => "No menu items",
    };
}

public static class MenuSnapshotPolicy
{
    public const int MaxDepth = 20;
    public const int ItemLimit = 4000;
    public const int PerSubmenuLimit = 200;

    public static bool CanEnter(int depth, int count) => depth < MaxDepth && count < ItemLimit;

    public static IReadOnlyList<MenuSearchItem> Flatten(IEnumerable<MenuSearchItem> items, bool dropFirstTopLevel)
    {
        var eligible = items.Where(i => i.IsEligible).ToList();
        if (!dropFirstTopLevel || eligible.Count == 0)
            return eligible.Take(ItemLimit).ToList();
        var first = eligible[0].Section;
        return eligible.Where(i => i.Section != first).Take(ItemLimit).ToList();
    }
}

public static class MenuSearchQuery
{
    public const int Cap = 200;

    public static IReadOnlyList<MenuSearchItem> Filter(IEnumerable<MenuSearchItem> items, string query)
    {
        var list = items.Where(i => i.IsEligible).ToList();
        if (string.IsNullOrWhiteSpace(query))
            return list;
        var folded = FuzzyMatcher.Query.Parse(query);
        return list
            .Select(i =>
            {
                var title = FuzzyMatcher.Match(folded, i.LeafTitle);
                var path = FuzzyMatcher.Match(folded, i.SearchPath);
                var titleScore = title?.RawScore ?? -1;
                var pathScore = path is { IsLiteral: true } ? path.Value.RawScore / 4 : -1;
                return (Item: i, Score: Math.Max(titleScore, pathScore));
            })
            .Where(x => x.Score >= 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Item.Path, StringComparer.OrdinalIgnoreCase)
            .Take(Cap)
            .Select(x => x.Item)
            .ToList();
    }
}

public sealed record FileSearchPolicy(IReadOnlyList<string> Roots, FileSearchIgnoreList Ignore)
{
    public static FileSearchPolicy Resolve(
        IEnumerable<string> scopes,
        IEnumerable<string> extraIgnore,
        string home,
        Func<string, IReadOnlyList<string>>? expandHome = null)
    {
        var ignore = new FileSearchIgnoreList(FileSearchIgnoreList.Defaults.Concat(extraIgnore));
        var roots = new List<string>();
        foreach (var raw in scopes)
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0)
                continue;
            var expanded = ExpandUser(trimmed, home);
            if (IsHome(expanded, home))
            {
                foreach (var child in expandHome?.Invoke(home) ?? [])
                    roots.Add(child);
                continue;
            }

            roots.Add(expanded);
        }

        if (roots.Count == 0)
        {
            foreach (var child in expandHome?.Invoke(home) ?? [])
                roots.Add(child);
        }

        return new FileSearchPolicy(roots, ignore);
    }

    public static string ExpandUser(string path, string home)
    {
        if (path == "~")
            return home;
        if (path.StartsWith("~\\", StringComparison.Ordinal) || path.StartsWith("~/", StringComparison.Ordinal))
            return Path.Combine(home, path[2..]);
        return Environment.ExpandEnvironmentVariables(path);
    }

    public static bool IsHome(string path, string home) =>
        Path.GetFullPath(path).TrimEnd('\\', '/').Equals(Path.GetFullPath(home).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    public static string Abbreviate(string path, string home)
    {
        if (path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
            return "~" + path[home.Length..];
        return path;
    }
}

public static class FileSearchAqs
{
    public static string FileNameClause(string query)
    {
        var terms = FileSearchQuery.Terms(query);
        if (terms.Count == 0)
            return "";
        return string.Join(" AND ", terms.Select(t => "filename:" + Quote(t)));
    }

    static string Quote(string term)
    {
        var escaped = term.Replace("\"", "\"\"");
        return "\"" + escaped + "\"";
    }
}
