using System.Text;
using System.Text.RegularExpressions;

namespace Relay.Features.FileSearch;

public enum FileSearchFilter
{
    All,
    Folders,
    Documents,
    Images,
    Audio,
    Video,
    Archives,
}

public static class FileSearchFilters
{
    public static IReadOnlyList<FileSearchFilter> All { get; } = Enum.GetValues<FileSearchFilter>();

    public static string Title(this FileSearchFilter filter) => filter switch
    {
        FileSearchFilter.Folders => "Folders",
        FileSearchFilter.Documents => "Documents",
        FileSearchFilter.Images => "Images",
        FileSearchFilter.Audio => "Audio",
        FileSearchFilter.Video => "Videos",
        FileSearchFilter.Archives => "Archives",
        _ => "All Types",
    };

    public static string EmptyMessage(this FileSearchFilter filter) => filter switch
    {
        FileSearchFilter.Folders => "No folders found",
        FileSearchFilter.Documents => "No documents found",
        FileSearchFilter.Images => "No images found",
        FileSearchFilter.Audio => "No audio found",
        FileSearchFilter.Video => "No videos found",
        FileSearchFilter.Archives => "No archives found",
        _ => "No files found",
    };

    public static FileSearchFilter Next(this FileSearchFilter filter)
    {
        var values = Enum.GetValues<FileSearchFilter>();
        var i = Array.IndexOf(values, filter);
        return values[(i + 1) % values.Length];
    }

    public static bool Accepts(this FileSearchFilter filter, string path, bool isDirectory)
    {
        if (filter == FileSearchFilter.All)
            return true;
        if (filter == FileSearchFilter.Folders)
            return isDirectory;
        if (isDirectory)
            return false;
        var ext = Path.GetExtension(path);
        return filter switch
        {
            FileSearchFilter.Documents => IsDocument(ext),
            FileSearchFilter.Images => IsImage(ext),
            FileSearchFilter.Audio => IsAudio(ext),
            FileSearchFilter.Video => IsVideo(ext),
            FileSearchFilter.Archives => IsArchive(ext),
            _ => true,
        };
    }

    static bool IsDocument(string ext) => ext.ToLowerInvariant() is
        ".txt" or ".md" or ".rtf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx"
        or ".pdf" or ".csv" or ".json" or ".xml" or ".html" or ".htm" or ".cs" or ".ts" or ".js"
        or ".py" or ".rs" or ".go" or ".java" or ".cpp" or ".h" or ".css";

    static bool IsImage(string ext) => ext.ToLowerInvariant() is
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".tif" or ".tiff" or ".svg" or ".ico" or ".heic";

    static bool IsAudio(string ext) => ext.ToLowerInvariant() is
        ".mp3" or ".wav" or ".flac" or ".aac" or ".m4a" or ".ogg" or ".wma" or ".aiff";

    static bool IsVideo(string ext) => ext.ToLowerInvariant() is
        ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".webm" or ".m4v";

    static bool IsArchive(string ext) => ext.ToLowerInvariant() is
        ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".bz2" or ".xz" or ".cab" or ".iso";
}

public sealed class FileSearchIgnoreList
{
    public static IReadOnlyList<string> Defaults { get; } =
        ["node_modules", "DerivedData", "build", "dist", "target", "Pods"];

    readonly HashSet<string> _literalNames;
    readonly List<string> _nameGlobs;
    readonly List<string> _pathGlobs;

    public FileSearchIgnoreList(IEnumerable<string> patterns)
    {
        _literalNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _nameGlobs = [];
        _pathGlobs = [];
        foreach (var raw in patterns)
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.Contains('\0'))
                continue;
            if (trimmed.Contains('/') || trimmed.Contains('\\'))
                _pathGlobs.Add(trimmed.Replace('\\', '/'));
            else if (trimmed.IndexOfAny(['*', '?', '[']) >= 0)
                _nameGlobs.Add(trimmed);
            else
                _literalNames.Add(trimmed);
        }
    }

    public bool Excludes(string path)
    {
        var normalized = path.Replace('\\', '/');
        foreach (var component in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (_literalNames.Contains(component))
                return true;
            if (_nameGlobs.Any(g => Glob(g, component)))
                return true;
        }

        return _pathGlobs.Any(g => Glob(g, normalized));
    }

    static bool Glob(string pattern, string candidate)
    {
        var rx = "^" + GlobToRegex(pattern) + "$";
        return Regex.IsMatch(candidate, rx, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    static string GlobToRegex(string pattern)
    {
        var rx = new StringBuilder(pattern.Length * 2);
        var i = 0;
        while (i < pattern.Length)
        {
            var c = pattern[i];
            if (c == '*')
            {
                rx.Append(".*");
                i++;
                continue;
            }

            if (c == '?')
            {
                rx.Append('.');
                i++;
                continue;
            }

            if (c == '[')
            {
                var close = CharClassClose(pattern, i);
                if (close < 0)
                {
                    rx.Append("\\[");
                    i++;
                    continue;
                }

                rx.Append('[');
                var n = i + 1;
                if (n < close && pattern[n] is '!' or '^')
                {
                    rx.Append('^');
                    n++;
                }

                while (n < close)
                {
                    if (pattern[n] == '\\')
                        rx.Append("\\\\");
                    else
                        rx.Append(pattern[n]);
                    n++;
                }

                rx.Append(']');
                i = close + 1;
                continue;
            }

            rx.Append(Regex.Escape(c.ToString()));
            i++;
        }

        return rx.ToString();
    }

    static int CharClassClose(string pattern, int open)
    {
        var i = open + 1;
        if (i < pattern.Length && pattern[i] is '!' or '^')
            i++;
        if (i < pattern.Length && pattern[i] == ']')
            i++;
        for (; i < pattern.Length; i++)
        {
            if (pattern[i] == ']')
                return i;
        }

        return -1;
    }
}

public sealed record FileSearchResult(
    string Path,
    string Name,
    DateTime? Modified,
    bool IsDirectory = false,
    bool IsVolume = false,
    long? Size = null)
{
    public string ParentName =>
        IsVolume ? "Volume" : System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(Path)?.TrimEnd('\\', '/') ?? "") ?? "";
}

public static class FileSearchQuery
{
    public const int SoftCap = 1000;
    public const int HardCap = 200;
    public const int RecentLimit = 20;
    public const int WalkMaxDepth = 32;
    public const int DebounceMs = 120;
    public const int LauncherCap = 8;

    public static IReadOnlyList<string> Terms(string query) =>
        query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    public static bool Matches(string fileName, string query)
    {
        var terms = Terms(query);
        if (terms.Count == 0)
            return true;
        return terms.All(term => fileName.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    public static bool SkipDescendName(string name) =>
        name.Equals("AppData", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Application Data", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Windows", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Program Files", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Program Files (x86)", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ProgramData", StringComparison.OrdinalIgnoreCase)
        || name.Equals("WindowsApps", StringComparison.OrdinalIgnoreCase)
        || name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)
        || name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Recycle.Bin", StringComparison.OrdinalIgnoreCase);

    public static bool IsExcludedPath(string path, FileSearchIgnoreList ignore)
    {
        var normalized = path.Replace('\\', '/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var component = parts[i];
            if (component.Length > 1 && component.StartsWith('.') && component is not "." and not "..")
                return true;
            if (component.StartsWith('$'))
                return true;
            if (i < parts.Length - 1 && SkipDescendName(component))
                return true;
        }

        return ignore.Excludes(path);
    }

    public static IReadOnlyList<FileSearchResult> Rank(
        IEnumerable<FileSearchResult> results, string query, FileSearchIgnoreList ignore, FileSearchFilter filter)
    {
        var terms = Terms(query);
        var whole = Features.Launcher.FuzzyMatcher.Query.Parse(query);
        var folded = terms.Select(Features.Launcher.FuzzyMatcher.Query.Parse).ToList();
        return results
            .Where(r => !IsExcludedPath(r.Path, ignore) && filter.Accepts(r.Path, r.IsDirectory))
            .Select(r =>
            {
                var full = string.IsNullOrWhiteSpace(query) ? 0 : Features.Launcher.FuzzyMatcher.Match(whole, r.Name)?.RawScore ?? -1;
                var term = folded.Sum(t => Features.Launcher.FuzzyMatcher.Match(t, r.Name)?.RawScore ?? 0);
                return (Result: r, Full: full, Term: term);
            })
            .Where(x => terms.Count == 0 || x.Full >= 0 || x.Term > 0)
            .OrderByDescending(x => x.Result.IsDirectory)
            .ThenByDescending(x => x.Full)
            .ThenByDescending(x => x.Term)
            .ThenBy(x => x.Result.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Result.Path, StringComparer.OrdinalIgnoreCase)
            .Take(HardCap)
            .Select(x => x.Result)
            .ToList();
    }
}

public static class FileBrowse
{
    public static bool IsDriveQuery(string query)
    {
        var t = query.Trim().TrimEnd('\\', '/', ':');
        return t.Length == 1 && char.IsLetter(t[0]);
    }

    public static bool IsVolumeQuery(string query)
    {
        var t = query.Trim();
        if (IsDriveQuery(t))
            return true;
        var trimmed = t.TrimEnd('\\', '/');
        return trimmed.Length == 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':';
    }

    public static string? DriveRoot(string query)
    {
        var t = query.Trim();
        if (t.Length >= 2 && char.IsLetter(t[0]) && t[1] == ':')
            return char.ToUpperInvariant(t[0]) + @":\";
        if (IsDriveQuery(t))
            return char.ToUpperInvariant(t.Trim().TrimEnd('\\', '/', ':')[0]) + @":\";
        return null;
    }

    public static string? ParentPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        var full = path.TrimEnd('\\', '/');
        if (full.Length == 2 && full[1] == ':')
            return null;
        var parent = Path.GetDirectoryName(full + (full.EndsWith(':') ? "\\" : ""));
        if (string.IsNullOrEmpty(parent))
            return null;
        if (parent.Length == 2 && parent[1] == ':')
            return parent + "\\";
        return parent;
    }

    public static IReadOnlyList<FileSearchResult> FilterVolumes(
        IEnumerable<FileSearchResult> volumes, string query)
    {
        var q = query.Trim();
        if (q.Length == 0)
            return volumes.ToList();
        var root = DriveRoot(q);
        if (root is not null)
            return volumes.Where(v => v.Path.StartsWith(root, StringComparison.OrdinalIgnoreCase)).ToList();
        return volumes
            .Where(v => v.Name.StartsWith(q, StringComparison.OrdinalIgnoreCase)
                        || v.Path.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}

public sealed record WindowSwitchEntry(nint Hwnd, string Title, string ProcessName, bool Minimized, string? Path = null);

public static class WindowSwitchQuery
{
    public static IReadOnlyList<WindowSwitchEntry> Filter(IEnumerable<WindowSwitchEntry> entries, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return entries.ToList();
        var q = Features.Launcher.FuzzyMatcher.Query.Parse(query);
        return entries.Where(e =>
                Features.Launcher.FuzzyMatcher.Match(q, e.Title) is not null
                || Features.Launcher.FuzzyMatcher.Match(q, e.ProcessName) is not null)
            .ToList();
    }
}

