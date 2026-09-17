namespace Tinycast.Features.Launcher;

public sealed class FallbackSpec
{
    public string Id { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public static class FallbackCatalog
{
    public const string Ai = "fallback:ai";
    public const string Files = "fallback:files";
    public const string Shell = "fallback:shell";
    public const string Browser = "fallback:browser";

    public static IReadOnlyList<FallbackSpec> BuiltIns { get; } =
    [
        new() { Id = Ai, Enabled = true },
        new() { Id = Files, Enabled = true },
        new() { Id = Shell, Enabled = true },
    ];

    public static string Title(string id) => id switch
    {
        Ai => "AI Chat",
        Files => "Search Files",
        Shell => "Run Shell Command",
        Browser => "Open in Browser",
        _ => id.StartsWith("quicklink:", StringComparison.Ordinal) ? id : "Fallback",
    };

    public static List<FallbackSpec> Merge(IReadOnlyList<FallbackSpec> stored, IEnumerable<string> argumentQuicklinkIds)
    {
        var wanted = BuiltIns.Select(b => b.Id)
            .Concat(argumentQuicklinkIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var byId = stored
            .Where(s => !string.IsNullOrWhiteSpace(s.Id))
            .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);
        var result = new List<FallbackSpec>();
        foreach (var spec in stored)
        {
            if (!wanted.Contains(spec.Id, StringComparer.OrdinalIgnoreCase))
                continue;
            if (result.Any(r => r.Id.Equals(spec.Id, StringComparison.OrdinalIgnoreCase)))
                continue;
            result.Add(spec);
        }

        foreach (var id in wanted)
        {
            if (result.Any(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
                continue;
            result.Add(byId.TryGetValue(id, out var existing)
                ? existing
                : new FallbackSpec { Id = id, Enabled = true });
        }

        return result;
    }

    public static bool LooksLikeUrl(string query)
    {
        var text = query.Trim();
        if (text.Length < 4 || text.Contains(' '))
            return false;
        if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!text.Contains('.') || text.StartsWith('.'))
            return false;
        return Uri.TryCreate("https://" + text.TrimEnd('/'), UriKind.Absolute, out var uri)
            && uri.Host.Contains('.');
    }

    public static string BrowserTarget(string query)
    {
        var text = query.Trim();
        if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            return text;
        return "https://" + text;
    }
}
