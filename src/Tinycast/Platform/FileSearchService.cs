using System.Runtime.InteropServices;
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
        CancellationToken token = default,
        bool wholeCatalog = false)
    {
        var scoped = roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var indexedScopes = wholeCatalog ? [] : scoped;
        if (WindowsSearch.TrySearch(query, indexedScopes, ignore, filter, FileSearchQuery.HardCap, out var indexed)
            && indexed.Count > 0)
            return FileSearchQuery.Rank(indexed, query, ignore, filter);

        var results = new List<FileSearchResult>();
        var walkRoots = wholeCatalog
            ? LibraryRoots(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).Concat(scoped).Distinct(StringComparer.OrdinalIgnoreCase)
            : scoped;
        foreach (var root in walkRoots)
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

    public static IReadOnlyList<string> LibraryRoots(string home)
    {
        var folders = new List<string>();
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;
            if (folders.Any(existing => existing.Equals(path, StringComparison.OrdinalIgnoreCase)))
                return;
            folders.Add(path);
        }

        Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Add(KnownDownloads(home));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
        Add(Path.Combine(home, "OneDrive"));
        Add(Path.Combine(home, "Downloads"));
        return folders;
    }

    static string? KnownDownloads(string home)
    {
        try
        {
            var id = new Guid("374DE290-123F-4565-9164-39C4925E467B");
            var result = NativeMethods.SHGetKnownFolderPath(id, 0, IntPtr.Zero, out var ptr);
            if (result != 0 || ptr == IntPtr.Zero)
                return Path.Combine(home, "Downloads");
            var path = Marshal.PtrToStringUni(ptr);
            Marshal.FreeCoTaskMem(ptr);
            return path;
        }
        catch (Exception)
        {
            return Path.Combine(home, "Downloads");
        }
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
                if (FileSearchQuery.SkipDescendName(name) || FileSearchQuery.IsExcludedPath(child, ignore))
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
        if (token.IsCancellationRequested || depth > FileSearchQuery.WalkMaxDepth || results.Count >= FileSearchQuery.SoftCap)
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

                if (isDir && depth < FileSearchQuery.WalkMaxDepth && !FileSearchQuery.SkipDescendName(name))
                    Walk(entry, depth + 1, query, ignore, filter, results, token);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

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
