using Microsoft.Data.Sqlite;
using System.IO.Compression;
using System.Text.Json;
using Relay;
using Relay.DesignSystem;
using Relay.Features.Backup;
using Relay.Features.Calculator;
using Relay.Features.Calendar;
using Relay.Features.Clipboard;
using Relay.Features.Commands;
using Relay.Features.Emoji;
using Relay.Features.HotKeys;
using Relay.Features.Launcher;
using Relay.Features.Quicklinks;
using Relay.Features.Settings;
using Relay.Features.Snippets;
using Relay.Features.SystemActions;
using Relay.Features.Uninstall;
using Relay.Features.FileSearch;
using Relay.Features.Updates;
using Relay.Features.WindowManagement;
using Relay.Features.Ai;
using Relay.Palette;

var failed = 0;
var passed = 0;

void Check(string name, bool ok, string? detail = null)
{
    if (ok)
    {
        passed++;
        Console.WriteLine($"  ok   {name}");
        return;
    }

    failed++;
    Console.WriteLine($" FAIL  {name}{(detail is null ? "" : ": " + detail)}");
}

{
    var palette = new PaletteState();
    palette.Query = "clip";
    palette.Selection = 2;
    palette.Push(PaletteMode.Clipboard);
    Check("push stores a frame and clears query",
        palette.Stack.Count == 1 && palette.Mode == PaletteMode.Clipboard && palette.Query == "" && palette.Selection == 0);
    Check("pop restores query and selection",
        palette.Pop() && palette.Mode == PaletteMode.Launcher && palette.Query == "clip" && palette.Selection == 2);
    palette.Prepare(PaletteMode.Emoji);
    Check("prepare becomes the root",
        palette.Stack.Count == 0 && palette.Mode == PaletteMode.Emoji && palette.Query == "");
    palette.Query = "foo";
    palette.PushCarryingQuery(PaletteMode.Clipboard);
    Check("pushCarryingQuery keeps the query", palette.Query == "foo" && palette.Mode == PaletteMode.Clipboard);
    palette.Replace(PaletteMode.Snippets);
    Check("replace keeps the stack", palette.Stack.Count == 1 && palette.Mode == PaletteMode.Snippets);
    Check("pop on root is false", !new PaletteState().Pop());
}

{
    Check("row index clamp empty", PaletteRowIndex.Clamp(4, 0) == 0);
    Check("row index clamp high", PaletteRowIndex.Clamp(9, 3) == 2);
    Check("row index move", PaletteRowIndex.Move(0, 1, 3) == 1);
    Check("row index move clamp", PaletteRowIndex.Move(2, 1, 3) == 2);
}

{
    Check("tab ring launcher to clipboard",
        PaletteTabRing.Next(PaletteMode.Launcher, clipboardEnabled: true, aiEnabled: false, backwards: false) == PaletteMode.Clipboard);
    Check("tab ring clipboard to launcher",
        PaletteTabRing.Next(PaletteMode.Clipboard, clipboardEnabled: true, aiEnabled: false, backwards: false) == PaletteMode.Launcher);
    Check("tab ring skips clipboard when off",
        PaletteTabRing.Next(PaletteMode.Launcher, clipboardEnabled: false, aiEnabled: false, backwards: false) == PaletteMode.Launcher);
    Check("tab ring from a side screen returns launcher",
        PaletteTabRing.Next(PaletteMode.Emoji, clipboardEnabled: true, aiEnabled: false, backwards: false) == PaletteMode.Launcher);
    Check("tab ring inserts AI when enabled",
        PaletteTabRing.Next(PaletteMode.Launcher, clipboardEnabled: true, aiEnabled: true, backwards: false) == PaletteMode.AiChat);
    Check("tab ring backwards from launcher with AI",
        PaletteTabRing.Next(PaletteMode.Launcher, clipboardEnabled: true, aiEnabled: true, backwards: true) == PaletteMode.Clipboard);
    Check("tab hint on launcher names clipboard",
        PaletteTabRing.Hint(PaletteMode.Launcher, clipboardEnabled: true, aiEnabled: false) == "Tab  Clipboard");
    Check("tab hint on clipboard names search",
        PaletteTabRing.Hint(PaletteMode.Clipboard, clipboardEnabled: true, aiEnabled: false) == "Tab  Search");
    Check("tab hint on launcher with AI names chat",
        PaletteTabRing.Hint(PaletteMode.Launcher, clipboardEnabled: true, aiEnabled: true) == "Tab  Chat");
}

{
    var uncovered = SettingsBackupCoverage.UncoveredKeys().ToArray();
    Check("every AppSettingsKey is in exactly one backup table", uncovered.Length == 0,
        string.Join(", ", uncovered));
    Check("snippets consent is excluded from backup",
        SettingsBackupCoverage.DeliberatelyExcluded.ContainsKey(AppSettingsKey.SnippetsEnabled));
    Check("ai consent is excluded from backup",
        SettingsBackupCoverage.DeliberatelyExcluded.ContainsKey(AppSettingsKey.AiEnabled));
    Check("calendar consent is excluded from backup",
        SettingsBackupCoverage.DeliberatelyExcluded.ContainsKey(AppSettingsKey.CalendarEnabled));
    Check("appearance is mirrored", SettingsBackupCoverage.Mirrored.Contains(AppSettingsKey.Appearance));
    Check("launch at login is externally sourced",
        SettingsBackupCoverage.ExternallySourced.Contains(AppSettingsKey.LaunchAtLogin));
}

{
    Check("panel radius 26", Theme.Radius.Panel == 26);
    Check("dialog radius 20", Theme.Radius.Dialog == 20);
    Check("panel width 750", Theme.Size.PanelWidth == 750);
    Check("panel height 475", Theme.Size.PanelHeight == 475);
    Check("clipboard list width 290", Theme.Size.ClipboardListWidth == 290);
    Check("clipboard media cap 260", Theme.Size.ClipboardMediaHeight == 260);
    Check("dark scrim 0.40", Theme.Colors.PanelScrim.Dark == 0.40);
    Check("light scrim 0.55", Theme.Colors.PanelScrim.Light == 0.55);
    Check("spacing ramp", Theme.Spacing.Xxs == 2 && Theme.Spacing.Xxl == 20);
    Check("standard metrics are identity", InterfaceMetrics.Standard.Space(Theme.Spacing.Md) == Theme.Spacing.Md);
}

{
    Check("P1 exact name stays above pool+usage", SearchRelevance.P1Holds);
    Check("P2 cell spacing leaves room for shape", SearchRelevance.P2Holds);
    Check("P3 usage can still outrank a pool-top cell", SearchRelevance.P3Holds);
    Check("usage ceiling stays below the protection floor",
        LauncherRankingStore.MaximumUsage < SearchRelevance.ProtectionFloor);
    var fields = new SearchFields(SearchAlias.Name("Visual Studio"), SearchAlias.Technical(@"C:\vs.exe"));
    Check("exact name outranks a prefix",
        SearchRelevance.Quality("visual studio", fields) > SearchRelevance.Quality("vis", fields));
    Check("technical subsequence is rejected",
        SearchRelevance.Quality("vse", new SearchFields(SearchAlias.Technical(@"C:\visualstudio.exe"))) is null);
    Check("user alias exact sits in the protected band",
        SearchRelevance.Cell(SearchRole.UserAlias, FuzzyTier.Exact) == 7_000);
    var usage = SearchRelevance.Total(SearchRelevance.Quality("vis", fields) ?? 0, LauncherRankingStore.MaximumUsage);
    var exact = SearchRelevance.Total(SearchRelevance.Quality("visual studio", fields) ?? 0, 0);
    Check("max frecency cannot overtake an exact name", exact > usage, $"{exact} vs {usage}");
    Check("foobar bar word-start", FuzzyMatcher.Match("bar", "foobar bar")?.Tier == FuzzyTier.WordStart);
}

{
    var now = new DateTime(2026, 9, 16, 15, 0, 0);
    Check("1000+1 copies without grouping", CalcEngine.Evaluate("1000+1", now)?.CopyText == "1001");
    Check("letters-only query is not a calculation", CalcEngine.Evaluate("settings", now) is null);
    Check("empty query is not a calculation", CalcEngine.Evaluate("   ", now) is null);
    Check("lone decimal is silent", CalcEngine.Evaluate("10", now) is null);
    Check("2+2", CalcEngine.Evaluate("2+2", now)?.CopyText == "4");
    Check("10k compact card", CalcEngine.Evaluate("10k", now)?.CopyText == "10000");
    Check("trailing binary keeps the prefix", CalcEngine.Evaluate("10+", now)?.CopyText == "10");
    Check("20% of 50", CalcEngine.Evaluate("20% of 50", now)?.CopyText == "10");
    Check("100 C in F", CalcEngine.Evaluate("100 C in F", now)?.CopyText.StartsWith("212") == true);
    var miles = CalcEngine.Evaluate("10km to mi", now);
    Check("10km to mi", miles is { IsError: false } && miles.CopyText.StartsWith("6.2", StringComparison.Ordinal));
    var kmToMi = CalcEngine.Evaluate("560 km to mi", now);
    Check("560 km to mi echoes the source", kmToMi?.Expression == "560 km");
    Check("560 km to mi display", kmToMi is { IsError: false } && kmToMi.Display.EndsWith(" mi", StringComparison.Ordinal));
    Check("560 km to mi badges", kmToMi?.SourceBadge == "Kilometers" && kmToMi?.TargetBadge == "Miles");
    var sum = CalcEngine.Evaluate("2+2", now);
    Check("2+2 is a two-column card", sum is { Expression: "2 + 2", Display: "4" });
    Check("10kg + 500g last unit wins", CalcEngine.Evaluate("10kg + 500g", now)?.Display == "10,500 g");
    Check("incompatible units error", CalcEngine.Evaluate("2 kg in F", now)?.IsError == true);
    Check("hex 0x10", CalcEngine.Evaluate("0x10", now)?.CopyText == "16");
    Check("now + 2h", CalcEngine.Evaluate("now + 2h", now)?.CopyText.StartsWith("2026-09-16T17:00") == true);
    var rates = new CurrencyRates(new Dictionary<string, double> { ["USD"] = 1, ["EUR"] = 0.5 }, DateTime.UtcNow);
    Check("currency conversion uses the store", CalcEngine.Evaluate("10 USD in EUR", now, rates)?.CopyText.StartsWith("5") == true);
    Check("missing rates stay non-actionable", CalcEngine.Evaluate("10 USD in EUR", now, null)?.IsActionable == false);
    Check("divide by zero is not a card", CalcEngine.Evaluate("1/0", now) is null);

    CalcResult? overflowWeeks = null;
    var weeksThrew = false;
    try { overflowWeeks = CalcEngine.Evaluate("monday in 2000000000 weeks", now); }
    catch { weeksThrew = true; }
    Check("overflow weeks does not throw", !weeksThrew && (overflowWeeks is null || overflowWeeks.IsError));

    CalcResult? overflowHours = null;
    var hoursThrew = false;
    try { overflowHours = CalcEngine.Evaluate("now + 9999999999 hours", now); }
    catch { hoursThrew = true; }
    Check("overflow hours does not throw", !hoursThrew && (overflowHours is null || overflowHours.IsError));

    CalcResult? hexOverflow = null;
    var hexThrew = false;
    try { hexOverflow = CalcEngine.Evaluate("0xFFFFFFFFFFFFFFFFF", now); }
    catch { hexThrew = true; }
    Check("hex overflow does not crash", !hexThrew && (hexOverflow is null || hexOverflow.IsError));

    Check("timezone invalid clock rejected", CalcEngine.Evaluate("25:99 to london", now) is null);
    var epoch = new CalcContext(new DateTime(1969, 12, 31, 23, 59, 58, 500), TimeZoneInfo.Utc);
    Check("negative ms floor", CalcEngine.Evaluate("now to unix", epoch)?.CopyText == "-2");
    Check("50% == 0.5 is true", CalcEngine.Evaluate("50% == 0.5", now)?.CopyText == "true");
    Check("1e19 to hex is error", CalcEngine.Evaluate("1e19 to hex", now)?.IsError == true);
    var ratioOverflow = CalcEngine.Evaluate("ratio of 99999999999999999999 to 1", now);
    Check("ratio overflow", ratioOverflow is null || ratioOverflow.IsError);
    var compactInf = CalcEngine.Evaluate("1e308k", now);
    Check("1e308k not Infinity", compactInf is null || compactInf.IsError);
    var mixedSum = CalcEngine.Evaluate("sum of 1, abc, 3", now);
    Check("sum of mixed list not silently 4", mixedSum is null || mixedSum.IsError);
    Check("sum of 1, 2, 3", CalcEngine.Evaluate("sum of 1, 2, 3", now)?.CopyText == "6");
    Check("0.3048m is 1 foot", CalcEngine.Evaluate("0.3048m", now)?.CopyText == "1 foot");
    Check("CompoundFeetInches carry", CalcFormatter.CompoundFeetInches(1 + 11.9 / 12) == "2 feet");
    Check("1m has no 12 inches", CalcEngine.Evaluate("1m", now) is { IsError: false } meter && !meter.Display.Contains("12 inches"));
    Check("2x3 is implicit multiply", CalcEngine.Evaluate("2x3", now)?.CopyText == "6");
    Check("1,23 is rejected", CalcEngine.Evaluate("1,23", now) is null || CalcEngine.Evaluate("1,23", now)!.IsError);
}

{
    var expanded = SnippetTemplateEngine.Expand("on {date} {cursor} done", new ExpansionContext { Now = new DateTime(2026, 1, 2, 3, 4, 5) });
    Check("snippet date token", expanded.Text.StartsWith("on 2026-01-02", StringComparison.Ordinal));
    Check("snippet cursor offset", expanded.CursorOffsetFromEnd == " done".Length);
    var missing = SnippetTemplateEngine.Expand("hi {argument name}", new ExpansionContext());
    Check("snippet missing argument is listed", missing.MissingArguments.Any(a => a.Name == "name"));
    var nested = SnippetTemplateEngine.Expand("{snippet:inner}", new ExpansionContext(), snippets:
    [
        new StoredSnippet("inner", "Inner", "in", "nested"),
    ]);
    Check("snippet nest", nested.Text == "nested");
    var loop = SnippetTemplateEngine.Expand("{snippet:a}", new ExpansionContext(), snippets:
    [
        new StoredSnippet("a", "A", "a", "{snippet:a}"),
    ]);
    Check("snippet cycle stops", loop.Text.Contains("{snippet:", StringComparison.Ordinal));
    var siblings = SnippetTemplateEngine.Expand("{snippet:a} {snippet:a}", new ExpansionContext(), snippets:
    [
        new StoredSnippet("a", "A", "a", "x"),
    ]);
    Check("sibling snippet expansion", siblings.Text == "x x");
    Check("nested braces", SnippetTemplateEngine.Expand("{argument name default=hello {world}}", new ExpansionContext()).Text == "hello {world}");
    Check("default=hello world",
        SnippetTemplateEngine.Expand("{argument name default=hello world}", new ExpansionContext()).Text == "hello world"
        && SnippetTemplateEngine.DeclaredArguments("{argument name default=hello world}").Count == 0);
}

{
    Check("zoom meeting", MeetingLink.Detect("join https://zoom.us/j/123456789")?.Provider == MeetingProvider.Zoom);
    Check("teams meeting", MeetingLink.Detect("https://teams.microsoft.com/l/meetup-join/abc")?.Provider == MeetingProvider.Teams);
    Check("generic https is last resort", MeetingLink.Detect("see https://example.com/meet/room")?.Provider == MeetingProvider.Generic);
    Check("non-url is ignored", MeetingLink.Detect("no link here") is null);
    Check("trailing punctuation stripped", MeetingLink.Detect("https://meet.google.com/abc-defg-hij.")?.Url.AbsolutePath == "/abc-defg-hij");
    var zoom = new MeetingLink(MeetingProvider.Zoom, new Uri("https://zoom.us/j/1"), null);
    var now = DateTime.Now;
    var standup = new MeetingEvent("m1", "Standup", now, now.AddMinutes(30), zoom, false);
    var joined = new HashSet<string>();
    Check("auto-join is due in the one-minute window",
        MeetingAutoJoin.IsDue(standup, now, now.AddMinutes(-5), joined));
    Check("auto-join skips all-day",
        !MeetingAutoJoin.IsDue(standup with { IsAllDay = true }, now, now.AddMinutes(-5), joined));
    Check("auto-join skips generic links",
        !MeetingAutoJoin.IsDue(standup with { Link = new MeetingLink(MeetingProvider.Generic, new Uri("https://example.com"), null) }, now, now.AddMinutes(-5), joined));
    joined.Add("m1");
    Check("auto-join skips already joined",
        !MeetingAutoJoin.IsDue(standup, now, now.AddMinutes(-5), joined));
}

{
    var detector = new DoubleTapDetector();
    var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    Check("first tap is not a fire",
        detector.Handle([DoubleTapModifier.Control], false, t0) is null
        && detector.Handle([], false, t0.AddMilliseconds(80)) is null);
    var fire = detector.Handle([DoubleTapModifier.Control], false, t0.AddMilliseconds(200));
    fire = detector.Handle([], false, t0.AddMilliseconds(280));
    Check("second control tap fires", fire == DoubleTapModifier.Control);
    detector.Reset();
    detector.Handle([DoubleTapModifier.Control], false, t0);
    detector.Handle([], false, t0.AddMilliseconds(80));
    var late = detector.Handle([DoubleTapModifier.Control], false, t0.AddSeconds(2));
    late = detector.Handle([], false, t0.AddSeconds(2.1));
    Check("late second tap does not fire", late is null);
    detector.Reset();
    Check("other modifiers invalidate",
        detector.Handle([DoubleTapModifier.Control], hasOtherModifiers: true, t0) is null);
}

{
    var a = new HotKeyBinding { CommandId = "one", Chord = new HotKeyChord(1, 0x20) };
    var b = new HotKeyBinding { CommandId = "two", Chord = new HotKeyChord(1, 0x20) };
    var c = new HotKeyBinding { CommandId = "three", Chord = new HotKeyChord(1, 0x20), AppPath = "notepad.exe" };
    Check("same chord conflicts", HotKeyConflicts.Find([a, b]).Count == 1);
    Check("global vs per-app conflict", HotKeyConflicts.Find([a, c]).Count == 1);
    var d = new HotKeyBinding { CommandId = "four", Chord = new HotKeyChord(1, 0x20), AppPath = "word.exe" };
    Check("distinct per-app chords do not conflict", HotKeyConflicts.Find([c, d]).Count == 0);
    Check("F1 label", new HotKeyChord(0, 0x70).Label == "F1");
    Check("hyper includes all four modifiers", HotKeyChord.Hyper(0x41).IsHyper);
}

{
    var screen = new ScreenSpec(0, new RectD(0, 0, 1000, 1000), new RectD(0, 0, 1000, 1000));
    var input = new PlacementInput
    {
        Command = "left-half",
        WindowFrame = new RectD(100, 100, 400, 300),
        Screens = [screen],
        Gap = 8,
    };
    var left = WindowPlacementEngine.PlacementFor(input);
    Check("left half tiles the leading half",
        left is { } p && p.Frame.X == 8 && p.Frame.Y == 8 && p.Frame.Width == 488 && p.Frame.Height == 984,
        left?.Frame.ToString());
    Check("35 window commands", WindowCommandCatalog.All.Count == 35);
    Check("no Stage Manager action", SystemActionCatalog.All.All(a => a.Id != "toggle-stage-manager"));
    Check("restore without a frame is a no-op",
        WindowPlacementEngine.PlacementFor(new PlacementInput
        {
            Command = "restore",
            WindowFrame = input.WindowFrame,
            Screens = input.Screens,
            Gap = 8,
        }) is null);
    var max = WindowPlacementEngine.PlacementFor(new PlacementInput
    {
        Command = "maximize",
        WindowFrame = input.WindowFrame,
        Screens = input.Screens,
        Gap = 8,
    });
    Check("maximize uses the gapped canvas", max is { } m && m.Frame.Width == 984 && m.Frame.Height == 984);
    var memory = new WindowActionMemory();
    var original = new RectD(10, 20, 300, 200);
    memory.Remember(1, original);
    Check("restore remembers the first frame", memory.Restore(1) == original);
    var step0 = memory.NextStep(1, "left-half");
    var last = memory.LastCommand(1);
    memory.NextStep(1, "next-display");
    Check("cycle starts at zero", step0 == 0);
    Check("last tile command is readable before the next command", last == "left-half");
    memory.Forget(1);
    Check("forget clears restore memory", memory.Restore(1) is null && memory.LastCommand(1) is null);
    var tight = new ScreenSpec(0, new RectD(0, 0, 400, 300), new RectD(0, 0, 400, 300));
    var vis = tight.VisibleFrame;
    var half = WindowPlacementEngine.PlacementFor(new PlacementInput
    {
        Command = "left-half",
        WindowFrame = new RectD(10, 10, 100, 80),
        Screens = [tight],
        Gap = -50,
    });
    Check("negative gap half stays on screen",
        half is { } tiled
        && tiled.Frame.MinX >= vis.MinX && tiled.Frame.MinY >= vis.MinY
        && tiled.Frame.MaxX <= vis.MaxX && tiled.Frame.MaxY <= vis.MaxY
        && tiled.Frame.Width > 0 && tiled.Frame.Height > 0,
        half?.Frame.ToString());
}

{
    Check("file search matches a substring", FileSearchQuery.Matches("readme.md", "read"));
    Check("file search is not a subsequence", !FileSearchQuery.Matches("readme.md", "rme"));
    Check("file search AND terms", FileSearchQuery.Matches("annual report.pdf", "annual report"));
    Check("file search AND requires every term", !FileSearchQuery.Matches("annual.pdf", "annual report"));
    Check("drive query D", FileBrowse.IsDriveQuery("D") && FileBrowse.IsDriveQuery("d:"));
    Check("volume query D:", FileBrowse.IsVolumeQuery("D:") && FileBrowse.IsVolumeQuery(@"D:\") && !FileBrowse.IsVolumeQuery(@"D:\Foo"));
    Check("drive root D:", FileBrowse.DriveRoot("D") == @"D:\" && FileBrowse.DriveRoot(@"D:\") == @"D:\");
    Check("parent of D:\\Foo is the volume", FileBrowse.ParentPath(@"D:\Foo") == @"D:\");
    Check("parent of a volume is null", FileBrowse.ParentPath(@"D:\") is null);
    var volumes = new[]
    {
        new FileSearchResult(@"C:\", "C:", null, true, true),
        new FileSearchResult(@"D:\", "D: · Data", null, true, true),
    };
    var dOnly = FileBrowse.FilterVolumes(volumes, "D");
    Check("filter volumes by letter", dOnly.Count == 1 && dOnly[0].Path == @"D:\");
    var ignore = new FileSearchIgnoreList(FileSearchIgnoreList.Defaults);
    Check("shipped ignore covers node_modules", ignore.Excludes(@"C:\src\node_modules\pkg\index.js"));
    var classIgnore = new FileSearchIgnoreList(["[abc].txt"]);
    Check("character-class glob",
        classIgnore.Excludes("a.txt") && classIgnore.Excludes("c.txt") && !classIgnore.Excludes("[abc].txt"));
    Check("dot component is structurally hidden", FileSearchQuery.IsExcludedPath(@"C:\Users\.git\config", ignore));
    Check("$ component is structurally hidden", FileSearchQuery.IsExcludedPath(@"D:\$Recycle.Bin\a", ignore));
    Check("images accept png", FileSearchFilter.Images.Accepts(@"C:\a.png", false));
    Check("folders filter keeps directories", FileSearchFilter.Folders.Accepts(@"C:\a", true) && !FileSearchFilter.Folders.Accepts(@"C:\a.png", false));
    Check("file search scopes are mirrored", SettingsBackupCoverage.Mirrored.Contains(AppSettingsKey.FileSearchScopes));
    Check("window gap is mirrored", SettingsBackupCoverage.Mirrored.Contains(AppSettingsKey.WindowGap));
}

{
    var identity = new UninstallIdentity("Relay", "com.relay.windows", []);
    Check("name match is exact and long enough", UninstallRules.MatchesName("Relay", identity) && !UninstallRules.MatchesName("Relay leftovers", identity));
    Check("short names do not match", !UninstallRules.MatchesName("ti", new UninstallIdentity("ti", null, [])));
    Check("windows folder is protected", UninstallRules.IsProtected(Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
    var relayData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Relay");
    Check("appdata relay is protected", UninstallRules.IsProtected(relayData));
}

{
    Check("quicklink http is a url", QuicklinkDestination.Classify("https://example.com") == QuicklinkKind.Url);
    Check("quicklink drive path", QuicklinkDestination.Classify(@"C:\Users") == QuicklinkKind.Path);
    Check("quicklink expand argument",
        QuicklinkDestination.Expand("https://example.com/q?s={argument}", "a b") == "https://example.com/q?s=a b");
    Check("quicklink encode query",
        QuicklinkDestination.Expand("https://example.com?q={query}", "a b") == "https://example.com?q=a%20b");
}

{
    var dir = Path.Combine(Path.GetTempPath(), "relay-harness-" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(dir);
    try
    {
        using var store = new ClipboardStore(dir);
        store.Insert(ClipboardKind.Text, "alpha bravo");
        store.Insert(ClipboardKind.Text, "charlie");
        Check("clipboard empty search returns newest first", store.Search("")[0].Text == "charlie");
        Check("clipboard fts finds a token", store.Search("alpha").Any(i => i.Text.Contains("alpha")));
        Check("punctuation-only query does not throw", store.Search("!!!").Count >= 0);
        Check("duplicate last text is detected", store.IsDuplicateText("charlie"));
        var first = store.Search("")[^1];
        store.TogglePin(first.Id);
        Check("pinned row sorts first", store.Search("")[0].Pinned);
        store.SetOcr(first.Id, "ocr-token");
        Check("ocr text is searchable", store.Search("ocr-token").Count >= 1);
        var imagePath = Path.Combine(store.ImageRoot, "clip.png");
        File.WriteAllBytes(imagePath, [137, 80, 78, 71]);
        var image = store.Insert(ClipboardKind.Image, "", imagePath, sourceId: @"C:\Program Files\Helium\helium.exe");
        Check("image list title is Image", image.Preview == "Image");
        Check("image source is stored", store.Get(image.Id)?.SourceId?.EndsWith("helium.exe") == true);
        store.Delete(image.Id);
        Check("delete removes the image file", !File.Exists(imagePath));
        store.Delete(first.Id);
        Check("delete removes the row", store.Get(first.Id) is null);
        var named = store.Insert(ClipboardKind.File, @"C:\Users\me\report.pdf", filePath: @"C:\Users\me\report.pdf");
        Check("file list title is the filename", named.Preview == "report.pdf");
        store.Insert(ClipboardKind.Text, "percent % sign");
        var wildcard = store.Search("%");
        Check("clipboard like escapes percent",
            wildcard.Count == 1 && wildcard[0].Text.Contains('%'),
            string.Join("|", wildcard.Select(i => i.Text)));
    }
    finally
    {
        try { Directory.Delete(dir, true); } catch (Exception) { }
    }
}

{
    var now = new DateTime(2026, 9, 17, 21, 29, 46);
    Check("today bucket", DateBuckets.From(now, now) == DateBucket.Today);
    Check("yesterday bucket", DateBuckets.From(now.AddDays(-1), now) == DateBucket.Yesterday);
    Check("earlier bucket", DateBuckets.From(now.AddYears(-1), now) == DateBucket.Earlier);
    Check("new year week bucket",
        DateBuckets.From(new DateTime(2025, 12, 31), new DateTime(2026, 1, 2)) == DateBucket.ThisWeek);
    Check("copied today label", ClipboardPresentation.CopiedLabel(now, now).StartsWith("Today at ", StringComparison.Ordinal));
    Check("file size 1.7 MB", ClipboardPresentation.FileSizeLabel(1_700_000) == "1.7 MB");
    Check("word count", ClipboardPresentation.WordCount("one two  three") == 3);
    Check("source title from path", ClipboardPresentation.SourceTitle(@"C:\Apps\Helium\helium.exe") == "helium");
    var png = new byte[]
    {
        137, 80, 78, 71, 13, 10, 26, 10,
        0, 0, 0, 13,
        73, 72, 68, 82,
        0, 0, 8, 100,
        0, 0, 5, 22,
    };
    var size = ClipboardPresentation.PngPixelSize(png);
    Check("png dimensions", size == (2148, 1302), size?.ToString());
    Check("files outrank bitmap", ClipboardCapture.Decide(true, true, true, "name.png") == ClipboardKind.File);
    Check("bitmap without text is image", ClipboardCapture.Decide(false, true, false, null) == ClipboardKind.Image);
    Check("url beside bitmap is image", ClipboardCapture.Decide(false, true, true, "https://example.com/a.png") == ClipboardKind.Image);
    Check("prose beside bitmap stays text", ClipboardCapture.Decide(false, true, true, "A full sentence of copied prose from Word") == ClipboardKind.Text);
    Check("volatile missing path", ClipboardCapture.IsVolatilePath(@"C:\nowhere\missing.png"));
}

{
    var rankingDir = Path.Combine(Path.GetTempPath(), "relay-rank-" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(rankingDir);
    var file = Path.Combine(rankingDir, "ranking.json");
    try
    {
        var store = new LauncherRankingStore(file, () => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        store.Record("app:one", "vis");
        store.Record("app:one", "vis");
        store.Record("app:one", "");
        Check("empty query is not recorded", store.Records.All(r => r.SubmittedQuery.Length > 0));
        Check("usage is query-prefixed", store.Usage("vi").ContainsKey("app:one"));
        Check("unrelated query has no usage", store.Usage("zzz").Count == 0);
        store.Reset("app:one");
        Check("reset clears that key", store.Records.Count == 0);
    }
    finally
    {
        try { Directory.Delete(rankingDir, true); } catch (Exception) { }
    }
}

{
    var dir = Path.Combine(Path.GetTempPath(), "relay-rank-ui-" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(dir);
    try
    {
        var visibility = new VisibilityStore(Path.Combine(dir, "vis.json"));
        var aliases = new AliasStore(Path.Combine(dir, "alias.json"));
        aliases.Set("app:code", "vs");
        visibility.Set("app:hidden", true);
        var settings = new AppSettings { QuicklinksEnabled = true, SnippetsEnabled = true, CustomCommandsEnabled = true };
        var entries = new[]
        {
            new AppEntry("app:code", "Visual Studio Code", AppEntryKind.Application, null, null, null, new SearchFields(SearchAlias.Name("Visual Studio Code"))),
            new AppEntry("app:hidden", "Hidden App", AppEntryKind.Application, null, null, null, new SearchFields(SearchAlias.Name("Hidden App"))),
        };
        var ranked = LauncherOrder.Rank(entries, "vs", new Dictionary<string, int>(), aliases.Items, visibility.IsHidden, settings, visibility);
        Check("user alias finds the app", ranked.Any(e => e.Id == "app:code"));
        var empty = LauncherOrder.Rank(entries, "", new Dictionary<string, int>(), aliases.Items, visibility.IsHidden, settings, visibility);
        Check("hidden apps are omitted", empty.All(e => e.Id != "app:hidden") && empty.Any(e => e.Id == "app:code"));
        var aliasFields = new SearchFields(SearchAlias.Name("Visual Studio Code"));
        var aliasEntry = new AppEntry("app:code", "Visual Studio Code", AppEntryKind.Application, null, null, null, aliasFields);
        LauncherOrder.Rank([aliasEntry], "vs", new Dictionary<string, int>(), aliases.Items, _ => false, settings, visibility);
        LauncherOrder.Rank([aliasEntry], "vs", new Dictionary<string, int>(), aliases.Items, _ => false, settings, visibility);
        Check("ranking does not mutate live search fields",
            aliasFields.Aliases.Count(a => a.Role == SearchRole.UserAlias) == 0);
        settings.SnippetsEnabled = false;
        var snippets = new[]
        {
            new AppEntry("snippet:x", "Sig", AppEntryKind.Snippet, null, null, null, new SearchFields(SearchAlias.Name("Sig"))),
        };
        Check("disabled snippet kind is omitted",
            LauncherOrder.Rank(snippets, "sig", new Dictionary<string, int>(), aliases.Items, _ => false, settings, visibility).Count == 0);
    }
    finally
    {
        try { Directory.Delete(dir, true); } catch (Exception) { }
    }
}

{
    var settings = new AppSettings { Appearance = AppAppearance.Light, AiEnabled = true, ShowInTray = false };
    var captured = SettingsSnapshot.Capture(settings);
    Check("snapshot still names excluded keys", captured.ContainsKey(AppSettingsKey.AiEnabled));
    var zip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n") + ".relay");
    try
    {
        BackupArchive.Write(zip, [BackupArchive.SettingsAndShortcuts], captured, null, null, null, null);
        var restored = BackupArchive.ReadSettings(zip);
        Check("archive drops AI consent", !restored.ContainsKey(AppSettingsKey.AiEnabled));
        Check("archive keeps appearance", restored[AppSettingsKey.Appearance] == "Light");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Update))
        {
            var entry = archive.GetEntry("settings.json");
            entry?.Delete();
            var tamper = archive.CreateEntry("settings.json");
            using var writer = new StreamWriter(tamper.Open());
            writer.Write(JsonSerializer.Serialize(new Dictionary<string, string>
            {
                [AppSettingsKey.AiEnabled] = "true",
                [AppSettingsKey.ShowInTray] = "true",
            }));
        }

        Check("archive can round-trip snippets",
            BackupArchive.ReadEntryText(zip, "settings.json") is not null);

        var target = new AppSettings { AiEnabled = false, ShowInTray = false };
        SettingsSnapshot.ApplyMirrored(target, BackupArchive.ReadSettings(zip));
        Check("import cannot enable AI", target.AiEnabled == false);
        Check("import applies mirrored tray flag", target.ShowInTray);
    }
    finally
    {
        if (File.Exists(zip))
            File.Delete(zip);
    }
}

{
    Check("opencode slug parses provider/model",
        AiModelRef.ParseSlug("openai/gpt-5") is { ProviderId: "openai", ModelId: "gpt-5" });
    Check("opencode slug rejects bare id", AiModelRef.ParseSlug("gpt-5") is null);
    Check("opencode slug rejects empty", AiModelRef.ParseSlug("openai/") is null);
    Check("opencode version gate accepts minimum",
        OpenCodeInventory.IsVersionSupported(OpenCodeConstants.MinimumVersion));
    Check("opencode version gate rejects old", !OpenCodeInventory.IsVersionSupported("1.13.0"));
    Check("opencode version gate rejects garbage", !OpenCodeInventory.IsVersionSupported("latest"));
    Check("opencode semver compares", OpenCodeInventory.CompareSemver("1.15.13", "1.14.19") > 0);
    Check("opencode server url parses readiness prefix",
        OpenCodeInventory.ParseServerUrlFromOutput("opencode server listening on http://127.0.0.1:4096") == "http://127.0.0.1:4096");
    Check("opencode server url ignores noise", OpenCodeInventory.ParseServerUrlFromOutput("starting…") is null);
    var providers = new[]
    {
        new OpenCodeInventory.ProviderEntry("openai", "OpenAI", new Dictionary<string, OpenCodeInventory.ProviderModel>
        {
            ["gpt-5"] = new("gpt-5", "GPT 5"),
            ["empty"] = new("empty", "  "),
        }),
        new OpenCodeInventory.ProviderEntry("anthropic", "Anthropic", new Dictionary<string, OpenCodeInventory.ProviderModel>
        {
            ["claude"] = new("claude", "Claude"),
        }),
    };
    var flat = OpenCodeInventory.Flatten(providers, ["openai"]);
    Check("opencode flatten keeps only connected", flat.Count == 1 && flat[0].Slug == "openai/gpt-5");
    Check("opencode flatten skips empty names", flat.All(m => !string.IsNullOrWhiteSpace(m.DisplayName)));
    Check("opencode config content defaults to {}",
        OpenCodeInventory.ResolveConfigContent(null, new Dictionary<string, string?>()) == "{}");
    Check("opencode config content respects explicit",
        OpenCodeInventory.ResolveConfigContent(
            new Dictionary<string, string?> { ["OPENCODE_CONFIG_CONTENT"] = """{"x":1}""" },
            new Dictionary<string, string?>()) == """{"x":1}""");
    Check("opencode external password is not forwarded",
        OpenCodeInventory.ResolveServerPassword(true, null,
            new Dictionary<string, string?> { ["OPENCODE_SERVER_PASSWORD"] = "pw" }) is null);
    Check("opencode local password inherits env",
        OpenCodeInventory.ResolveServerPassword(false, null,
            new Dictionary<string, string?> { ["OPENCODE_SERVER_PASSWORD"] = "pw" }) == "pw");
    Check("opencode explicit password wins",
        OpenCodeInventory.ResolveServerPassword(false, "explicit",
            new Dictionary<string, string?> { ["OPENCODE_SERVER_PASSWORD"] = "env" }) == "explicit");
    Check("opencode basic auth header",
        OpenCodeInventory.BasicAuthHeader("secret") == "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("opencode:secret")));
}

{
    var coreDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Relay.Core"));
    if (!Directory.Exists(coreDir))
        coreDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "Relay.Core"));
    Check("core project exists", Directory.Exists(coreDir), coreDir);
    if (Directory.Exists(coreDir))
    {
        var leaks = Directory.EnumerateFiles(coreDir, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadAllLines(path).Select((line, i) => (path, i, line)))
            .Where(x => x.line.Contains("Microsoft.UI", StringComparison.Ordinal)
                        || x.line.Contains("WinRT", StringComparison.Ordinal)
                        || x.line.Contains("DllImport", StringComparison.Ordinal))
            .Select(x => $"{Path.GetRelativePath(coreDir, x.path)}:{x.i + 1}")
            .ToArray();
        Check("Relay.Core has no WinUI or P/Invoke", leaks.Length == 0, string.Join(", ", leaks));
    }
}

{
    Check("update display version drops git suffix", UpdateRelease.DisplayVersion("0.3.2+c13e437") == "0.3.2");
    Check("update tag drops v", UpdateRelease.ParseTag("v0.2.0") == new Version(0, 2, 0));
    Check("update tag ignores prerelease suffix", UpdateRelease.ParseTag("v1.0.0-beta.1") == new Version(1, 0, 0));
    Check("update zip prefers x64", UpdateRelease.PickAsset(["notes.txt", "Relay-windows-x64.zip"], System.Runtime.InteropServices.Architecture.X64) == "Relay-windows-x64.zip");
    Check("update notes drop install marker",
        UpdateRelease.NotesSummary("Fixed search.\n\n<!-- relay:install -->\nUnzip me") == "Fixed search.");
    var listings = new UpdateRelease.ReleaseListing[]
    {
        new("v0.1.0", new Version(0, 1, 0), false, false, ["Relay-windows-x64.zip"]),
        new("v0.2.0", new Version(0, 2, 0), false, false, ["Relay-windows-x64.zip"]),
        new("v0.3.0", new Version(0, 3, 0), true, false, ["Relay-windows-x64.zip"]),
        new("v0.2.1", new Version(0, 2, 1), false, false, ["notes.txt"]),
    };
    Check("update latest skips drafts and missing zips",
        UpdateRelease.SelectLatest(listings, System.Runtime.InteropServices.Architecture.X64)?.Tag == "v0.2.0");
    var env = DotEnv.Parse("""
        # comment
        export RELAY_GITHUB_TOKEN="ghp_example"
        GITHUB_TOKEN=ignored # trailing
        """);
    Check("dotenv reads quoted token", env["RELAY_GITHUB_TOKEN"] == "ghp_example");
    var quotedEnv = DotEnv.Parse("""
        KEY="value" # comment
        HASH="hash # inside"
        PLAIN=plain # comment
        """);
    Check("dotenv quoted value drops trailing comment", quotedEnv["KEY"] == "value");
    Check("dotenv keeps hash inside quotes", quotedEnv["HASH"] == "hash # inside");
    Check("dotenv plain trailing comment", quotedEnv["PLAIN"] == "plain");
    Check("github token strips bearer", GitHubToken.Sanitize("Bearer ghp_example") == "ghp_example");
    Check("github token rejects blank", GitHubToken.Sanitize("  ") is null);
}

{
    Check("settings search finds ocr on clipboard", SettingsCatalog.Search("ocr").Any(p => p.Tab == SettingsTab.Clipboard));
    Check("settings has no apple shortcuts pane", SettingsCatalog.Panes.All(p => p.Title != "Apple Shortcuts"));
    Check("url fallback detects host", FallbackCatalog.LooksLikeUrl("github.com"));
    Check("url fallback rejects words", !FallbackCatalog.LooksLikeUrl("open notepad"));
    Check("https:// is not a url", !FallbackCatalog.LooksLikeUrl("https://"));
    Check("installed apps settings uri is unique",
        MsSettingsCatalog.All.Count(e => e.Uri == "ms-settings:appsfeatures") == 1
        && MsSettingsCatalog.All.Any(e => e.Title == "Installed apps" && e.Uri == "ms-settings:installedapps"));
    Check("clipboard filter cycles", ClipboardListFilterLogic.Next(ClipboardListFilter.All) == ClipboardListFilter.Text);
    Check("clipboard scheme-only is not a link", !ClipboardListFilterLogic.IsLink("https://"));
    Check("clipboard mailto-only is not a link", !ClipboardListFilterLogic.IsLink("mailto:"));
    Check("clipboard link match", ClipboardListFilterLogic.IsLink("https://example.com"));
    var join = MeetingJoinCard.NextJoinable(
        [new MeetingEvent("1", "Standup", DateTime.Now.AddMinutes(10), DateTime.Now.AddMinutes(40), new MeetingLink(MeetingProvider.Zoom, new Uri("https://zoom.us/j/1"), null), false)],
        DateTime.Now);
    Check("join card in two-hour window", join?.Id == "1");
    Check("emoji catalog is a grid set", EmojiCatalog.All.Count >= 80);
    var merged = FallbackCatalog.Merge(
        [new FallbackSpec { Id = FallbackCatalog.Shell, Enabled = false }, new FallbackSpec { Id = FallbackCatalog.Ai, Enabled = true }],
        []);
    Check("fallback merge keeps disable", merged.First(f => f.Id == FallbackCatalog.Shell).Enabled == false);
    Check("clipboard color parses hex", ClipboardColor.TryParse("#0a0", out var c) && c.Hex == "#00AA00");
    Check("clipboard policy ignores app", ClipboardPolicy.IsIgnored("C:\\Apps\\Slack.exe", ["Slack"]));
    Check("clipboard policy retention", ClipboardPolicy.RetentionCutoff(90, new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc)) == new DateTime(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc));
    Check("escape clears query first", PaletteEscape.ClearsQueryFirst("foo", true) && !PaletteEscape.ClearsQueryFirst("", true));
    Check("bare digit types into empty query", !PaletteDigit.ActivatesSlot("", control: false, alt: false));
    Check("ctrl digit activates empty-query slot", PaletteDigit.ActivatesSlot("", control: true, alt: false));
    Check("ctrl digit does not steal a typed query", !PaletteDigit.ActivatesSlot("8", control: true, alt: false));
    Check("alt digit is not a slot", !PaletteDigit.ActivatesSlot("", control: true, alt: true));
    Check("volume steps 50 percent is 25 ups", VolumeSteps.UpsFromPercent(50) == 25);
    Check("emoji skin tints clap", EmojiSkin.Apply("👏", 3) != "👏");
    var md = SnippetFrontmatter.Parse("sig.md", "---\nname: Sign-off\nkeyword: sig\n---\nThanks,\n{date}\n");
    Check("snippet frontmatter name", md?.Name == "Sign-off" && md.Keyword == "sig");
    Check("snippet conflict keywords", SnippetFrontmatter.ConflictingKeywords(
        [new StoredSnippet("a", "A", "x", "1"), new StoredSnippet("b", "B", "x", "2")]).Contains("x"));
    Check("quicklink expands clipboard token", QuicklinkDestination.Expand("https://q/?q={clipboard}", "", "", "hi") == "https://q/?q=hi");
    Check("backup rejects future schema", BackupArchive.IncompatibleReason(new BackupManifest("99", DateTime.UtcNow, [])) is not null);
    Check("calc color card", CalcColor.Evaluate("#ff00aa")?.CopyText == "#FF00AA");
    Check("palette compact size is smaller", Theme.Size.PalettePanel("standard", true).Width < Theme.Size.PanelWidth);
    Check("uninstall leftover labels megabytes", UninstallLeftoverLogic.SizeLabel(1_500_000).Contains("MB"));
    Check("update notes strip marker", UpdateRelease.NotesSummary("Hello\n<!-- relay:install -->\ninstall") == "Hello");
}

void Regression(string name, Action test)
{
    try { test(); }
    catch (Exception ex) { Check(name, false, ex.GetType().Name + ": " + ex.Message); }
}

bool Throws<T>(Action action) where T : Exception
{
    try { action(); return false; }
    catch (T) { return true; }
    catch (Exception) { return false; }
}

Regression("backup atomic export", () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "relay-atomic-" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(dir);
    try
    {
        var zip = Path.Combine(dir, "backup.relay");
        var notes = Path.Combine(dir, "notes");
        Directory.CreateDirectory(notes);
        var note = Path.Combine(notes, "locked.md");
        File.WriteAllText(note, "note content");
        BackupArchive.Write(zip, [BackupArchive.Snippets], new Dictionary<string, string>(), "original", null, null, null);
        var original = File.ReadAllBytes(zip);
        using (var locked = new FileStream(note, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Check("export reports unreadable source", Throws<IOException>(() =>
                BackupArchive.Write(zip, [BackupArchive.Notes], new Dictionary<string, string>(), null, notes, null, null)));
            Check("failed export preserves previous bytes", File.ReadAllBytes(zip).SequenceEqual(original));
            var fresh = Path.Combine(dir, "fresh.relay");
            Check("new export reports unreadable source", Throws<IOException>(() =>
                BackupArchive.Write(fresh, [BackupArchive.Notes], new Dictionary<string, string>(), null, notes, null, null)));
            Check("failed new export leaves no archive", !File.Exists(fresh));
        }
        BackupArchive.Write(zip, [BackupArchive.Notes], new Dictionary<string, string>(), null, notes, null, null);
        Check("successful export replaces with complete archive", BackupArchive.ReadEntryText(zip, "notes/locked.md") == "note content"
            && BackupArchive.ReadEntryText(zip, "snippets.json") is null);
        Check("exports clean up staging files", Directory.GetFiles(dir).Length == 1);
    }
    finally { Directory.Delete(dir, true); }
});

Regression("clipboard FTS mutations", () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "relay-fts-" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(dir);
    try
    {
        using var store = new ClipboardStore(dir);
        using var db = new SqliteConnection($"Data Source={Path.Combine(dir, "clipboard.sqlite")};Pooling=False");
        db.Open();
        void Sql(string text)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = text;
            cmd.ExecuteNonQuery();
        }
        bool Integrity() => !Throws<SqliteException>(() => Sql("INSERT INTO items_fts(items_fts, rank) VALUES('integrity-check', 1)"));
        var item = store.Insert(ClipboardKind.Text, "originalword");
        Check("clipboard FTS handles operator words and whitespace", store.Search("AND\tOR\nNOT").Count == 0);
        Check("insert FTS agrees with backing columns", Integrity());
        store.SetOcr(item.Id, "oldocrword");
        Check("OCR FTS agrees with backing columns", Integrity());
        store.SetOcr(item.Id, "newocrword");
        Check("OCR replacement removes old search tokens", store.Search("oldocrword").Count == 0);
        Check("OCR replacement keeps original and new tokens", store.Search("originalword").Count == 1 && store.Search("newocrword").Count == 1);
        store.SetOcr(item.Id, "");
        Check("empty OCR removes previous tokens", store.Search("newocrword").Count == 0 && Integrity());
        store.Delete(item.Id);
        store.Delete(item.Id);
        store.SetOcr(item.Id, "ghostword");
        Check("delete and late OCR leave no index debris", Integrity() && store.Search("").Count == 0);
        var image = Path.Combine(store.ImageRoot, "retained.png");
        File.WriteAllBytes(image, [1, 2, 3]);
        var retained = store.Insert(ClipboardKind.Image, "retainedword", image);
        Sql("DROP TABLE items_fts");
        Check("index failure is surfaced on insert", Throws<SqliteException>(() => store.Insert(ClipboardKind.Text, "rollbackword")));
        Check("failed insert rolls back backing row", store.Search("").Count == 1);
        Check("index failure is surfaced on OCR", Throws<SqliteException>(() => store.SetOcr(retained.Id, "rollbackocr")));
        Check("failed OCR preserves backing text", store.Get(retained.Id)?.OcrText is null);
        Check("index failure is surfaced on delete", Throws<SqliteException>(() => store.Delete(retained.Id)));
        Check("failed delete preserves row and blob", store.Get(retained.Id) is not null && File.Exists(image));
        Check("search does not hide index errors", Throws<SqliteException>(() => store.Search("retainedword")));
    }
    finally { Directory.Delete(dir, true); }
});

foreach (var legacyOperation in new[] { "OCR", "delete" })
{
    Regression("clipboard repairs legacy " + legacyOperation, () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "relay-legacy-fts-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        try
        {
            long retainedId;
            long deletedId;
            using (var original = new ClipboardStore(dir))
            {
                retainedId = original.Insert(ClipboardKind.Text, "originalword", sourceId: "legacy-source").Id;
                original.TogglePin(retainedId);
                deletedId = original.Insert(ClipboardKind.Text, "deletedword").Id;
            }
            var connectionString = $"Data Source={Path.Combine(dir, "clipboard.sqlite")};Pooling=False";
            void Sql(string text)
            {
                using var db = new SqliteConnection(connectionString);
                db.Open();
                using var cmd = db.CreateCommand();
                cmd.CommandText = text;
                cmd.ExecuteNonQuery();
            }
            bool Integrity()
            {
                try
                {
                    Sql("INSERT INTO items_fts(items_fts, rank) VALUES('integrity-check', 1)");
                    return true;
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 11)
                {
                    return false;
                }
            }
            Check("legacy " + legacyOperation + " fixture starts consistent", Integrity());
            if (legacyOperation == "OCR")
            {
                Sql($"UPDATE items SET ocr_text = 'legacyocrword' WHERE id = {retainedId}");
                try { Sql($"DELETE FROM items_fts WHERE rowid = {retainedId}"); }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 11) { }
                Sql($"INSERT INTO items_fts(rowid, text, ocr_text) VALUES ({retainedId}, 'originalword legacyocrword', 'legacyocrword')");
            }
            else
            {
                Sql($"DELETE FROM items WHERE id = {deletedId}");
                try { Sql($"DELETE FROM items_fts WHERE rowid = {deletedId}"); }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 11) { }
            }
            Check("legacy " + legacyOperation + " fixture has inconsistent external content", !Integrity());
            using (var reopened = new ClipboardStore(dir))
            {
                Check("reopen repairs legacy " + legacyOperation + " FTS", Integrity());
                Check("legacy " + legacyOperation + " repair preserves row metadata",
                    reopened.Get(retainedId) is { Text: "originalword", Pinned: true, SourceId: "legacy-source" });
                Check("legacy " + legacyOperation + " repaired text is searchable",
                    reopened.Search("originalword").Single().Id == retainedId);
                if (legacyOperation == "OCR")
                    Check("legacy OCR remains searchable after repair",
                        reopened.Search("legacyocrword originalword").Single().Id == retainedId);
                else
                    Check("legacy deleted row stays deleted", reopened.Get(deletedId) is null && reopened.Search("deletedword").Count == 0);
                reopened.SetOcr(retainedId, "replacementocrword");
                Check("legacy " + legacyOperation + " repaired index supports OCR replacement",
                    reopened.Search("replacementocrword originalword").Single().Id == retainedId
                    && reopened.Search("legacyocrword").Count == 0 && Integrity());
                reopened.Delete(retainedId);
                Check("legacy " + legacyOperation + " repaired index supports delete",
                    reopened.Get(retainedId) is null && reopened.Search("replacementocrword").Count == 0
                    && reopened.Search("originalword").Count == 0 && Integrity());
            }
            using var healthy = new ClipboardStore(dir);
            Check("legacy " + legacyOperation + " repair survives another reopen", Integrity());
        }
        finally { Directory.Delete(dir, true); }
    });
}

Regression("ranking file validation", () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "relay-rank-validation-" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(dir);
    try
    {
        var file = Path.Combine(dir, "ranking.json");
        foreach (var json in new[] { "null", "[null]", "[{\"ItemKey\":null,\"SubmittedQuery\":\"a\",\"Count\":1}]", "[{\"ItemKey\":\"a\",\"SubmittedQuery\":null,\"Count\":1}]", "{broken", "{}" })
        {
            Regression("ranking tolerates " + json, () =>
            {
                File.WriteAllText(file, json);
                var loaded = new LauncherRankingStore(file);
                Check("invalid ranking file yields no usage: " + json, loaded.Usage("a").Count == 0);
            });
        }
        var now = new DateTime(2026, 1, 1);
        var records = Enumerable.Range(0, LauncherRankingStore.Cap + 5).Select(i => new LauncherRankingRecord
        {
            ItemKey = "app:" + i, SubmittedQuery = " Query ", Count = i + 1, LastUsed = now,
        }).ToList();
        File.WriteAllText(file, JsonSerializer.Serialize(records));
        var store = new LauncherRankingStore(file, () => now);
        Check("load applies ranking cap and normalization", store.Records.Count == LauncherRankingStore.Cap
            && store.Records.All(r => r.SubmittedQuery == "query") && store.Records.All(r => r.Count > 5));
        store.ReplaceAll(records);
        Check("import applies ranking cap", store.Records.Count == LauncherRankingStore.Cap);
        Regression("ranking null import collection", () =>
        {
            store.ReplaceAll(null!);
            Check("null import collection is empty", store.Records.Count == 0);
        });
        Regression("ranking malformed import records", () =>
        {
            store.ReplaceAll([null!, new() { ItemKey = null!, Count = 1, SubmittedQuery = "a" },
                new() { ItemKey = "app", Count = 1, SubmittedQuery = new string('a', 65) },
                new() { ItemKey = "app", Count = 1, SubmittedQuery = "   " },
                new() { ItemKey = "app", Count = 1, SubmittedQuery = " VALID " }]);
            Check("import keeps only valid normalized records", store.Records.Count == 1 && store.Usage("valid").Count == 1);
        });
        store.ReplaceAll([
            new() { ItemKey = "app", SubmittedQuery = "a", Count = int.MaxValue, LastUsed = now },
            new() { ItemKey = "app", SubmittedQuery = "ab", Count = int.MaxValue, LastUsed = now },
            new() { ItemKey = "other", SubmittedQuery = "a", Count = int.MaxValue, LastUsed = now },
        ]);
        Regression("ranking usage overflow", () =>
        {
            var usage = store.Usage("a");
            Check("large count sums produce bounded positive scores", usage.Count == 2
                && usage.Values.All(v => v > 0 && v <= LauncherRankingStore.MaximumUsage));
        });
        store.Record("app", "a");
        Check("record saturates count without wrapping", store.Records.First(r => r.ItemKey == "app" && r.SubmittedQuery == "a").Count == int.MaxValue);
        Check("score handles maximum count", LauncherRankingStore.Score(int.MaxValue, now, 1, now) > 0);
        store.ReplaceAll([
            new() { ItemKey = "app", SubmittedQuery = "a", Count = int.MaxValue, LastUsed = now },
            new() { ItemKey = "other", SubmittedQuery = "a", Count = int.MaxValue, LastUsed = now },
        ]);
        var reopened = new LauncherRankingStore(file, () => now);
        Check("ranking total across items cannot overflow after restart", reopened.Usage("a").Values.All(v => v > 0));
        var before = File.ReadAllText(file);
        var revision = store.Revision;
        IEnumerable<LauncherRankingRecord> InterruptedImport()
        {
            yield return new() { ItemKey = "replacement", SubmittedQuery = "query", Count = 1 };
            throw new InvalidDataException("Interrupted payload");
        }
        Check("interrupted ranking import is rejected", Throws<InvalidDataException>(() => store.ReplaceAll(InterruptedImport())));
        Check("interrupted ranking import leaves memory and disk unchanged", store.Revision == revision
            && store.Records.Count == 2 && store.Usage("a").Count == 2 && File.ReadAllText(file) == before);
        var aliasFile = Path.Combine(dir, "aliases.json");
        File.WriteAllText(aliasFile, """{"app":null,"APP:VALID":"alias"}""");
        var aliases = new AliasStore(aliasFile);
        Check("loaded aliases discard null values and retain case-insensitive lookup", !aliases.Items.ContainsKey("app")
            && aliases.Get("app:valid") == "alias");
    }
    finally { Directory.Delete(dir, true); }
});

Regression("settings prevalidation", () =>
{
    var dir = Path.Combine(Path.GetTempPath(), "relay-settings-validation-" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(dir);
    try
    {
        var zip = Path.Combine(dir, "invalid.relay");
        foreach (var invalid in new (string Key, string? Value)[]
        {
            (AppSettingsKey.FileSearchScopes, null), (AppSettingsKey.InterfaceSize, null),
            (AppSettingsKey.ClipboardEnabled, "perhaps"), (AppSettingsKey.WindowGap, "overflow9999999999999"),
            (AppSettingsKey.Appearance, "999"), (AppSettingsKey.PaletteLeft, "NaN"),
        })
        {
            var settings = new AppSettings { ShowInTray = false, FileSearchScopes = ["original"] };
            var before = JsonSerializer.Serialize(settings);
            BackupArchive.Write(zip, [BackupArchive.SettingsAndShortcuts], new Dictionary<string, string>
            {
                [AppSettingsKey.ShowInTray] = "true", [invalid.Key] = invalid.Value!,
            }, null, null, null, null);
            Check("invalid mirrored value rejected: " + invalid.Key, Throws<InvalidDataException>(() =>
                SettingsSnapshot.ApplyMirrored(settings, BackupArchive.ReadSettings(zip))));
            Check("invalid payload applies nothing: " + invalid.Key, JsonSerializer.Serialize(settings) == before);
        }
        var target = new AppSettings();
        var excluded = SettingsBackupCoverage.DeliberatelyExcluded.Keys.ToDictionary(key => key, _ => "true");
        SettingsSnapshot.ApplyMirrored(target, excluded);
        Check("all capability exclusions survive import", SettingsSnapshot.Capture(target)
            .Where(kv => excluded.ContainsKey(kv.Key)).All(kv => kv.Value == "false"));
        SettingsSnapshot.ApplyMirrored(target, new Dictionary<string, string>
        {
            [AppSettingsKey.ShowInTray] = "0", [AppSettingsKey.FileSearchScopes] = "one\ntwo|three",
            [AppSettingsKey.WindowGap] = "999", [AppSettingsKey.PaletteLeft] = "12.5",
            [AppSettingsKey.AiEnabled] = null!,
        });
        Check("valid settings retain clamping and list parsing", !target.ShowInTray && target.WindowGap == 64
            && target.FileSearchScopes.SequenceEqual(new[] { "one", "two", "three" }) && target.PaletteLeft == 12.5 && !target.AiEnabled);
    }
    finally { Directory.Delete(dir, true); }
});

{
    var defaults = new AppSettings();
    Check("clipboard stays on by default", defaults.ClipboardEnabled);
    Check("file search ships off", !defaults.FileSearchEnabled);
    Check("window management ships off", !defaults.WindowManagementEnabled);
    Check("navigation ships off", !defaults.NavigationEnabled);
    Check("custom commands ship off", !defaults.CustomCommandsEnabled);
    Check("notes ship off", !defaults.NotesEnabled);
    Check("snippets ship off", !defaults.SnippetsEnabled);
    Check("quicklinks ship off", !defaults.QuicklinksEnabled);
}

{
    Check("emoji catalog is generated", EmojiCatalog.All.Count > 1500);
    var birthday = EmojiSearch.Rank(EmojiCatalog.All, "birthday", 0, [], [], null);
    Check("birthday ranks cake first", birthday.Count > 0 && birthday[0].Glyph == "🎂", birthday.FirstOrDefault()?.Name);
    var plusOne = EmojiSearch.Rank(EmojiCatalog.All, ":+1:", 0, [], [], null);
    Check("colon query unwraps +1", plusOne.Count > 0 && plusOne[0].Glyph == "👍", plusOne.FirstOrDefault()?.Name);
    var pray = EmojiSearch.Rank(EmojiCatalog.All, "pray", 0, [], [], null);
    Check("pray favours folded hands", pray.Count > 0 && pray[0].Glyph == "🙏", pray.FirstOrDefault()?.Name);
    var pins = EmojiPinOrder.Toggle([], "🔥");
    pins = EmojiPinOrder.Toggle(pins, "🚀");
    pins = EmojiPinOrder.Move(pins, "🚀", -1);
    Check("new pin appends and can move up", pins.Count == 2 && pins[0] == "🚀" && pins[1] == "🔥");
    var frequent = FrequentEmoji.Record(FrequentEmoji.Record([], "😀"), "😂");
    Check("frequent puts latest first", frequent[0] == "😂" && frequent[1] == "😀");
    var overview = EmojiCatalog.Search("", 0, ["👍"], ["🔥"]);
    Check("overview leads with pins then frequent", overview.Count > 2 && overview[0].Glyph == "👍" && overview[1].Glyph == "🔥");
    Check("emoji zoom in removes a column", EmojiGridGeometry.ZoomIn(8) == 7);
    Check("emoji actual size clamp", EmojiGridGeometry.ClampColumns(99) == 10);
    Check("digit 0 is the tenth favorite slot", PaletteDigit.SlotIndex(0) == 9 && PaletteDigit.SlotIndex(1) == 0);
    Check("compact strip keeps five", CompactFavorites.Strip(["a", "b", "c", "d", "e", "f"]).Count == 5 && CompactFavorites.ShowsOverflow(6));
}

{
    var dir = Path.Combine(Path.GetTempPath(), "relay-fav-" + Guid.NewGuid().ToString("n"));
    Directory.CreateDirectory(dir);
    try
    {
        var path = Path.Combine(dir, "favorites.json");
        File.WriteAllText(path, """{"app:one":true,"app:two":false,"app:three":true}""");
        var store = new FavoritesStore(path);
        Check("favorites migrate dict order", store.OrderedIds().SequenceEqual(new[] { "app:one", "app:three" }));
        store.Toggle("app:two");
        Check("toggle appends", store.OrderedIds()[^1] == "app:two");
        Check("move up swaps", store.Move("app:two", -1) && store.At(1) == "app:two");
        Check("slot 0 is first favorite", store.At(PaletteDigit.SlotIndex(1)) == "app:one");
        var reloaded = new FavoritesStore(path);
        Check("favorites persist as a list", reloaded.OrderedIds().Count == 3 && reloaded.IsFavorite("app:two"));
    }
    finally { try { Directory.Delete(dir, true); } catch (Exception) { } }
}

{
    var home = @"C:\Users\demo";
    var empty = FileSearchPolicy.Resolve([], [], home, _ => [@"C:\Users\demo\Documents"]);
    Check("empty file-search scopes use home libraries", empty.Roots.Count == 1 && empty.Roots[0].EndsWith("Documents"));
    var expanded = FileSearchPolicy.Resolve(["~"], ["*.tmp"], home, h => [h + @"\Desktop", h + @"\Documents"]);
    Check("home scope expands to children", expanded.Roots.Count == 2 && expanded.Roots[0].EndsWith("Desktop"));
    Check("shipped ignore still compiled in", expanded.Ignore.Excludes(@"C:\src\node_modules\x"));
    Check("file search walks nested folders", FileSearchQuery.WalkMaxDepth >= 24);
    Check("file search debounce is live", FileSearchQuery.DebounceMs == 120);
    Check("file search launcher cap keeps the list short", FileSearchQuery.LauncherCap == 8);
    Check("file search skips the Windows tree", FileSearchQuery.IsExcludedPath(@"C:\Windows\System32\notepad.exe", expanded.Ignore));
    Check("file search keeps Downloads", !FileSearchQuery.IsExcludedPath(@"C:\Users\demo\Downloads\nested\file.pdf", expanded.Ignore));
    Check("aqs joins filename terms", FileSearchAqs.FileNameClause("annual report") == "filename:\"annual\" AND filename:\"report\"");
    Check("file search command hidden when off", !CommandAvailability.InLauncher(BuiltinCommands.FileSearch, new AppSettings()));
    Check("file search shortcut no-ops when off", !CommandAvailability.CanRun(BuiltinCommands.FileSearch, new AppSettings()));
}

{
    var file = new MenuSearchItem("File → Export As", "Ctrl+Shift+E", 1, 0, "Export As", "File");
    var edit = new MenuSearchItem("Edit → Copy", "Ctrl+C", 2, 0, "Copy", "Edit");
    var hidden = new MenuSearchItem("View → Hidden", "", 3, 0, "Hidden", "View", Enabled: false);
    var ranked = MenuSearchQuery.Filter([file, edit, hidden], "export");
    Check("menu search ranks the leaf title", ranked.Count == 1 && ranked[0].LeafTitle == "Export As");
    Check("disabled menu rows are ineligible", !hidden.IsEligible);
    Check("self target is classified before menu-less",
        MenuSearchTarget.Classify(@"C:\Relay.exe", @"C:\Relay.exe", []) == MenuSearchTargetKind.SelfTarget);
    Check("excluded app is refused",
        MenuSearchTarget.Classify(@"C:\Apps\Slack.exe", @"C:\Relay.exe", ["Slack"]) == MenuSearchTargetKind.Excluded);
    var flattened = MenuSnapshotPolicy.Flatten([
        new MenuSearchItem("Apple → About", "", 1, 0, "About", "Apple"),
        new MenuSearchItem("File → New", "", 2, 0, "New", "File"),
    ], dropFirstTopLevel: true);
    Check("apple menu dropped by first section", flattened.Count == 1 && flattened[0].Section == "File");
}

{
    var box = new RectD(100, 50, 1000, 800);
    var described = WindowLayoutGeometry.Describe(new RectD(100, 50, 500, 400), box, "notepad", @"C:\Windows\notepad.exe", "DISPLAY1");
    var resolved = WindowLayoutGeometry.Resolve(described, box);
    Check("layout describe/resolve round-trips a top-left half",
        resolved.X == 100 && resolved.Y == 50 && resolved.Width == 500 && resolved.Height == 400,
        resolved.ToString());
    var center = WindowLayoutGeometry.Describe(new RectD(350, 250, 500, 400), box, "code", null, "DISPLAY1");
    Check("centered residual stays exact",
        Math.Abs(WindowLayoutGeometry.Resolve(center, box).X - 350) < 0.6);
    Check("absent display is skipped", WindowLayoutGeometry.MatchDisplay("missing", 99, [new WindowLayoutDisplay("DISPLAY1", box)]) is null);
    var old = new WindowLayoutSlot("notepad", new RectD(10, 10, 200, 100), 0);
    Check("legacy slots keep their stored frame",
        WindowLayoutGeometry.ResolveSlot(old, box).Width == 200);
    var windows = new (string ProcessName, string? Path, RectD Frame, int Index)[]
    {
        ("notepad", @"C:\Windows\notepad.exe", new RectD(100, 50, 500, 400), 0),
        ("notepad", @"C:\Windows\notepad.exe", new RectD(900, 400, 200, 100), 1),
    };
    var nearest = WindowLayoutGeometry.NearestWindowIndex(described, resolved, windows, new HashSet<int>());
    Check("layout binds the nearest centre", nearest == 0);
}

{
    var identity = new UninstallIdentity("Relay", "com.relay.windows", ["com.relay.windows.dev"]);
    Check("name match is exact", UninstallRules.MatchesName("Relay", identity) && !UninstallRules.MatchesName("Relay leftovers", identity));
    Check("vendor sibling is not claimed", !UninstallRules.MatchesBundleId("com.relay.windows.dev.plist", identity));
    var locked = new UninstallCandidate(@"C:\Windows\notepad.exe", "Notepad", "Install", UninstallProtection.SystemProtected, "system");
    var loose = new UninstallCandidate(@"C:\Apps\Foo", "Foo", "Folder", UninstallProtection.Removable, "name", 12);
    var selection = new UninstallSelection();
    selection.SelectAll([locked, loose]);
    Check("locked leftover cannot be checked", !selection.Contains(locked.Path) && selection.Contains(loose.Path));
    Check("relay refuses its own uninstall", UninstallPlanLogic.IsSelf(identity, "com.relay.windows", @"C:\Relay"));
}

{
    var positional = ShellCommandSpec.NeverSplicedArguments("echo %1 & del C:\\", ["hello; rm -rf"]);
    Check("custom command args are positional", positional.Count == 1 && positional[0] == "hello; rm -rf");
    Check("missing working directory fails closed", ShellCommandSpec.ResolvedWorkingDirectory(@"C:\missing-relay-dir", "C:\\") is null);
    var imported = RaycastScriptImport.Parse(@"C:\scripts\hi.sh", """
        #!/bin/bash
        # @raycast.title Say Hi
        # @raycast.mode fullOutput
        # @raycast.needsConfirmation true
        # @raycast.argument1 { "type": "text", "placeholder": "Name" }
        echo hi
        """);
    Check("raycast script import maps title and output",
        imported is { Name: "Say Hi", ShowOutput: true, Confirm: true } && imported.Parameters.Count == 1);
    Check("raycast helper without title is skipped", RaycastScriptImport.Parse(@"C:\scripts\lib.sh", "#!/bin/bash\necho\n") is null);
    var session = new ArgumentSession("custom:1", "command", [new ArgumentPrompt("Name"), new ArgumentPrompt("Note", false)]);
    Check("required argument holds enter", !session.Submit(""));
    Check("argument form advances", session.Submit("Ada") && session.Current?.Name == "Note");
    Check("optional argument occupies its slot", session.Submit("") && session.IsComplete && session.Values[1] == "");
    Check("quit is not bindable", !CommandAvailability.IsBindable(BuiltinCommands.Quit));
}

{
    var ctx = new ExpansionContext
    {
        ClipboardHistory = ["one", "two"],
        Selection = "  Hello ",
        Now = new DateTime(2026, 9, 17, 15, 4, 0),
        MakeUuid = () => "uuid-1",
    };
    Check("snippet query alias is argument",
        SnippetTemplateEngine.DeclaredArguments("{query}").Count == 1);
    Check("snippet clipboard offset",
        SnippetTemplateEngine.Expand("{clipboard offset=1}", ctx).Text == "two");
    Check("snippet modifier pipeline",
        SnippetTemplateEngine.Expand("{selection | trim | uppercase}", ctx).Text == "HELLO");
    Check("snippet unknown token stays",
        SnippetTemplateEngine.Expand("{browser-tab}", ctx).Text == "{browser-tab}");
    var nested = SnippetTemplateEngine.Expand("{snippet:Inner}", ctx, snippets:
        [new StoredSnippet("Inner", "Inner", "in", "X{cursor}Y")]);
    Check("snippet cursor from nested reference", nested.CursorOffsetFromEnd == 1);
    var md = SnippetFrontmatter.Parse("sig.md", "---\nname: Sign-off\nkeyword: sig\nenabled: false\nshow_confirmation: true\n---\nHi\n");
    Check("snippet frontmatter flags", md is { Enabled: false, ShowConfirmation: true });
}

Console.WriteLine();
Console.WriteLine(failed == 0 ? $"All {passed} checks passed." : $"{failed} failed, {passed} passed.");
return failed == 0 ? 0 : 1;
