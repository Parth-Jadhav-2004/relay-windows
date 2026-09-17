using Tinycast.Features.Launcher;

namespace Tinycast.Features.Emoji;

public sealed record EmojiItem(string Glyph, string Name, string Group, IReadOnlyList<string> Keywords, bool CanTint = false);

public static partial class EmojiCatalog
{
    public static IReadOnlyList<string> Groups { get; } =
        ["Pinned", "Frequent", "Smileys", "People", "Nature", "Food", "Activities", "Travel", "Objects", "Symbols", "Flags"];

    public static IReadOnlyList<EmojiItem> Search(
        string query,
        int skinTone = 0,
        IReadOnlyList<string>? pins = null,
        IReadOnlyList<string>? frequent = null,
        string? group = null) =>
        EmojiSearch.Rank(All, query, skinTone, pins ?? [], frequent ?? [], group);

    public static EmojiItem? Find(string glyph) =>
        All.FirstOrDefault(e => e.Glyph == glyph || EmojiSkin.Strip(e.Glyph) == EmojiSkin.Strip(glyph));
}

public static class EmojiSearch
{
    public const int Cap = 200;

    public static string Unwrap(string query)
    {
        var t = query.Trim();
        if (t.Length >= 2 && t[0] == ':' && t[^1] == ':')
            return t[1..^1].Trim();
        return t;
    }

    public static IReadOnlyList<EmojiItem> Rank(
        IReadOnlyList<EmojiItem> catalog,
        string query,
        int skinTone,
        IReadOnlyList<string> pins,
        IReadOnlyList<string> frequent,
        string? group)
    {
        var unwrapped = Unwrap(query);
        var terms = unwrapped.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var usage = UsageBonus(frequent);
        IEnumerable<EmojiItem> source = catalog;
        if (!string.IsNullOrWhiteSpace(group) && group is not ("Pinned" or "Frequent"))
            source = catalog.Where(e => e.Group.Equals(group, StringComparison.OrdinalIgnoreCase));

        var ranked = source
            .Select(e => (Item: e, Score: Score(e, unwrapped, terms, usage)))
            .Where(x => terms.Length == 0 || x.Score >= 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Cap)
            .Select(x => Tint(x.Item, skinTone))
            .ToList();

        if (terms.Length > 0)
            return ranked;

        var byGlyph = catalog.ToDictionary(e => e.Glyph, StringComparer.Ordinal);
        var leading = new List<EmojiItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(group) || group == "Pinned")
        {
            foreach (var glyph in pins)
            {
                if (!byGlyph.TryGetValue(EmojiSkin.Strip(glyph), out var item) && !byGlyph.TryGetValue(glyph, out item))
                    continue;
                if (!seen.Add(item.Glyph))
                    continue;
                leading.Add(Tint(item, skinTone));
            }
        }

        if (string.IsNullOrWhiteSpace(group) || group == "Frequent")
        {
            foreach (var glyph in frequent.Take(30))
            {
                if (!byGlyph.TryGetValue(EmojiSkin.Strip(glyph), out var item) && !byGlyph.TryGetValue(glyph, out item))
                    continue;
                if (!seen.Add(item.Glyph))
                    continue;
                leading.Add(Tint(item, skinTone));
            }
        }

        if (!string.IsNullOrWhiteSpace(group) && group is "Pinned" or "Frequent")
            return leading;

        foreach (var item in ranked)
        {
            if (seen.Add(item.Glyph))
                leading.Add(item);
        }

        return leading;
    }

    static EmojiItem Tint(EmojiItem item, int skinTone)
    {
        if (skinTone <= 0 || !item.CanTint)
            return item;
        return item with { Glyph = EmojiSkin.Apply(item.Glyph, skinTone) };
    }

    static Dictionary<string, int> UsageBonus(IReadOnlyList<string> frequent)
    {
        var bonus = new Dictionary<string, int>(StringComparer.Ordinal);
        var rank = 100;
        foreach (var glyph in frequent.Take(100))
        {
            bonus[EmojiSkin.Strip(glyph)] = rank;
            rank = Math.Max(1, rank - 1);
        }

        return bonus;
    }

    static int Score(EmojiItem item, string query, string[] terms, IReadOnlyDictionary<string, int> usage)
    {
        usage.TryGetValue(item.Glyph, out var bonus);
        if (terms.Length == 0)
            return bonus;

        var name = FuzzyMatcher.Normalized(item.Name);
        var foldedQuery = FuzzyMatcher.Normalized(query);
        if (name == foldedQuery)
            return 400_000 + bonus;

        if (NameWords(item.Name).Any(w => w == foldedQuery))
            return 350_000 + bonus;

        if (item.Keywords.Any(k => FuzzyMatcher.Normalized(k) == foldedQuery))
            return 300_000 + bonus;

        if (name.StartsWith(foldedQuery, StringComparison.Ordinal))
            return 250_000 + bonus;

        if (NameWords(item.Name).Any(w => w.StartsWith(foldedQuery, StringComparison.Ordinal)))
            return 200_000 + bonus;

        if (item.Keywords.Any(k => KeywordStarts(k, foldedQuery)))
            return 150_000 + bonus;

        if (terms.Length > 1 && terms.All(t => WordStartsSomewhere(item, t)))
            return 120_000 + bonus;

        var fuzzy = FuzzyMatcher.Match(foldedQuery, item.Name);
        if (fuzzy is { IsLiteral: true })
            return 80_000 + fuzzy.Value.RawScore + bonus;

        return -1;
    }

    static IEnumerable<string> NameWords(string name) =>
        name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(FuzzyMatcher.Normalized);

    static bool KeywordStarts(string keyword, string query)
    {
        var folded = FuzzyMatcher.Normalized(keyword);
        if (folded.StartsWith(query, StringComparison.Ordinal))
            return true;
        return folded.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Any(w => w.StartsWith(query, StringComparison.Ordinal));
    }

    static bool WordStartsSomewhere(EmojiItem item, string term)
    {
        var folded = FuzzyMatcher.Normalized(term);
        if (NameWords(item.Name).Any(w => w.StartsWith(folded, StringComparison.Ordinal)))
            return true;
        return item.Keywords.Any(k => KeywordStarts(k, folded));
    }
}

public static class EmojiGridGeometry
{
    public const int MinColumns = 6;
    public const int MaxColumns = 10;
    public const int DefaultColumns = 8;

    public static int ClampColumns(int columns) => Math.Clamp(columns, MinColumns, MaxColumns);

    public static int ZoomIn(int columns) => ClampColumns(columns - 1);

    public static int ZoomOut(int columns) => ClampColumns(columns + 1);

    public static int RowOf(int index, int columns) => columns <= 0 ? 0 : index / ClampColumns(columns);

    public static int ColumnOf(int index, int columns) => columns <= 0 ? 0 : index % ClampColumns(columns);

    public static int IndexAt(int row, int column, int columns, int count)
    {
        var cols = ClampColumns(columns);
        var index = row * cols + column;
        return index < 0 || index >= count ? -1 : index;
    }
}

public static class EmojiPinOrder
{
    public static IReadOnlyList<string> Toggle(IReadOnlyList<string> pins, string glyph)
    {
        var stripped = EmojiSkin.Strip(glyph);
        var next = pins.Where(p => EmojiSkin.Strip(p) != stripped).ToList();
        if (next.Count == pins.Count)
            next.Add(stripped);
        return next;
    }

    public static IReadOnlyList<string> Move(IReadOnlyList<string> pins, string glyph, int delta)
    {
        var stripped = EmojiSkin.Strip(glyph);
        var next = pins.ToList();
        var index = next.FindIndex(p => EmojiSkin.Strip(p) == stripped);
        if (index < 0)
            return pins;
        var target = index + delta;
        if (target < 0 || target >= next.Count)
            return pins;
        (next[index], next[target]) = (next[target], next[index]);
        return next;
    }

    public static int CountShown(IReadOnlyList<string> pins, IReadOnlySet<string> catalogGlyphs) =>
        pins.Count(p => catalogGlyphs.Contains(EmojiSkin.Strip(p)));
}

public static class FrequentEmoji
{
    public static IReadOnlyList<string> Record(IReadOnlyList<string> current, string glyph, int cap = 100)
    {
        var stripped = EmojiSkin.Strip(glyph);
        var next = current.Where(g => EmojiSkin.Strip(g) != stripped).ToList();
        next.Insert(0, stripped);
        if (next.Count > cap)
            next.RemoveRange(cap, next.Count - cap);
        return next;
    }
}
