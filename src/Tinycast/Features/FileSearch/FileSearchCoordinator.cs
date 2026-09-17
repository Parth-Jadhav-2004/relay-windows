using System.Threading;
using Tinycast.Features.FileSearch;
using Tinycast.Palette;
using Tinycast.Platform;

namespace Tinycast;

public sealed class FileSearchCoordinator
{
    readonly AppCore _core;
    string? _root;
    FileSearchFilter _filter = FileSearchFilter.All;
    int _generation;
    string _searchQuery = "";
    IReadOnlyList<PaletteRow> _searchRows = [];
    bool _searching;
    CancellationTokenSource? _cts;

    public FileSearchCoordinator(AppCore core) => _core = core;

    public FileSearchFilter Filter => _filter;
    public string? Root => _root;
    public bool IsBrowsing => _root is not null;

    public string Placeholder =>
        _root is null ? "Search files and folders" : _root;

    public void Reset()
    {
        _root = null;
        _filter = FileSearchFilter.All;
        CancelSearch();
        _searchQuery = "";
        _searchRows = [];
        _searching = false;
    }

    public void CycleFilter()
    {
        _filter = _filter.Next();
        CancelSearch();
        _searchQuery = "";
        _core.Palette.Selection = 0;
        _core.Palette.Notify();
    }

    public bool HandleEscape()
    {
        if (_core.Palette.Query.Length > 0)
        {
            _core.Palette.Query = "";
            _core.Palette.Selection = 0;
            _core.Palette.Notify();
            return true;
        }

        return Pop();
    }

    public bool HandleBackspace()
    {
        if (_core.Palette.Query.Length > 0)
            return false;
        return Pop();
    }

    public bool Pop()
    {
        if (_root is null)
            return false;
        _root = FileBrowse.ParentPath(_root);
        CancelSearch();
        _core.Palette.Query = "";
        _core.Palette.Selection = 0;
        _core.Palette.Notify();
        return true;
    }

    public IReadOnlyList<PaletteRow> Rows(string query)
    {
        if (!_core.Settings.FileSearchEnabled)
            return [Hint("file-off", "File search is off", "Enable it in Settings → File Search", "\uE721")];

        var policy = FileSearchPolicy.Resolve(
            _core.Settings.FileSearchScopes,
            _core.Settings.FileSearchIgnorePatterns,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            HomeChildren);
        var ignore = policy.Ignore;
        if (LooksLikePath(query) is { } existing)
            return [ToRow(existing, existing.IsDirectory ? "Go to folder" : "Open")];

        if (_root is null)
            return RootRows(query, ignore, policy.Roots);
        return BrowseRows(query, ignore);
    }

    public IReadOnlyList<PaletteRow> LiveRows(string query)
    {
        if (!_core.Settings.FileSearchEnabled)
            return [];
        var trimmed = query.Trim();
        if (trimmed.Length < 2 || FileBrowse.IsVolumeQuery(trimmed))
            return [];
        if (LooksLikePath(trimmed) is { } existing)
            return [ToRow(existing, "Files")];

        var policy = FileSearchPolicy.Resolve(
            _core.Settings.FileSearchScopes,
            _core.Settings.FileSearchIgnorePatterns,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            HomeChildren);
        return SearchRows(trimmed, policy.Ignore, [], policy.Roots, wholeCatalog: true)
            .Where(r => r.Id.StartsWith("fs:", StringComparison.Ordinal) && !r.Id.StartsWith("fs:vol:", StringComparison.Ordinal))
            .Select(r => r with { Section = "Files" })
            .Take(FileSearchQuery.LauncherCap)
            .ToList();
    }

    IReadOnlyList<PaletteRow> RootRows(string query, FileSearchIgnoreList ignore, IReadOnlyList<string> scopes)
    {
        var rows = new List<PaletteRow>();
        var volumes = FileBrowse.FilterVolumes(FileSearchService.Volumes(), query);
        foreach (var volume in volumes)
            rows.Add(ToRow(volume, "Volumes"));

        if (FileBrowse.IsVolumeQuery(query))
            return rows.Count > 0 ? rows : [Hint("file-empty", "No volume matches", "Try C, D, or a full path", "\uE7C4")];

        if (query.Trim().Length == 0)
        {
            var recents = scopes.Count == 0 ? [] : FileSearchService.Recents(scopes, ignore, _filter);
            if (recents.Count > 0)
            {
                foreach (var recent in recents)
                    rows.Add(ToRow(recent, "Recently Used"));
            }
            else if (rows.Count == 0)
                rows.Add(Hint("file-hint", "Type a name or a drive letter", "Documents, Downloads, and the rest of the PC are searched", "\uE721"));
            return rows;
        }

        if (query.Trim().Length < 2)
        {
            if (rows.Count == 0)
                rows.Add(Hint("file-hint", "Type at least two letters", "Or a drive letter such as D", "\uE721"));
            return rows;
        }

        return SearchRows(query.Trim(), ignore, rows, scopes, wholeCatalog: true);
    }

    IReadOnlyList<PaletteRow> BrowseRows(string query, FileSearchIgnoreList ignore)
    {
        var rows = new List<PaletteRow>();
        rows.Add(new PaletteRow("fs:up", "Up one level", FileBrowse.ParentPath(_root!) ?? "Volumes", "\uE74B", _root));
        var trimmed = query.Trim();
        if (trimmed.Length >= 2)
            return SearchRows(trimmed, ignore, rows, [_root!], wholeCatalog: false);

        var children = FileSearchService.Children(_root!, trimmed, ignore, _filter);
        foreach (var child in children)
            rows.Add(ToRow(child, child.IsDirectory ? "Folders" : "Files"));
        if (children.Count == 0)
            rows.Add(Hint("file-empty", _filter.EmptyMessage(), "Type a name to search this folder, or go up a level", "\uE721"));
        return rows;
    }

    IReadOnlyList<PaletteRow> SearchRows(
        string query, FileSearchIgnoreList ignore, List<PaletteRow> leading, IReadOnlyList<string> scopes, bool wholeCatalog)
    {
        var searchKey = (_root ?? "") + "\0" + query + "\0" + (wholeCatalog ? "1" : "0");
        if (searchKey != _searchQuery)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var gen = Interlocked.Increment(ref _generation);
            _searchRows = _searchRows.Where(r => FileSearchQuery.Matches(r.Title, query)).ToList();
            _searchQuery = searchKey;
            _searching = true;
            var searchScopes = scopes;
            var filter = _filter;
            _ = Task.Run(async () =>
            {
                IReadOnlyList<PaletteRow> rows = [];
                try
                {
                    await Task.Delay(FileSearchQuery.DebounceMs, token);
                    rows = FileSearchService.Search(query, searchScopes, ignore, filter, token, wholeCatalog)
                        .Select(f => ToRow(f, "Results"))
                        .ToList();
                }
                catch (OperationCanceledException) { return; }
                catch (Exception) { }

                if (token.IsCancellationRequested || gen != _generation)
                    return;
                _core.PaletteWindow?.DispatcherQueue.TryEnqueue(() =>
                {
                    if (gen != _generation)
                        return;
                    _searchRows = rows;
                    _searching = false;
                    _core.Palette.Notify();
                });
            }, token);
        }

        leading.AddRange(_searchRows);
        if (_searching && _searchRows.Count == 0)
            leading.Add(Hint("file-searching", "Searching…", "Results update as you type", "\uE721"));
        else if (!_searching && _searchRows.Count == 0)
            leading.Add(Hint("file-empty", _filter.EmptyMessage(), _root is null ? "Try another name or a drive letter" : "Try another name, or go up a level", "\uE721"));
        return leading;
    }

    public bool Activate(string id, bool reveal)
    {
        if (id is "file-off" or "file-hint" or "file-empty" or "file-searching")
            return true;
        if (id == "fs:up")
        {
            Pop();
            return true;
        }

        if (!id.StartsWith("fs:", StringComparison.Ordinal))
            return false;
        var rest = id[3..];
        var kind = "file";
        var path = rest;
        var split = rest.IndexOf(':');
        if (split > 0)
        {
            kind = rest[..split];
            path = rest[(split + 1)..];
        }

        if (kind is "dir" or "vol")
        {
            if (reveal || _core.Palette.Mode != PaletteMode.FileSearch)
            {
                if (reveal)
                    FileSearchService.Reveal(path);
                else
                    ProcessLauncher.Open(path);
                _core.PaletteCoordinator.HidePalette(restoreFocus: false);
                return true;
            }

            Enter(path);
            return true;
        }

        if (reveal)
        {
            FileSearchService.Reveal(path);
            _core.PaletteCoordinator.HidePalette(restoreFocus: false);
            return true;
        }

        _core.PaletteCoordinator.HidePalette(restoreFocus: false);
        ProcessLauncher.Open(path);
        return true;
    }

    public void CopyPath(string id)
    {
        var path = PathOf(id);
        if (path is null)
            return;
        _core.Clipboard.CopyText(path);
        _core.ShowMessage("Copied path");
    }

    public void CopyName(string id)
    {
        var path = PathOf(id);
        if (path is null)
            return;
        _core.Clipboard.CopyText(Path.GetFileName(path.TrimEnd('\\')));
        _core.ShowMessage("Copied name");
    }

    public void PasteIntoApp(string id)
    {
        var path = PathOf(id);
        if (path is null)
            return;
        var previous = _core.PaletteWindow?.PreviousHwnd ?? IntPtr.Zero;
        _core.PaletteCoordinator.HidePalette(restoreFocus: true);
        if (File.Exists(path))
        {
            _ = PasteFileAsync(path, previous);
            return;
        }

        Paster.PasteText(path, previous);
    }

    async Task PasteFileAsync(string path, IntPtr previous)
    {
        await _core.Clipboard.CopyFileAsync(path);
        Paster.SendCtrlV(previous);
    }

    public void Trash(string id)
    {
        var path = PathOf(id);
        if (path is null)
            return;
        try
        {
            Recycle.Send(path);
            _core.ShowMessage("Moved to Recycle Bin");
            CancelSearch();
            _core.Palette.Notify();
        }
        catch (Exception ex)
        {
            _core.ShowMessage(ex.Message, DialogTone.Danger);
        }
    }

    void Enter(string path)
    {
        var full = path.Trim();
        if (full.Length == 2 && full[1] == ':')
            full += "\\";
        else if (Directory.Exists(full))
            full = Path.GetFullPath(full);
        _root = full;
        CancelSearch();
        _core.Palette.Query = "";
        _core.Palette.Selection = 0;
        _core.Palette.Notify();
    }

    static IReadOnlyList<string> HomeChildren(string home) =>
        FileSearchService.LibraryRoots(home);

    FileSearchResult? LooksLikePath(string query)
    {
        var t = query.Trim().Trim('"');
        if (t.Length < 3)
            return null;
        var rest = t.TrimEnd('\\', '/');
        if (FileBrowse.DriveRoot(t) is not null && rest.Length <= 2)
            return null;
        try
        {
            if (Directory.Exists(t))
                return new FileSearchResult(Path.GetFullPath(t), Path.GetFileName(t.TrimEnd('\\')) is { Length: > 0 } n ? n : t, null, true);
            if (File.Exists(t))
                return new FileSearchResult(Path.GetFullPath(t), Path.GetFileName(t), null, false);
        }
        catch (Exception) { }

        return null;
    }

    static PaletteRow ToRow(FileSearchResult item, string section)
    {
        var kind = item.IsVolume ? "vol" : item.IsDirectory ? "dir" : "file";
        var subtitle = item.IsVolume ? "Enter to browse" : item.IsDirectory ? item.ParentName : item.Path;
        var glyph = item.IsVolume ? "\uEDA2" : item.IsDirectory ? "\uE8B7" : "\uE8A5";
        return new PaletteRow("fs:" + kind + ":" + item.Path, item.Name, subtitle, glyph, section);
    }

    static PaletteRow Hint(string id, string title, string subtitle, string glyph) =>
        new(id, title, subtitle, glyph);

    public string? PathOf(string id)
    {
        if (!id.StartsWith("fs:", StringComparison.Ordinal) || id == "fs:up")
            return null;
        var rest = id[3..];
        var split = rest.IndexOf(':');
        return split < 0 ? rest : rest[(split + 1)..];
    }

    void CancelSearch()
    {
        _cts?.Cancel();
        _generation++;
        _searchQuery = "";
        _searchRows = [];
        _searching = false;
    }
}
