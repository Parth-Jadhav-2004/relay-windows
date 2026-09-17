using Tinycast.Features.FileSearch;

namespace Tinycast.Platform;

internal static class FileSearchService
{
    public static FileSearchIgnoreList Compile(IEnumerable<string> extraPatterns) =>
        new(FileSearchIgnoreList.Defaults.Concat(extraPatterns));

    public static IReadOnlyList<FileSearchResult> Volumes()
    {
        var rows = new List<FileSearchResult>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable or DriveType.Network or DriveType.CDRom))
                continue;
            var letter = drive.Name.TrimEnd('\\');
            string? label = null;
            try
            {
                if (drive.IsReady)
                    label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? null : drive.VolumeLabel;
            }
            catch (Exception ex)
            {
                Log.Write("volume label: " + ex.Message);
            }

            rows.Add(new FileSearchResult(
                drive.Name,
                label is null ? letter : letter + " · " + label,
                null,
                true,
                true));
        }

        return rows;
    }

    public static IReadOnlyList<FileSearchResult> Children(
        string path, string query, FileSearchIgnoreList ignore, FileSearchFilter filter)
    {
        var results = new List<FileSearchResult>();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return results;
        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(path))
            {
                if (FileSearchQuery.IsExcludedPath(entry, ignore))
                    continue;
                bool isDir;
                try { isDir = Directory.Exists(entry) && File.GetAttributes(entry).HasFlag(FileAttributes.Directory); }
                catch (Exception ex)
                {
                    Log.Write("file entry: " + ex.Message);
                    continue;
                }

                if ((File.GetAttributes(entry) & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                    continue;
                if (!filter.Accepts(entry, isDir))
                    continue;
                var name = Path.GetFileName(entry);
                if (query.Length > 0 && !FileSearchQuery.Matches(name, query)
                    && Features.Launcher.FuzzyMatcher.Match(query, name) is null)
                    continue;
                DateTime? modified = null;
                long? size = null;
                try
                {
                    modified = isDir ? Directory.GetLastWriteTime(entry) : File.GetLastWriteTime(entry);
                    if (!isDir)
                        size = new FileInfo(entry).Length;
                }
                catch (Exception) { }

                results.Add(new FileSearchResult(entry, name, modified, isDir, false, size));
                if (results.Count >= FileSearchQuery.SoftCap)
                    break;
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        return FileSearchQuery.Rank(results, query, ignore, FileSearchFilter.All);
    }

    public static IReadOnlyList<FileSearchResult> Recents(IEnumerable<string> scopes, FileSearchIgnoreList ignore, FileSearchFilter filter)
    {
        var roots = scopes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (WindowsSearch.TryRecents(roots, ignore, filter, out var indexed))
            return indexed;

        var cutoffChanged = DateTime.Now.AddDays(-3);
        var cutoffUsed = DateTime.Now.AddDays(-30);
        var found = new List<FileSearchResult>();
        foreach (var root in roots.Take(8))
        {
            if (!Directory.Exists(root))
                continue;
            CollectRecents(root, 0, cutoffChanged, cutoffUsed, ignore, filter, found);
            if (found.Count >= FileSearchQuery.SoftCap)
                break;
        }

        return found
            .OrderByDescending(r => r.Modified ?? DateTime.MinValue)
            .Take(FileSearchQuery.RecentLimit)
            .ToList();
    }

    public static IReadOnlyList<FileSearchResult> Search(
        string query,
        IEnumerable<string> roots,
        FileSearchIgnoreList ignore,
        FileSearchFilter filter,
        CancellationToken token = default)
    {
        var scoped = roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (WindowsSearch.TrySearch(query, scoped, ignore, filter, FileSearchQuery.HardCap, out var indexed))
            return FileSearchQuery.Rank(indexed, query, ignore, filter);

        var results = new List<FileSearchResult>();
        foreach (var root in scoped)
        {
            if (token.IsCancellationRequested)
                break;
            if (!Directory.Exists(root))
                continue;
            Walk(root, 0, query, ignore, filter, results, token);
            if (results.Count >= FileSearchQuery.SoftCap)
                break;
        }

        if (token.IsCancellationRequested)
            return [];
        return FileSearchQuery.Rank(results, query, ignore, filter);
    }

    static void CollectRecents(
        string dir, int depth, DateTime changed, DateTime used, FileSearchIgnoreList ignore, FileSearchFilter filter, List<FileSearchResult> results)
    {
        if (depth > 2 || results.Count >= FileSearchQuery.SoftCap)
            return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                if (FileSearchQuery.IsExcludedPath(file, ignore) || !filter.Accepts(file, false))
                    continue;
                DateTime modified;
                try { modified = File.GetLastWriteTime(file); }
                catch (Exception) { continue; }
                if (modified < used)
                    continue;
                results.Add(new FileSearchResult(file, Path.GetFileName(file), modified, false));
            }

            foreach (var child in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(child);
                if (name.Equals("AppData", StringComparison.OrdinalIgnoreCase) || FileSearchQuery.IsExcludedPath(child, ignore))
                    continue;
                DateTime modified;
                try { modified = Directory.GetLastWriteTime(child); }
                catch (Exception) { continue; }
                if (modified >= changed && filter.Accepts(child, true))
                    results.Add(new FileSearchResult(child, name, modified, true));
                CollectRecents(child, depth + 1, changed, used, ignore, filter, results);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    static void Walk(
        string dir, int depth, string query, FileSearchIgnoreList ignore, FileSearchFilter filter,
        List<FileSearchResult> results, CancellationToken token)
    {
        if (token.IsCancellationRequested || depth > 5 || results.Count >= FileSearchQuery.SoftCap)
            return;
        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
            {
                if (token.IsCancellationRequested || results.Count >= FileSearchQuery.SoftCap)
                    return;
                if (FileSearchQuery.IsExcludedPath(entry, ignore))
                    continue;
                bool isDir;
                try { isDir = (File.GetAttributes(entry) & FileAttributes.Directory) != 0; }
                catch (Exception) { continue; }

                var name = Path.GetFileName(entry);
                try
                {
                    if ((File.GetAttributes(entry) & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                        continue;
                }
                catch (Exception) { continue; }

                if (FileSearchQuery.Matches(name, query) && filter.Accepts(entry, isDir))
                {
                    DateTime? modified = null;
                    try { modified = isDir ? Directory.GetLastWriteTime(entry) : File.GetLastWriteTime(entry); }
                    catch (Exception) { }
                    results.Add(new FileSearchResult(entry, name, modified, isDir));
                }

                if (isDir && depth < 5 && !SkipDescend(name))
                    Walk(entry, depth + 1, query, ignore, filter, results, token);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    static bool SkipDescend(string name) =>
        name.Equals("AppData", StringComparison.OrdinalIgnoreCase)
        || name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)
        || name.Equals("WindowsApps", StringComparison.OrdinalIgnoreCase);

    public static void Reveal(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "/select,\"" + path + "\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Write("reveal: " + ex.Message);
            ProcessLauncher.Open(path);
        }
    }
}
