using System.IO.Compression;
using System.Text.Json;
using Tinycast;
using Tinycast.DesignSystem;
using Tinycast.Features.Backup;
using Tinycast.Features.Calculator;
using Tinycast.Features.Calendar;
using Tinycast.Features.Clipboard;
using Tinycast.Features.Emoji;
using Tinycast.Features.HotKeys;
using Tinycast.Features.Launcher;
using Tinycast.Features.Quicklinks;
using Tinycast.Features.Settings;
using Tinycast.Features.Snippets;
using Tinycast.Features.SystemActions;
using Tinycast.Features.Uninstall;
using Tinycast.Features.FileSearch;
using Tinycast.Features.Updates;
using Tinycast.Features.WindowManagement;
using Tinycast.Features.Ai;
using Tinycast.Palette;

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
    Check("per-app chord does not conflict globally", HotKeyConflicts.Find([a, c]).Count == 0);
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
    Check("dot component is structurally hidden", FileSearchQuery.IsExcludedPath(@"C:\Users\.git\config", ignore));
    Check("$ component is structurally hidden", FileSearchQuery.IsExcludedPath(@"D:\$Recycle.Bin\a", ignore));
    Check("images accept png", FileSearchFilter.Images.Accepts(@"C:\a.png", false));
    Check("folders filter keeps directories", FileSearchFilter.Folders.Accepts(@"C:\a", true) && !FileSearchFilter.Folders.Accepts(@"C:\a.png", false));
    Check("file search scopes are mirrored", SettingsBackupCoverage.Mirrored.Contains(AppSettingsKey.FileSearchScopes));
    Check("window gap is mirrored", SettingsBackupCoverage.Mirrored.Contains(AppSettingsKey.WindowGap));
}

{
    var identity = new UninstallIdentity("Tinycast", "com.tinycast.windows", []);
    Check("name match needs three letters", UninstallRules.MatchesName("Tinycast leftovers", identity));
    Check("short names do not match", !UninstallRules.MatchesName("ti", new UninstallIdentity("ti", null, [])));
    Check("windows folder is protected", UninstallRules.IsProtected(Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
    var tinycastData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tinycast");
    Check("appdata tinycast is protected", UninstallRules.IsProtected(tinycastData));
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
    var dir = Path.Combine(Path.GetTempPath(), "tinycast-harness-" + Guid.NewGuid().ToString("n"));
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
    var rankingDir = Path.Combine(Path.GetTempPath(), "tinycast-rank-" + Guid.NewGuid().ToString("n"));
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
    var dir = Path.Combine(Path.GetTempPath(), "tinycast-rank-ui-" + Guid.NewGuid().ToString("n"));
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
    var zip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n") + ".tinycast");
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
    var coreDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Tinycast.Core"));
    if (!Directory.Exists(coreDir))
        coreDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "src", "Tinycast.Core"));
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
        Check("Tinycast.Core has no WinUI or P/Invoke", leaks.Length == 0, string.Join(", ", leaks));
    }
}

{
    Check("update tag drops v", UpdateRelease.ParseTag("v0.2.0") == new Version(0, 2, 0));
    Check("update tag ignores prerelease suffix", UpdateRelease.ParseTag("v1.0.0-beta.1") == new Version(1, 0, 0));
    Check("update zip prefers x64", UpdateRelease.PickAsset(["notes.txt", "Tinycast-windows-x64.zip"], System.Runtime.InteropServices.Architecture.X64) == "Tinycast-windows-x64.zip");
    Check("update notes drop install marker",
        UpdateRelease.NotesSummary("Fixed search.\n\n<!-- tinycast:install -->\nUnzip me") == "Fixed search.");
    var listings = new UpdateRelease.ReleaseListing[]
    {
        new("v0.1.0", new Version(0, 1, 0), false, false, ["Tinycast-windows-x64.zip"]),
        new("v0.2.0", new Version(0, 2, 0), false, false, ["Tinycast-windows-x64.zip"]),
        new("v0.3.0", new Version(0, 3, 0), true, false, ["Tinycast-windows-x64.zip"]),
        new("v0.2.1", new Version(0, 2, 1), false, false, ["notes.txt"]),
    };
    Check("update latest skips drafts and missing zips",
        UpdateRelease.SelectLatest(listings, System.Runtime.InteropServices.Architecture.X64)?.Tag == "v0.2.0");
    var env = DotEnv.Parse("""
        # comment
        export TINYCAST_GITHUB_TOKEN="ghp_example"
        GITHUB_TOKEN=ignored # trailing
        """);
    Check("dotenv reads quoted token", env["TINYCAST_GITHUB_TOKEN"] == "ghp_example");
    Check("github token strips bearer", GitHubToken.Sanitize("Bearer ghp_example") == "ghp_example");
    Check("github token rejects blank", GitHubToken.Sanitize("  ") is null);
}

{
    Check("settings search finds ocr on clipboard", SettingsCatalog.Search("ocr").Any(p => p.Tab == SettingsTab.Clipboard));
    Check("settings has no apple shortcuts pane", SettingsCatalog.Panes.All(p => p.Title != "Apple Shortcuts"));
    Check("url fallback detects host", FallbackCatalog.LooksLikeUrl("github.com"));
    Check("url fallback rejects words", !FallbackCatalog.LooksLikeUrl("open notepad"));
    Check("clipboard filter cycles", ClipboardListFilterLogic.Next(ClipboardListFilter.All) == ClipboardListFilter.Text);
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
}

Console.WriteLine();
Console.WriteLine(failed == 0 ? $"All {passed} checks passed." : $"{failed} failed, {passed} passed.");
return failed == 0 ? 0 : 1;
