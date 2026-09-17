using Tinycast.Features.Calculator;
using Tinycast.Features.Calendar;
using Tinycast.Features.Clipboard;
using Tinycast.Features.Commands;
using Tinycast.Features.Emoji;
using Tinycast.Features.FileSearch;
using Tinycast.Features.Launcher;
using Tinycast.Features.Quicklinks;
using Tinycast.Features.Snippets;
using Tinycast.Features.SystemActions;
using Tinycast.Features.Uninstall;
using Tinycast.Features.WindowManagement;
using Tinycast.Palette;
using Tinycast.Platform;

namespace Tinycast;

public sealed class LauncherCoordinator
{
    readonly AppCore _core;
    CalcResult? _lastCalc;

    public LauncherCoordinator(AppCore core) => _core = core;

    public void RevealApplication(AppEntry app)
    {
        if (string.IsNullOrWhiteSpace(app.Path))
            return;
        if (!Path.IsPathFullyQualified(app.Path) && app.Path.Contains('!', StringComparison.Ordinal))
            ProcessLauncher.Open("shell:AppsFolder\\" + app.Path);
        else
            FileSearchService.Reveal(app.Path);
    }

    public async Task RestartApplicationAsync(AppEntry app)
    {
        var result = await _core.AppProcesses.RestartAsync(app);
        ReportProcessResult(result);
    }

    public async Task QuitApplicationAsync(AppEntry app)
    {
        var result = await _core.AppProcesses.QuitAsync(app);
        ReportProcessResult(result);
    }

    void ReportProcessResult(AppProcessResult result)
    {
        if (result is AppProcessResult.UnsupportedIdentity or AppProcessResult.IdentityUnavailable)
            _core.ShowMessage("This application's process identity could not be verified.", DialogTone.Neutral);
        else if (result == AppProcessResult.TimedOut)
            _core.ShowMessage("Application is still running. No replacement was launched.", DialogTone.Neutral);
        else if (result == AppProcessResult.Failed)
            _core.ShowMessage("Application could not be closed safely.", DialogTone.Neutral);
    }

    public IReadOnlyList<PaletteRow> Rows(string query)
    {
        return _core.Palette.Mode switch
        {
            PaletteMode.Clipboard => ClipboardRows(query),
            PaletteMode.Emoji => EmojiRows(query),
            PaletteMode.FileSearch => FileRows(query),
            PaletteMode.CalculatorHistory => HistoryRows(query),
            PaletteMode.Snippets => SnippetRows(query),
            PaletteMode.Quicklinks => QuicklinkRows(query),
            PaletteMode.SwitchWindows => SwitchRows(query),
            PaletteMode.MenuSearch => MenuRows(query),
            PaletteMode.Uninstall => UninstallRows(query),
            PaletteMode.Schedule => ScheduleRows(query),
            PaletteMode.AiChat => AiRows(query),
            PaletteMode.Volume => VolumeRows(query),
            PaletteMode.CommandOutput => OutputRows(query),
            _ => LauncherRows(query),
        };
    }

    IReadOnlyList<PaletteRow> LauncherRows(string query)
    {
        var rows = new List<PaletteRow>();
        var calc = CalcEngine.Evaluate(
            query,
            DateTime.Now,
            CurrencyRateStore.Current,
            System.Globalization.RegionInfo.CurrentRegion.ISOCurrencySymbol);
        if (calc is not null)
        {
            if (calc.IsActionable)
                _lastCalc = calc;
            rows.Add(PaletteRow.Calculator("calc-live", calc, "Calculator"));
        }

        var entries = new List<AppEntry>();
        entries.AddRange(_core.Apps);
        foreach (var setting in MsSettingsCatalog.All)
        {
            entries.Add(new AppEntry(
                "settings-uri:" + setting.Uri, setting.Title, AppEntryKind.SystemSettings,
                setting.Uri, setting.Uri, setting.Glyph, new SearchFields(SearchAlias.Name(setting.Title))));
        }

        foreach (var action in SystemActionCatalog.All)
        {
            entries.Add(new AppEntry(
                action.EntryId, action.Name, AppEntryKind.SystemAction, "System",
                null, action.Glyph, new SearchFields(SearchAlias.Name(action.Name))));
        }

        foreach (var command in WindowCommandCatalog.All)
        {
            if (!_core.Settings.WindowManagementEnabled && query.Length == 0)
                continue;
            entries.Add(new AppEntry(
                command.EntryId, command.Name, AppEntryKind.Command, command.Group.ToString(),
                null, "\uE7C4", new SearchFields(SearchAlias.Name(command.Name))));
        }

        foreach (var cmd in BuiltinEntries())
            entries.Add(cmd);
        foreach (var link in _core.Quicklinks)
            entries.Add(new AppEntry(link.Id, link.Name, AppEntryKind.Quicklink, link.Destination, link.Destination, "\uE71B", new SearchFields(SearchAlias.Name(link.Name), SearchAlias.Technical(link.Destination))));
        foreach (var snippet in _core.Snippets)
            entries.Add(new AppEntry(snippet.Id, snippet.Name, AppEntryKind.Snippet, snippet.Keyword, null, "\uE8A5", new SearchFields(SearchAlias.Name(snippet.Name), SearchAlias.Name(snippet.Keyword))));
        foreach (var custom in _core.CustomCommands)
            entries.Add(new AppEntry(custom.Id, custom.Name, AppEntryKind.CustomCommand, custom.FileName, custom.FileName, "\uE756", new SearchFields(SearchAlias.Name(custom.Name))));
        if (_core.Settings.NotesEnabled)
        {
            foreach (var note in _core.Notes)
                entries.Add(new AppEntry("note:" + note.Id, note.Title, AppEntryKind.Command, "Note", note.Path, "\uE70B", new SearchFields(SearchAlias.Name(note.Title))));
        }

        var ranked = LauncherOrder.Rank(
            entries,
            query,
            _core.Ranking.Usage(query),
            _core.Aliases.Items,
            id => _core.Visibility.IsHidden(id),
            _core.Settings,
            _core.Visibility);

        if (query.Length == 0)
        {
            if (_core.Settings.QuickActionsEnabled && !string.IsNullOrWhiteSpace(_core.LastSelection))
            {
                rows.Insert(0, new PaletteRow(
                    "quick-action:selection",
                    "Selected text",
                    _core.LastSelection.Length > 80 ? _core.LastSelection[..80] + "…" : _core.LastSelection,
                    "\uE8A1",
                    "Quick Actions",
                    Preview: _core.LastSelection,
                    CopyText: _core.LastSelection));
            }

            if (_core.Settings.CalendarEnabled)
            {
                var join = MeetingJoinCard.NextJoinable(_core.Meetings, DateTime.Now, _core.Settings.CalendarExcludedIds);
                if (join?.Link is not null)
                {
                    rows.Insert(0, new PaletteRow(
                        "meet:" + join.Id,
                        "Join " + join.Title,
                        join.Link.Title + "  ·  " + join.Start.ToString("t"),
                        "\uE716",
                        "Meetings",
                        PrimaryAction: "Join"));
                }
            }

            var favorites = ranked.Where(e => _core.Favorites.IsFavorite(e.Id)).Take(8).ToList();
            foreach (var entry in favorites)
                rows.Add(ToRow(entry, "Favorites"));
            foreach (var entry in ranked.Where(e => e.Kind == AppEntryKind.Command).Take(12))
                rows.Add(ToRow(entry, "Commands"));
            foreach (var entry in ranked.Where(e => e.Kind == AppEntryKind.Application).Take(12))
                rows.Add(ToRow(entry, "Applications"));
            if (_core.Settings.NavigationEnabled)
            {
                foreach (var layout in _core.Layouts)
                    rows.Add(new PaletteRow("layout:" + layout.Id, layout.Name, layout.Slots.Count + " windows", "\uE8A9", "Layouts"));
            }
            return rows;
        }

        if (FallbackCatalog.LooksLikeUrl(query))
        {
            rows.Insert(calc is null ? 0 : Math.Min(1, rows.Count), new PaletteRow(
                FallbackCatalog.Browser,
                "Open in Browser",
                FallbackCatalog.BrowserTarget(query),
                "\uE774",
                "Browser",
                PrimaryAction: "Open"));
        }

        foreach (var entry in ranked.Take(40))
            rows.Add(ToRow(entry, SectionFor(entry.Kind)));
        foreach (var fallback in FallbackRows(query))
            rows.Add(fallback);
        return rows;
    }

    IEnumerable<PaletteRow> FallbackRows(string query)
    {
        foreach (var spec in _core.Fallbacks.Where(f => f.Enabled))
        {
            if (spec.Id == FallbackCatalog.Ai)
            {
                if (!_core.Settings.AiEnabled)
                    continue;
                yield return new PaletteRow(spec.Id, "Ask AI", query, "\uE99A", "Use with");
            }
            else if (spec.Id == FallbackCatalog.Files)
            {
                if (!_core.Settings.FileSearchEnabled)
                    continue;
                yield return new PaletteRow(spec.Id, "Search Files", query, "\uE721", "Use with");
            }
            else if (spec.Id == FallbackCatalog.Shell)
            {
                yield return new PaletteRow(spec.Id, "Run Shell Command", query, "\uE756", "Use with");
            }
            else if (spec.Id.StartsWith("quicklink:", StringComparison.Ordinal))
            {
                var link = _core.Quicklinks.FirstOrDefault(q => q.Id == spec.Id);
                if (link is null || !_core.Settings.QuicklinksEnabled)
                    continue;
                yield return new PaletteRow("fallback-link:" + link.Id, link.Name, query, "\uE71B", "Use with");
            }
        }
    }

    IEnumerable<AppEntry> BuiltinEntries()
    {
        yield return Cmd(BuiltinCommands.Settings, "Settings", "Open Tinycast settings", "\uE713");
        yield return Cmd(BuiltinCommands.Clipboard, "Clipboard History", "Browse copied text and images", "\uE16D");
        yield return Cmd(BuiltinCommands.Emoji, "Emoji", "Insert an emoji", "\uE899");
        yield return Cmd(BuiltinCommands.FileSearch, "Search Files", "Find files in your folders", "\uE721");
        yield return Cmd(BuiltinCommands.Notes, "Notes", "Floating markdown notes", "\uE70B");
        yield return Cmd(BuiltinCommands.CalculatorHistory, "Calculator History", "Past calculations", "\uE8EF");
        yield return Cmd(BuiltinCommands.Snippets, "Snippets", "Expandable text snippets", "\uE8A5");
        yield return Cmd(BuiltinCommands.Quicklinks, "Quicklinks", "Saved URLs and paths", "\uE71B");
        yield return Cmd(BuiltinCommands.SwitchWindows, "Switch Windows", "Filter open windows", "\uE8A7");
        yield return Cmd(BuiltinCommands.MenuSearch, "Search Menu Items", "Find commands in the front app", "\uE700");
        yield return Cmd(BuiltinCommands.Uninstall, "Uninstall Leftovers", "Find leftover app files", "\uE74D");
        yield return Cmd(BuiltinCommands.Support, "Support Tinycast", "About and support", "\uE946");
        yield return Cmd(BuiltinCommands.Backup, "Backup", "Export a .tinycast archive", "\uE8B5");
        yield return Cmd(BuiltinCommands.Updates, "Check for Updates", "GitHub Releases", "\uE895");
        yield return Cmd(BuiltinCommands.Camera, "Open Camera", "Camera preview", "\uE722");
        yield return Cmd(BuiltinCommands.AiChat, "AI Chat", "Ask a model", "\uE99A");
        yield return Cmd(BuiltinCommands.Schedule, "Schedule", "Upcoming calendar events", "\uE787");
        yield return Cmd(BuiltinCommands.SaveLayout, "Save Window Layout", "Remember open window frames", "\uE8A9");
        yield return Cmd(BuiltinCommands.SearchNotes, "Search Notes", "Find a note by title", "\uE721");
        yield return Cmd(BuiltinCommands.RevealNotes, "Reveal Notes Folder", "Open the notes folder", "\uE8B7");
        yield return Cmd(BuiltinCommands.JoinNext, "Join Next Meeting", "Open the next joinable meeting", "\uE716");
        yield return Cmd(BuiltinCommands.CreateEvent, "Create Event", "Compose a calendar event", "\uE787");
        yield return Cmd(BuiltinCommands.CopyMeetingLink, "Copy Meeting Link", "Copy the next meeting URL", "\uE71B");
        yield return Cmd(BuiltinCommands.OpenCalendar, "Open Calendar", "Open the Windows Calendar app", "\uE787");
        yield return Cmd(BuiltinCommands.Quit, "Quit Tinycast", "Leave the tray and hotkey", "\uE711");
    }

    static AppEntry Cmd(string id, string title, string subtitle, string glyph) =>
        new(id, title, AppEntryKind.Command, subtitle, null, glyph, new SearchFields(SearchAlias.Name(title), SearchAlias.Translation(subtitle)));

    static PaletteRow ToRow(AppEntry entry, string section) =>
        new(
            entry.Id,
            entry.Title,
            entry.Kind == AppEntryKind.Application ? null : entry.Subtitle,
            entry.Glyph ?? "\uE7C5",
            section,
            entry.Kind,
            IconPath: entry.IconPath,
            ShowActions: entry.Kind == AppEntryKind.Application);

    static string SectionFor(AppEntryKind kind) => kind switch
    {
        AppEntryKind.Application => "Applications",
        AppEntryKind.SystemSettings => "Settings",
        AppEntryKind.SystemAction => "System",
        AppEntryKind.Quicklink => "Quicklinks",
        AppEntryKind.Snippet => "Snippets",
        AppEntryKind.CustomCommand => "Commands",
        AppEntryKind.Favorite => "Favorites",
        _ => "Commands",
    };

    IReadOnlyList<PaletteRow> ClipboardRows(string query) => _core.ClipboardCoordinator.Rows(query);

    IReadOnlyList<PaletteRow> EmojiRows(string query)
    {
        var skin = _core.Settings.EmojiSkinTone;
        var hits = EmojiCatalog.Search(query, skin).ToList();
        var pinned = _core.Favorites.OrderedIds()
            .Where(id => id.StartsWith("emoji:", StringComparison.Ordinal))
            .Select(id => hits.FirstOrDefault(e => ("emoji:" + e.Glyph) == id))
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();
        var rest = hits.Where(e => pinned.All(p => p.Glyph != e.Glyph)).ToList();
        var rows = new List<PaletteRow>();
        foreach (var e in pinned.Concat(rest))
            rows.Add(new PaletteRow("emoji:" + e.Glyph, e.Glyph + "  " + e.Name, e.Group, e.Glyph, e.Group, AppEntryKind.Command, e.Glyph, false, e.Glyph));
        return rows;
    }

    IReadOnlyList<PaletteRow> FileRows(string query) => _core.FileSearchCoordinator.Rows(query);

    IReadOnlyList<PaletteRow> HistoryRows(string query) =>
        _core.CalcHistory
            .Where(c => query.Length == 0 || c.Expression.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select((c, i) => PaletteRow.Calculator("calc-hist:" + i, c, "History"))
            .ToList();

    IReadOnlyList<PaletteRow> SnippetRows(string query)
    {
        if (!_core.Settings.SnippetsEnabled)
            return [new PaletteRow("snip-off", "Snippets are off", "Enable them in Settings → Snippets", "\uE8A5")];
        if (_core.PendingSnippet is { } pending)
        {
            var declared = SnippetTemplateEngine.DeclaredArguments(pending.Text);
            var name = declared.Count > 0 ? declared[0].Name : "argument";
            return
            [
                new PaletteRow(
                    pending.Id,
                    "Use “" + (query.Length == 0 ? "…" : query) + "” for " + name,
                    pending.Name,
                    "\uE8A5",
                    "Argument",
                    AppEntryKind.Snippet,
                    query,
                    false,
                    query),
            ];
        }

        return _core.Snippets.Where(s => query.Length == 0 || s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || s.Keyword.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(s => new PaletteRow(s.Id, s.Name, s.Keyword, "\uE8A5", "Snippets", AppEntryKind.Snippet, s.Text, false, s.Text))
            .ToList();
    }

    IReadOnlyList<PaletteRow> QuicklinkRows(string query)
    {
        if (!_core.Settings.QuicklinksEnabled)
            return [new PaletteRow("link-off", "Quicklinks are off", "Enable them in Settings → Quicklinks", "\uE71B")];
        return _core.Quicklinks.Where(s => query.Length == 0 || s.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(s => new PaletteRow(s.Id, s.Name, s.Destination, "\uE71B", "Quicklinks", AppEntryKind.Quicklink))
            .ToList();
    }

    IReadOnlyList<PaletteRow> SwitchRows(string query)
    {
        if (!_core.Settings.NavigationEnabled)
            return [new PaletteRow("nav-off", "Navigation is off", "Enable it in Settings → Navigation", "\uE8A7")];
        var excluded = _core.Settings.NavigationExcludedApps;
        var rows = new List<PaletteRow>
        {
            new("desk:next", "Next virtual desktop", "Win+Ctrl+Right", "\uE149", "Desktops"),
            new("desk:prev", "Previous virtual desktop", "Win+Ctrl+Left", "\uE148", "Desktops"),
        };
        rows.AddRange(WindowSwitchQuery.Filter(WindowInventory.Enumerate(), query)
            .Where(w => !excluded.Any(ex => w.ProcessName.Equals(ex.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Select(w => new PaletteRow("switch:" + w.Hwnd.ToInt64(), w.Title, w.ProcessName, "\uE8A7", "Windows")));
        return rows;
    }

    IReadOnlyList<PaletteRow> MenuRows(string query)
    {
        if (!_core.Settings.NavigationEnabled)
            return [new PaletteRow("nav-off", "Navigation is off", "Enable it in Settings → Navigation", "\uE700")];
        var hwnd = _core.PaletteWindow?.PreviousHwnd ?? IntPtr.Zero;
        var process = Paster.ForegroundProcessPath(hwnd);
        if (ClipboardPolicy.IsIgnored(process, _core.Settings.NavigationExcludedApps))
            return [new PaletteRow("menu-skip", "This app is excluded", "Remove it from Settings → Navigation", "\uE700")];
        var items = MenuProbe.Items(hwnd);
        if (items.Count == 0)
            items = AccessibleMenu.Items(hwnd);
        var rows = MenuSearchQuery.Filter(items, query)
            .Select(i => new PaletteRow("menu:" + i.Hwnd.ToInt64() + ":" + i.CommandId, i.Path, i.Shortcut, "\uE700", "Menu"))
            .ToList();
        if (rows.Count == 0)
            rows.Add(new PaletteRow("menu-empty", "No menu items", "Classic menus and accessibility trees are searched", "\uE700"));
        return rows;
    }

    IReadOnlyList<PaletteRow> UninstallRows(string query)
    {
        var name = query.Trim();
        if (name.Length < 3)
            return [new PaletteRow("uninstall-hint", "Type an app name", "At least three letters. Tinycast’s own data stays protected.", "\uE74D")];
        var hits = UninstallScanner.Find(name)
            .Select(h => new PaletteRow(
                "uninstall:" + h.Path,
                h.Title,
                h.Kind + " · " + UninstallLeftoverLogic.SizeLabel(h.Bytes),
                "\uE74D",
                "Leftovers",
                Preview: h.Path))
            .ToList();
        if (hits.Count == 0)
            hits.Add(new PaletteRow("uninstall-empty", "No leftovers for that name", "Type the app name. System folders stay protected.", "\uE74D"));
        return hits;
    }

    IReadOnlyList<PaletteRow> ScheduleRows(string query)
    {
        if (!_core.Settings.CalendarEnabled)
            return [new PaletteRow("cal-off", "Calendar is off", "Enable it in Settings → Calendar", "\uE787")];
        var rows = _core.Meetings
            .Where(m => query.Length == 0 || m.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(m => new PaletteRow("meet:" + m.Id, m.Title, m.Start.ToString("g") + (m.Link is null ? "" : " · " + m.Link.Title), "\uE787", "Schedule"))
            .ToList();
        if (rows.Count == 0)
            rows.Add(new PaletteRow("cal-empty", "No upcoming events", "Grant calendar access from Settings", "\uE787"));
        return rows;
    }

    IReadOnlyList<PaletteRow> AiRows(string query)
    {
        var rows = _core.Chat.Select((m, i) => new PaletteRow("ai:" + i, m.Role, m.Content, "\uE99A", "Chat", Preview: m.Content)).ToList();
        if (!_core.Settings.AiEnabled)
        {
            rows.Insert(0, new PaletteRow("ai-off", "AI is off", "Enable it in Settings → AI", "\uE99A", "Chat"));
            return rows;
        }

        var subtitle = _core.AiCoordinator.Models.Count > 0
            ? _core.AiCoordinator.SelectedModel + " · " + _core.AiCoordinator.Models.Count + " OpenCode models"
            : _core.AiCoordinator.Status;
        if (query.Length > 0)
            rows.Insert(0, new PaletteRow("ai-send:" + query, "Send: " + query, subtitle, "\uE724", "Compose"));
        else if (rows.Count == 0)
            rows.Add(new PaletteRow("ai-empty", "Ask via OpenCode", subtitle, "\uE99A"));
        else
            rows.Insert(0, new PaletteRow("ai-model", _core.AiCoordinator.SelectedModel, subtitle, "\uE99A", "Model"));
        return rows;
    }

    IReadOnlyList<PaletteRow> VolumeRows(string query)
    {
        return VolumeSteps.Percents
            .Where(p => query.Length == 0 || p.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) || query.Contains(p.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(p => new PaletteRow("volume:" + p, p + "%", "Set output volume", "\uE767", "Volume"))
            .ToList();
    }

    IReadOnlyList<PaletteRow> OutputRows(string query)
    {
        var text = _core.LastCommandOutput;
        if (string.IsNullOrWhiteSpace(text))
            return [new PaletteRow("out-empty", "No command output", "Run a custom command to capture stdout", "\uE756")];
        return text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => query.Length == 0 || line.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(80)
            .Select((line, i) => new PaletteRow("out:" + i, line, null, "\uE8A5", "Output", CopyText: line))
            .ToList();
    }

    public async void Activate(string id, bool reveal = false)
    {
        if (id is not ("file-off" or "file-hint" or "file-empty" or "file-searching" or "fs:up"))
            _core.Ranking.Record(id, _core.Palette.Query);
        if (_core.FileSearchCoordinator.Activate(id, reveal))
            return;
        if (id == "calc-live" && _lastCalc is { IsActionable: true } live)
        {
            CopyCalc(live, record: true);
            return;
        }

        if (id.StartsWith("calc-hist:", StringComparison.Ordinal)
            && int.TryParse(id["calc-hist:".Length..], out var histIndex)
            && histIndex >= 0 && histIndex < _core.CalcHistory.Count)
        {
            CopyCalc(_core.CalcHistory[histIndex], record: false);
            return;
        }

        if (id.StartsWith("calc:", StringComparison.Ordinal))
        {
            var text = id["calc:".Length..];
            CopyCalc(new CalcResult(text, text, text, "History", "Result", false), record: true);
            return;
        }

        if (_core.ClipboardCoordinator.Activate(id, inverted: reveal))
            return;

        if (id.StartsWith("emoji:", StringComparison.Ordinal))
        {
            var glyph = id[6..];
            var previous = TargetHwnd();
            _core.PaletteCoordinator.HidePalette();
            Paster.PasteText(glyph, previous);
            return;
        }

        if (id == FallbackCatalog.Browser)
        {
            _core.PaletteCoordinator.HidePalette(restoreFocus: false);
            ProcessLauncher.OpenUri(FallbackCatalog.BrowserTarget(_core.Palette.Query));
            return;
        }

        if (id == FallbackCatalog.Ai)
        {
            _core.PaletteCoordinator.ShowPalette(PaletteMode.AiChat, seeding: _core.Palette.Query);
            return;
        }

        if (id == FallbackCatalog.Files)
        {
            _core.PaletteCoordinator.ShowPalette(PaletteMode.FileSearch, seeding: _core.Palette.Query);
            return;
        }

        if (id == FallbackCatalog.Shell)
        {
            var command = _core.Palette.Query.Trim();
            if (command.Length == 0)
                return;
            var choice = await _core.Confirm(new DialogRequest(
                "Run this command?",
                "\uE756",
                [new DialogAction("Cancel", DialogActionRole.Cancel), new DialogAction("Run")],
                1, 0, command));
            if (choice != 1)
                return;
            _core.PaletteCoordinator.HidePalette();
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            }
            catch (Exception ex) { _core.ShowMessage(ex.Message, DialogTone.Danger); }
            return;
        }

        if (id.StartsWith("fallback-link:", StringComparison.Ordinal))
        {
            var link = _core.Quicklinks.FirstOrDefault(q => q.Id == id["fallback-link:".Length..]);
            if (link is not null)
            {
                _core.PaletteCoordinator.HidePalette(restoreFocus: false);
                ProcessLauncher.Open(ExpandLink(link));
            }

            return;
        }

        if (id.StartsWith("app:", StringComparison.Ordinal)
            || id.StartsWith("store:", StringComparison.Ordinal)
            || id.StartsWith("file:", StringComparison.Ordinal))
        {
            _core.PaletteCoordinator.HidePalette(restoreFocus: false);
            var app = _core.Apps.FirstOrDefault(a => a.Id == id);
            if (app is not null)
                ProcessLauncher.Launch(app);
            else
                ProcessLauncher.Open(id[(id.IndexOf(':') + 1)..]);
            return;
        }

        if (id.StartsWith("settings-uri:", StringComparison.Ordinal))
        {
            _core.PaletteCoordinator.HidePalette(restoreFocus: false);
            ProcessLauncher.OpenUri(id["settings-uri:".Length..]);
            return;
        }

        if (id.StartsWith("system-action:", StringComparison.Ordinal))
        {
            var action = SystemActionCatalog.Find(id);
            if (action is null)
                return;
            if (action.Confirmation is SystemActionConfirmationKind.Required or SystemActionConfirmationKind.Computed)
            {
                var message = action.ConfirmMessage;
                if (action.Id == "quit-all-apps")
                    message = WindowInventory.Enumerate().Count() + " windows will be asked to close.";
                var choice = await _core.Confirm(new DialogRequest(
                    action.ConfirmTitle ?? action.Name,
                    action.Glyph,
                    [new DialogAction("Cancel", DialogActionRole.Cancel), new DialogAction(action.Name, DialogActionRole.Destructive)],
                    0, 0, message, DialogTone.Danger));
                if (choice != 1)
                    return;
            }

            _core.PaletteCoordinator.HidePalette();
            SystemActionRunner.Run(action.Id, _core);
            return;
        }

        if (id.StartsWith("window-command:", StringComparison.Ordinal))
        {
            var previous = TargetHwnd();
            _core.PaletteCoordinator.HidePalette();
            WindowMover.Run(id["window-command:".Length..], previous);
            return;
        }

        if (id.StartsWith("switch:", StringComparison.Ordinal) && long.TryParse(id[7..], out var hwndVal))
        {
            _core.PaletteCoordinator.HidePalette(restoreFocus: false);
            WindowInventory.Focus(new IntPtr(hwndVal));
            return;
        }

        if (id.StartsWith("quicklink:", StringComparison.Ordinal) || _core.Quicklinks.Any(q => q.Id == id))
        {
            var link = _core.Quicklinks.FirstOrDefault(q => q.Id == id);
            if (link is not null)
            {
                _core.PaletteCoordinator.HidePalette(restoreFocus: false);
                ProcessLauncher.Open(ExpandLink(link));
            }

            return;
        }

        if (id.StartsWith("note:", StringComparison.Ordinal))
        {
            _core.PaletteCoordinator.HidePalette();
            _core.ShowNotes(id["note:".Length..]);
            return;
        }

        if (id.StartsWith("volume:", StringComparison.Ordinal) && int.TryParse(id["volume:".Length..], out var percent))
        {
            _core.PaletteCoordinator.HidePalette();
            SystemActionRunner.RunVolume(percent, _core);
            return;
        }

        if (id is "desk:next" or "desk:prev")
        {
            _core.PaletteCoordinator.HidePalette();
            if (id == "desk:next")
                VirtualDesktop.Next();
            else
                VirtualDesktop.Previous();
            return;
        }

        if (id.StartsWith("out:", StringComparison.Ordinal))
        {
            var row = OutputRows("").FirstOrDefault(r => r.Id == id);
            if (row?.CopyText is { } copy)
            {
                _core.Clipboard.CopyText(copy);
                _core.ShowMessage("Copied");
                _core.PaletteCoordinator.HidePalette();
            }

            return;
        }

        if (_core.Snippets.Any(s => s.Id == id))
        {
            var snippet = _core.Snippets.First(s => s.Id == id);
            var previous = TargetHwnd();
            if (!_core.TryPasteSnippet(snippet, previous))
                _core.PaletteCoordinator.ShowPalette(PaletteMode.Snippets);
            return;
        }

        if (_core.CustomCommands.Any(c => c.Id == id))
        {
            var command = _core.CustomCommands.First(c => c.Id == id);
            if (command.Confirm)
            {
                var choice = await _core.Confirm(new DialogRequest(
                    "Run " + command.Name + "?",
                    "\uE756",
                    [new DialogAction("Cancel", DialogActionRole.Cancel), new DialogAction("Run")],
                    1, 0, command.FileName));
                if (choice != 1)
                    return;
            }

            _core.PaletteCoordinator.HidePalette();
            try
            {
                var extra = _core.Palette.Query.Trim();
                if (extra.Equals(command.Name, StringComparison.OrdinalIgnoreCase))
                    extra = "";
                var output = CommandProcess.Run(command.FileName, command.Arguments, extra);
                if (!string.IsNullOrWhiteSpace(output))
                {
                    _core.LastCommandOutput = output;
                    _core.PaletteCoordinator.ShowPalette(PaletteMode.CommandOutput);
                }
            }
            catch (Exception ex) { _core.ShowMessage(ex.Message, DialogTone.Danger); }
            return;
        }

        if (id.StartsWith("uninstall:", StringComparison.Ordinal))
        {
            var path = id["uninstall:".Length..];
            if (UninstallRules.IsProtected(path) || path.StartsWith("reg:", StringComparison.OrdinalIgnoreCase))
            {
                if (path.StartsWith("reg:", StringComparison.OrdinalIgnoreCase))
                {
                    _core.ShowMessage("Uninstall this app from Settings → Apps.", DialogTone.Neutral);
                    ProcessLauncher.OpenUri("ms-settings:appsfeatures");
                    return;
                }

                _core.ShowMessage("Protected path", DialogTone.Danger);
                return;
            }

            var choice = await _core.Confirm(new DialogRequest(
                "Move to Recycle Bin?",
                "\uE74D",
                [new DialogAction("Cancel", DialogActionRole.Cancel), new DialogAction("Move", DialogActionRole.Destructive)],
                0, 0, path, DialogTone.Danger));
            if (choice == 1)
            {
                try
                {
                    if (File.Exists(path) || Directory.Exists(path))
                        Recycle.Send(path);
                    else
                        ProcessLauncher.Open(path);
                }
                catch (Exception ex) { _core.ShowMessage(ex.Message, DialogTone.Danger); }
            }

            return;
        }

        if (id.StartsWith("meet:", StringComparison.Ordinal))
        {
            var meeting = _core.Meetings.FirstOrDefault(m => m.Id == id[5..]);
            if (meeting?.Link is not null)
            {
                _core.PaletteCoordinator.HidePalette(restoreFocus: false);
                ProcessLauncher.OpenUri(meeting.Link.Url.ToString());
            }

            return;
        }

        if (id.StartsWith("ai-send:", StringComparison.Ordinal))
        {
            await _core.SendChat(id["ai-send:".Length..]);
            return;
        }

        if (id == "ai-model")
        {
            _core.ShowMessage(_core.AiCoordinator.Status);
            return;
        }

        if (id.StartsWith("menu:", StringComparison.Ordinal))
        {
            var parts = id["menu:".Length..].Split(':', 2);
            if (parts.Length == 2 && long.TryParse(parts[0], out var hwnd) && uint.TryParse(parts[1], out var cmd) && cmd != 0)
            {
                _core.PaletteCoordinator.HidePalette(restoreFocus: true);
                NativeMethods.PostMessage(new IntPtr(hwnd), NativeMethods.WmCommand, (IntPtr)cmd, IntPtr.Zero);
            }
            return;
        }

        if (id.StartsWith("layout:", StringComparison.Ordinal))
        {
            _core.PaletteCoordinator.HidePalette();
            _core.RestoreLayout(id["layout:".Length..]);
            return;
        }

        if (id == "quick-action:selection")
        {
            _core.Clipboard.CopyText(_core.LastSelection);
            _core.ShowMessage("Copied selection");
            _core.PaletteCoordinator.HidePalette();
            return;
        }

        switch (id)
        {
            case BuiltinCommands.Settings:
                _core.PaletteCoordinator.HidePalette();
                _core.SettingsCoordinator.Show();
                break;
            case BuiltinCommands.Quit:
                _core.Quit();
                break;
            case BuiltinCommands.Clipboard:
                _core.PaletteCoordinator.ShowPalette(PaletteMode.Clipboard);
                break;
            case BuiltinCommands.Emoji:
                _core.PaletteCoordinator.ShowPalette(PaletteMode.Emoji);
                break;
            case BuiltinCommands.FileSearch:
                _core.PaletteCoordinator.ShowPalette(PaletteMode.FileSearch);
                break;
            case BuiltinCommands.Notes:
                _core.PaletteCoordinator.HidePalette();
                _core.ShowNotes();
                break;
            case BuiltinCommands.CalculatorHistory:
                _core.PaletteCoordinator.ShowPalette(PaletteMode.CalculatorHistory);
                break;
            case BuiltinCommands.Snippets:
                _core.PaletteCoordinator.ShowPalette(PaletteMode.Snippets);
                break;
            case BuiltinCommands.Quicklinks:
                _core.PaletteCoordinator.ShowPalette(PaletteMode.Quicklinks);
                break;
            case BuiltinCommands.SwitchWindows:
                _core.PaletteCoordinator.ShowPalette(PaletteMode.SwitchWindows);
                break;
            case BuiltinCommands.MenuSearch:
                _core.PaletteCoordinator.ShowPalette(PaletteMode.MenuSearch);
                break;
            case BuiltinCommands.Uninstall:
                _core.PaletteCoordinator.ShowPalette(PaletteMode.Uninstall);
                break;
            case BuiltinCommands.Schedule:
                _core.PaletteCoordinator.ShowPalette(PaletteMode.Schedule);
                break;
            case BuiltinCommands.AiChat:
                _core.PaletteCoordinator.ShowPalette(PaletteMode.AiChat);
                break;
            case BuiltinCommands.Support:
                _core.PaletteCoordinator.HidePalette();
                _core.ShowSupport();
                break;
            case BuiltinCommands.Backup:
                _core.PaletteCoordinator.HidePalette();
                _core.SettingsCoordinator.Show(Tinycast.Features.Settings.SettingsTab.Backup);
                break;
            case BuiltinCommands.Updates:
                _core.PaletteCoordinator.HidePalette();
                _core.SettingsCoordinator.Show(Tinycast.Features.Settings.SettingsTab.About);
                _ = _core.CheckUpdates();
                break;
            case BuiltinCommands.Camera:
                _core.PaletteCoordinator.HidePalette();
                _core.ShowCamera();
                break;
            case BuiltinCommands.SaveLayout:
                _core.PaletteCoordinator.HidePalette();
                _core.SaveCurrentLayout();
                break;
            case BuiltinCommands.SearchNotes:
                _core.PaletteCoordinator.HidePalette();
                _core.ShowNotes();
                break;
            case BuiltinCommands.RevealNotes:
                _core.PaletteCoordinator.HidePalette(restoreFocus: false);
                ProcessLauncher.Open(AppPaths.NotesDir);
                break;
            case BuiltinCommands.JoinNext:
                _core.JoinNextMeeting();
                break;
            case BuiltinCommands.CreateEvent:
                _core.PaletteCoordinator.HidePalette(restoreFocus: false);
                _ = _core.CreateCalendarEventAsync();
                break;
            case BuiltinCommands.CopyMeetingLink:
                _core.CopyNextMeetingLink();
                break;
            case BuiltinCommands.OpenCalendar:
                _core.PaletteCoordinator.HidePalette(restoreFocus: false);
                ProcessLauncher.OpenUri("ms-calendar:");
                break;
        }
    }

    string ExpandLink(Quicklink link)
    {
        var clip = _core.ClipboardStore.Search("").FirstOrDefault()?.Text ?? "";
        return QuicklinkDestination.Expand(link.Destination, _core.Palette.Query, _core.LastSelection, clip);
    }

    public void CopyCalculator(string id, bool withExpression)
    {
        if (id == "calc-live" && _lastCalc is { IsActionable: true } live)
        {
            CopyCalc(live, record: true, withExpression);
            return;
        }

        if (id.StartsWith("calc-hist:", StringComparison.Ordinal)
            && int.TryParse(id["calc-hist:".Length..], out var histIndex)
            && histIndex >= 0 && histIndex < _core.CalcHistory.Count)
        {
            CopyCalc(_core.CalcHistory[histIndex], record: false, withExpression);
        }
    }

    void CopyCalc(CalcResult calc, bool record, bool withExpression = false)
    {
        if (!calc.IsActionable)
            return;
        var text = withExpression ? calc.Expression + " = " + calc.CopyText : calc.CopyText;
        _core.Clipboard.CopyText(text);
        if (record)
        {
            _core.CalcHistory.Insert(0, calc);
            if (_core.CalcHistory.Count > 50)
                _core.CalcHistory.RemoveAt(50);
            _core.PersistCalcHistory();
        }

        _core.ShowMessage("Copied " + text);
        _core.PaletteCoordinator.HidePalette();
    }

    IntPtr TargetHwnd()
    {
        if (!_core.PaletteCoordinator.IsVisible)
            return IntPtr.Zero;
        var palette = _core.PaletteWindow;
        if (palette is null)
            return IntPtr.Zero;
        var previous = palette.PreviousHwnd;
        if (previous == IntPtr.Zero || previous == palette.Hwnd)
            return IntPtr.Zero;
        return previous;
    }
}
