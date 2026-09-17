using System.Text.Json;

namespace Tinycast.Features.Launcher;

public sealed record AppEntry(
    string Id,
    string Title,
    AppEntryKind Kind,
    string? Subtitle,
    string? Path,
    string? Glyph,
    SearchFields Fields,
    string? IconPath = null);

public sealed class LauncherRankingRecord
{
    public string ItemKey { get; set; } = "";
    public string SubmittedQuery { get; set; } = "";
    public int Count { get; set; }
    public DateTime LastUsed { get; set; }
}

public sealed class LauncherRankingStore
{
    public const int Cap = 1_000;
    public const int QueryLimit = 64;
    public static int MaximumUsage => SearchRelevance.UsageCeiling - 1;

    readonly string _filePath;
    readonly Func<DateTime> _now;
    public List<LauncherRankingRecord> Records { get; private set; }
    public int Revision { get; private set; }

    public LauncherRankingStore(string filePath, Func<DateTime>? now = null)
    {
        _filePath = filePath;
        _now = now ?? (() => DateTime.UtcNow);
        Records = Load(filePath);
    }

    public void Record(string itemKey, string query)
    {
        query = Normalize(query);
        if (string.IsNullOrEmpty(itemKey) || string.IsNullOrEmpty(query) || query.Length > QueryLimit)
            return;
        var timestamp = _now();
        var existing = Records.Find(r => r.ItemKey == itemKey && r.SubmittedQuery == query);
        if (existing is not null)
        {
            existing.Count = (int)Math.Min(int.MaxValue, (long)existing.Count + 1);
            existing.LastUsed = timestamp;
        }
        else
        {
            Records.Add(new LauncherRankingRecord
            {
                ItemKey = itemKey,
                SubmittedQuery = query,
                Count = 1,
                LastUsed = timestamp,
            });
        }

        if (Records.Count > Cap)
        {
            Records = Records
                .OrderByDescending(r => r.Count)
                .ThenByDescending(r => r.LastUsed)
                .Take(Cap)
                .ToList();
        }

        Persist();
    }

    public Dictionary<string, int> Usage(string query)
    {
        query = Normalize(query);
        if (query.Length == 0)
            return [];
        var totals = new Dictionary<string, (long Count, DateTime LastUsed)>();
        foreach (var record in Records)
        {
            if (!record.SubmittedQuery.StartsWith(query, StringComparison.Ordinal))
                continue;
            if (totals.TryGetValue(record.ItemKey, out var running))
            {
                totals[record.ItemKey] = (running.Count + record.Count,
                    record.LastUsed > running.LastUsed ? record.LastUsed : running.LastUsed);
            }
            else
            {
                totals[record.ItemKey] = (record.Count, record.LastUsed);
            }
        }

        if (totals.Count == 0)
            return [];
        var bucket = 0L;
        foreach (var value in totals.Values)
            bucket += value.Count;
        if (bucket <= 0)
            return [];
        var timestamp = _now();
        return totals.ToDictionary(
            kv => kv.Key,
            kv => Score(kv.Value.Count, kv.Value.LastUsed, kv.Value.Count / (double)bucket, timestamp));
    }

    public static int Score(long count, DateTime lastUsed, double share, DateTime timestamp)
    {
        var ageInDays = Math.Max(0, (timestamp - lastUsed).TotalDays);
        var frequency = 2_000 * (1 - Math.Pow(count + 1.0, -0.30));
        var recency = 700 * Math.Exp(-ageInDays / 14);
        var confidence = 300 * share * Math.Min(1, count / 3.0);
        return Math.Min(MaximumUsage, (int)Math.Round(frequency + recency + confidence));
    }

    public void Reset(string itemKey)
    {
        var before = Records.Count;
        Records.RemoveAll(r => r.ItemKey == itemKey);
        if (Records.Count != before)
            Persist();
    }

    public void ResetAll()
    {
        if (Records.Count == 0)
            return;
        Records = [];
        Persist();
    }

    public void ReplaceAll(IEnumerable<LauncherRankingRecord?>? records)
    {
        Records = Sanitize(records);
        Persist();
    }

    static List<LauncherRankingRecord> Sanitize(IEnumerable<LauncherRankingRecord?>? records) => (records ?? [])
        .Select(Validated)
        .OfType<LauncherRankingRecord>()
        .OrderByDescending(r => r.Count)
        .ThenByDescending(r => r.LastUsed)
        .Take(Cap)
        .ToList();

    static LauncherRankingRecord? Validated(LauncherRankingRecord? record)
    {
        if (record is null || string.IsNullOrWhiteSpace(record.ItemKey)
            || record.SubmittedQuery is null || record.Count <= 0)
            return null;
        var query = Normalize(record.SubmittedQuery);
        if (query.Length == 0 || query.Length > QueryLimit)
            return null;
        return new LauncherRankingRecord
        {
            ItemKey = record.ItemKey,
            SubmittedQuery = query,
            Count = record.Count,
            LastUsed = record.LastUsed,
        };
    }

    public static string Normalize(string query) =>
        FuzzyMatcher.Normalized(query.Trim());

    void Persist()
    {
        Revision++;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(Records));
    }

    static List<LauncherRankingRecord> Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return [];
            return Sanitize(JsonSerializer.Deserialize<List<LauncherRankingRecord?>>(File.ReadAllText(path)));
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }
}

public class JsonKeyedStore<T>
{
    readonly string _path;
    public Dictionary<string, T> Items { get; private set; }

    public JsonKeyedStore(string path)
    {
        _path = path;
        Items = Load();
    }

    public T? Get(string key) => Items.TryGetValue(key, out var value) ? value : default;

    public void Set(string key, T value)
    {
        Items[key] = value;
        Persist();
    }

    public bool Remove(string key)
    {
        if (!Items.Remove(key))
            return false;
        Persist();
        return true;
    }

    Dictionary<string, T> Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, T>>(File.ReadAllText(_path));
            var items = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in loaded ?? [])
            {
                if (value is not null)
                    items[key] = value;
            }
            return items;
        }
        catch (Exception)
        {
            return new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        }
    }

    void Persist()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(Items));
    }
}

public sealed class FavoritesStore
{
    readonly string _path;
    readonly List<string> _ids = [];

    public FavoritesStore(string path)
    {
        _path = path;
        Load();
    }

    public bool IsFavorite(string id) =>
        _ids.Any(existing => existing.Equals(id, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<string> OrderedIds() => _ids.ToList();

    public string? At(int index) => index >= 0 && index < _ids.Count ? _ids[index] : null;

    public void Toggle(string id)
    {
        var index = _ids.FindIndex(existing => existing.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            _ids.RemoveAt(index);
        else
            _ids.Add(id);
        Persist();
    }

    public bool Move(string id, int delta)
    {
        var index = _ids.FindIndex(existing => existing.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return false;
        var target = index + delta;
        if (target < 0 || target >= _ids.Count)
            return false;
        (_ids[index], _ids[target]) = (_ids[target], _ids[index]);
        Persist();
        return true;
    }

    void Load()
    {
        try
        {
            if (!File.Exists(_path))
                return;
            var json = File.ReadAllText(_path);
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(json);
                if (list is not null && (list.Count == 0 || list[0] is not null) && !json.TrimStart().StartsWith('{'))
                {
                    foreach (var id in list.Where(id => !string.IsNullOrWhiteSpace(id)))
                    {
                        if (!_ids.Contains(id, StringComparer.OrdinalIgnoreCase))
                            _ids.Add(id);
                    }
                    return;
                }
            }
            catch (JsonException)
            {
            }

            var dict = JsonSerializer.Deserialize<Dictionary<string, bool>>(json);
            if (dict is null)
                return;
            foreach (var (key, value) in dict)
            {
                if (value && !_ids.Contains(key, StringComparer.OrdinalIgnoreCase))
                    _ids.Add(key);
            }
        }
        catch (Exception)
        {
        }
    }

    void Persist()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_ids));
    }
}

public sealed class AliasStore : JsonKeyedStore<string>
{
    public AliasStore(string path) : base(path) { }
}

public sealed class VisibilityStore : JsonKeyedStore<bool>
{
    public VisibilityStore(string path) : base(path) { }

    public bool IsHidden(string id) => Items.TryGetValue(id, out var value) && value;

    public bool IsKindEnabled(AppSettings settings, AppEntryKind kind) => kind switch
    {
        AppEntryKind.Application => true,
        AppEntryKind.SystemSettings => true,
        AppEntryKind.SystemAction => true,
        AppEntryKind.Command => true,
        AppEntryKind.Favorite => true,
        AppEntryKind.Quicklink => settings.QuicklinksEnabled,
        AppEntryKind.Snippet => settings.SnippetsEnabled && settings.SnippetsShowInLauncher,
        AppEntryKind.CustomCommand => settings.CustomCommandsEnabled && settings.CustomCommandsShowInLauncher,
        _ => true,
    };
}

public static class SearchScopes
{
    public static IReadOnlyList<string> DefaultWindows()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
        var userStart = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return
        [
            Path.Combine(programData, "Programs"),
            Path.Combine(userStart, "Programs"),
            pf,
            pf86,
            Path.Combine(local, "Microsoft", "WindowsApps"),
        ];
    }

    public static string Abbreviate(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
            return "~" + path[home.Length..];
        return path;
    }
}

public static class LauncherOrder
{
    public static IReadOnlyList<AppEntry> Rank(
        IEnumerable<AppEntry> entries,
        string query,
        IReadOnlyDictionary<string, int> usage,
        IReadOnlyDictionary<string, string> aliases,
        Func<string, bool> hidden,
        AppSettings settings,
        VisibilityStore visibility)
    {
        var folded = FuzzyMatcher.Query.Parse(query);
        var scored = new List<(AppEntry Entry, int Score)>();
        foreach (var entry in entries)
        {
            if (hidden(entry.Id) || !visibility.IsKindEnabled(settings, entry.Kind))
                continue;
            var fields = entry.Fields;
            if (aliases.TryGetValue(entry.Id, out var alias) && alias.Length > 0)
            {
                var withAlias = new SearchFields();
                foreach (var existing in entry.Fields.Aliases)
                    withAlias.Add(existing);
                withAlias.Add(SearchAlias.User(alias));
                fields = withAlias;
            }
            var quality = SearchRelevance.Quality(folded, fields);
            if (quality is null)
                continue;
            usage.TryGetValue(entry.Id, out var used);
            scored.Add((entry, SearchRelevance.Total(quality.Value, used)));
        }

        return scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Entry.Title, StringComparer.OrdinalIgnoreCase)
            .Select(s => s.Entry)
            .ToList();
    }
}
