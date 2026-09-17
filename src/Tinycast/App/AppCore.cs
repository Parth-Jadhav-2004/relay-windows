using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Tinycast.DesignSystem;
using Tinycast.Features.Backup;
using Tinycast.Features.Calculator;
using Tinycast.Features.Calendar;
using Tinycast.Features.Clipboard;
using Tinycast.Features.Commands;
using Tinycast.Features.HotKeys;
using Tinycast.Features.Launcher;
using Tinycast.Features.Quicklinks;
using Tinycast.Features.Onboarding;
using Tinycast.Features.Settings;
using Tinycast.Features.Snippets;
using Tinycast.Features.Updates;
using Tinycast.Features.WindowManagement;
using Tinycast.Palette;
using Tinycast.Platform;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Tinycast;

public sealed class AppCore
{
    public static AppCore Shared { get; } = new();

    Mutex? _mutex;
    WindowProcedureHook? _hook;
    TrayIcon? _tray;
    HotKeyCenter? _hotKeys;
    NotesWindow? _notes;
    SupportWindow? _support;
    CameraWindow? _camera;
    OnboardingWindow? _onboarding;
    DateTime _installedAt = DateTime.UtcNow;
    DateTime? _supportShown;
    DispatcherQueueTimer? _meetingTimer;
    DateTime _meetingWatchArmedAt = DateTime.Now;
    readonly HashSet<string> _joinedMeetings = [];
    GitHubRelease? _pendingUpdate;
    string _updateStatus = "";
    bool _updateBusy;
    bool _updateInstalling;
    public HashSet<string> BackupCategories { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        BackupArchive.SettingsAndShortcuts,
        BackupArchive.Clipboard,
        BackupArchive.Snippets,
        BackupArchive.Notes,
        BackupArchive.Learning,
    };
    public string LastCommandOutput { get; set; } = "";
    public StoredSnippet? PendingSnippet { get; set; }

    public AppSettings Settings { get; private set; } = new();
    public PaletteState Palette { get; } = new();
    public PaletteWindow? PaletteWindow { get; private set; }
    public PaletteCoordinator PaletteCoordinator { get; }
    public SettingsCoordinator SettingsCoordinator { get; }
    public LauncherCoordinator LauncherCoordinator { get; }
    public FileSearchCoordinator FileSearchCoordinator { get; }
    public ClipboardCoordinator ClipboardCoordinator { get; }
    public AiCoordinator AiCoordinator { get; }
    public DialogPresenter Dialogs { get; }
    public MessageHudPresenter MessageHud { get; }
    public ClipboardStore ClipboardStore { get; private set; } = null!;
    public ClipboardManager Clipboard { get; private set; } = null!;
    public LauncherRankingStore Ranking { get; private set; } = null!;
    public FavoritesStore Favorites { get; private set; } = null!;
    public AliasStore Aliases { get; private set; } = null!;
    public VisibilityStore Visibility { get; private set; } = null!;
    public List<AppEntry> Apps { get; private set; } = [];
    public List<StoredSnippet> Snippets { get; private set; } = [];
    public List<Quicklink> Quicklinks { get; private set; } = [];
    public List<CustomCommand> CustomCommands { get; private set; } = [];
    public List<CalcResult> CalcHistory { get; private set; } = [];
    public List<MeetingEvent> Meetings { get; private set; } = [];
    public List<AiChatMessage> Chat { get; private set; } = [];
    public List<NoteDocument> Notes { get; private set; } = [];
    public List<WindowLayout> Layouts { get; private set; } = [];
    public List<HotKeyBinding> HotKeys { get; private set; } = [];
    public List<McpServerSpec> McpServers { get; private set; } = [];
    public List<QuickAction> QuickActions { get; private set; } = [];
    public List<FallbackSpec> Fallbacks { get; private set; } = [];
    public string LastSelection { get; private set; } = "";
    public AiConfig Ai { get; private set; } = new();
    public bool UpdatesBusy => _updateBusy;
    public bool CanInstallUpdate => _pendingUpdate is not null && !_updateBusy;
    public string UpdatesButtonLabel =>
        _updateInstalling ? "Downloading…"
        : _updateBusy ? "Checking…"
        : _pendingUpdate is not null ? "Download and restart"
        : "Check for updates";
    public string AboutUpdateCopy => _updateStatus;

    AppCore()
    {
        PaletteCoordinator = new PaletteCoordinator(this);
        SettingsCoordinator = new SettingsCoordinator(this);
        LauncherCoordinator = new LauncherCoordinator(this);
        FileSearchCoordinator = new FileSearchCoordinator(this);
        ClipboardCoordinator = new ClipboardCoordinator(this);
        AiCoordinator = new AiCoordinator(this);
        Dialogs = new DialogPresenter(this);
        MessageHud = new MessageHudPresenter(this);
    }

    public void Start()
    {
        _mutex = new Mutex(true, @"Local\" + AppPaths.ChannelId, out var created);
        if (!created)
        {
            Log.Write("Another instance is already running.");
            Application.Current.Exit();
            return;
        }

        AppPaths.EnsureRoot();
        Settings = SettingsStore.Load();
        LoadStores();
        AiCoordinator.Start();
        PaletteWindow = new PaletteWindow(this);
        PaletteWindow.AppWindow.Closing += (_, args) =>
        {
            args.Cancel = true;
            PaletteWindow.AppWindow.Hide();
        };
        PaletteWindow.AppWindow.Show(false);
        ApplyAppearance();

        var uiSettings = new Windows.UI.ViewManagement.UISettings();
        uiSettings.ColorValuesChanged += (_, _) =>
            PaletteWindow.DispatcherQueue.TryEnqueue(() => ApplyAppearance());

        var hwnd = PaletteWindow.Hwnd;
        Clipboard = new ClipboardManager(this);
        Clipboard.Start(hwnd);

        _hotKeys = new HotKeyCenter(hwnd);
        _hotKeys.TogglePalette += () =>
            PaletteWindow.DispatcherQueue.TryEnqueue(() => PaletteCoordinator.TogglePalette());
        _hotKeys.Command += id =>
            PaletteWindow.DispatcherQueue.TryEnqueue(() => DispatchHotKey(id));
        _hotKeys.ExpandSnippet += (id, keyword) =>
            PaletteWindow.DispatcherQueue.TryEnqueue(() => ExpandSnippetKeyword(id, keyword));
        _hotKeys.RegistrationFailed += msg =>
            PaletteWindow.DispatcherQueue.TryEnqueue(() => ShowMessage(msg, DialogTone.Danger));
        _hotKeys.ReplaceBindings(HotKeys);
        RefreshSnippetHook();

        _tray = new TrayIcon(hwnd, PaletteWindow.IconPath);
        _tray.ShowPalette += () =>
            PaletteWindow.DispatcherQueue.TryEnqueue(() => PaletteCoordinator.TogglePalette());
        _tray.OpenSettings += () =>
            PaletteWindow.DispatcherQueue.TryEnqueue(() => SettingsCoordinator.Show());
        _tray.Quit += () =>
            PaletteWindow.DispatcherQueue.TryEnqueue(Quit);
        ApplyTrayVisibility();

        _hook = new WindowProcedureHook(hwnd, Filter);
        if (Settings.LaunchAtLogin)
            LaunchAtLogin.SetEnabled(true);

        var keepAlive = PaletteWindow.DispatcherQueue.CreateTimer();
        keepAlive.Interval = TimeSpan.FromHours(12);
        keepAlive.IsRepeating = true;
        keepAlive.Tick += (_, _) => { };
        keepAlive.Start();

        _ = CurrencyRateStore.RefreshAsync();
        RefreshApps();
        _ = RefreshCalendarAsync();
        StartMeetingWatch();
        GitHubAuth.MigrateVault();
        ScheduleUpdateCheck();
        MaybeShowOnboarding();

        Log.Write($"Started {AppPaths.ChannelId} hwnd=0x{hwnd.ToInt64():X}");
        MaybeRemindSupport();
    }

    void LoadStores()
    {
        ClipboardStore = new ClipboardStore(AppPaths.ClipboardDir);
        Ranking = new LauncherRankingStore(AppPaths.RankingFile);
        Favorites = new FavoritesStore(AppPaths.FavoritesFile);
        Aliases = new AliasStore(AppPaths.AliasesFile);
        Visibility = new VisibilityStore(AppPaths.VisibilityFile);
        Snippets = JsonList.Load<StoredSnippet>(AppPaths.SnippetsFile);
        MergeSnippetMarkdown();
        Quicklinks = JsonList.Load<Quicklink>(AppPaths.QuicklinksFile);
        CustomCommands = JsonList.Load<CustomCommand>(AppPaths.CustomCommandsFile);
        CalcHistory = JsonList.Load<CalcResult>(AppPaths.CalcHistoryFile);
        HotKeys = JsonList.Load<HotKeyBinding>(AppPaths.HotKeysFile);
        Layouts = JsonList.Load<WindowLayout>(AppPaths.LayoutsFile);
        McpServers = McpHost.Load().ToList();
        Chat = JsonList.Load<AiChatMessage>(AppPaths.ChatFile);
        QuickActions = JsonList.Load<QuickAction>(AppPaths.QuickActionsFile);
        RefreshFallbacks();
        Ai = AiClient.Load();
        Notes = LoadNotes();
        LoadSupportStamp();
        SeedDefaults();
    }

    void SeedDefaults()
    {
        if (Snippets.Count == 0)
        {
            Snippets.Add(new StoredSnippet("snippet:date", "Today’s date", "date", "{date}"));
            Snippets.Add(new StoredSnippet("snippet:sig", "Sign-off", "sig", "Thanks,\n{date}"));
            PersistSnippets();
        }

        if (Quicklinks.Count == 0)
        {
            Quicklinks.Add(new Quicklink(
                "quicklink:home",
                "User folder",
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                QuicklinkKind.Path,
                "home"));
            PersistQuicklinks();
        }
    }

    List<NoteDocument> LoadNotes()
    {
        var notes = new List<NoteDocument>();
        if (!Directory.Exists(AppPaths.NotesDir))
            return notes;
        foreach (var file in Directory.EnumerateFiles(AppPaths.NotesDir, "*.md"))
        {
            notes.Add(new NoteDocument(
                Path.GetFileNameWithoutExtension(file),
                Path.GetFileNameWithoutExtension(file),
                file,
                File.ReadAllText(file)));
        }

        return notes;
    }

    void LoadSupportStamp()
    {
        try
        {
            if (!File.Exists(AppPaths.SupportFile))
            {
                PersistSupportStamp();
                return;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(AppPaths.SupportFile));
            if (doc.RootElement.TryGetProperty("installedAt", out var installed))
                _installedAt = installed.GetDateTime();
            if (doc.RootElement.TryGetProperty("lastShown", out var shown) && shown.ValueKind != JsonValueKind.Null)
                _supportShown = shown.GetDateTime();
        }
        catch (Exception)
        {
            _installedAt = DateTime.UtcNow;
        }
    }

    void PersistSupportStamp()
    {
        var json = JsonSerializer.Serialize(new { installedAt = _installedAt, lastShown = _supportShown });
        File.WriteAllText(AppPaths.SupportFile, json);
    }

    void MaybeRemindSupport()
    {
        var schedule = new SupportReminderSchedule(_installedAt, _supportShown);
        if (!schedule.IsDue(DateTime.UtcNow))
            return;
        PaletteWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            ShowSupport();
            PostponeSupportReminder();
        });
    }

    public void PostponeSupportReminder()
    {
        _supportShown = DateTime.UtcNow;
        PersistSupportStamp();
    }

    public void RefreshApps()
    {
        _ = Task.Run(() =>
        {
            try
            {
                var apps = AppIndex.Scan();
                PaletteWindow?.DispatcherQueue.TryEnqueue(() =>
                {
                    Apps = apps;
                    Palette.Notify();
                });
            }
            catch (Exception ex)
            {
                Log.Write("app index: " + ex.Message);
            }
        });
    }

    public async Task RefreshCalendarAsync()
    {
        if (!Settings.CalendarEnabled)
        {
            Meetings = [];
            return;
        }

        Meetings = (await CalendarService.UpcomingAsync()).Where(m =>
                Settings.CalendarExcludedIds.Count == 0
                || string.IsNullOrWhiteSpace(m.CalendarId)
                || !Settings.CalendarExcludedIds.Contains(m.CalendarId))
            .ToList();
        Palette.Notify();
        UpdateTrayTitle();
    }

    void StartMeetingWatch()
    {
        _meetingTimer = PaletteWindow?.DispatcherQueue.CreateTimer();
        if (_meetingTimer is null)
            return;
        _meetingTimer.Interval = TimeSpan.FromSeconds(30);
        _meetingTimer.IsRepeating = true;
        _meetingWatchArmedAt = DateTime.Now;
        _meetingTimer.Tick += (_, _) =>
        {
            if (!Settings.AutoJoinMeetings || !Settings.CalendarEnabled)
                return;
            _ = RefreshCalendarAsync();
            var now = DateTime.Now;
            foreach (var meeting in Meetings)
            {
                if (!MeetingAutoJoin.IsDue(meeting, now, _meetingWatchArmedAt, _joinedMeetings))
                    continue;
                if (meeting.Link is not { } link)
                    continue;
                _joinedMeetings.Add(meeting.Id);
                ProcessLauncher.OpenUri(link.Url.ToString());
                break;
            }
        };
        _meetingTimer.Start();
    }

    public void Persist() => SettingsStore.Save(Settings);
    public void PersistSnippets()
    {
        JsonList.Save(AppPaths.SnippetsFile, Snippets);
        WriteSnippetMarkdown();
        RefreshSnippetHook();
    }

    void MergeSnippetMarkdown()
    {
        if (!Directory.Exists(AppPaths.SnippetsDir))
            return;
        foreach (var file in Directory.EnumerateFiles(AppPaths.SnippetsDir, "*.md"))
        {
            StoredSnippet? parsed;
            try { parsed = SnippetFrontmatter.Parse(file, File.ReadAllText(file)); }
            catch (Exception) { continue; }
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Text))
                continue;
            var id = "snippet:md:" + Path.GetFileNameWithoutExtension(file);
            var existing = Snippets.FindIndex(s =>
                s.Id == id
                || (!string.IsNullOrWhiteSpace(parsed.Keyword)
                    && s.Keyword.Equals(parsed.Keyword, StringComparison.OrdinalIgnoreCase)));
            var snippet = parsed with { Id = existing >= 0 ? Snippets[existing].Id : id };
            if (existing >= 0)
                Snippets[existing] = snippet;
            else
                Snippets.Add(snippet);
        }
    }

    void WriteSnippetMarkdown()
    {
        Directory.CreateDirectory(AppPaths.SnippetsDir);
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var snippet in Snippets)
        {
            var safe = string.Join("_", (string.IsNullOrWhiteSpace(snippet.Keyword) ? snippet.Name : snippet.Keyword)
                .Split(Path.GetInvalidFileNameChars())).Trim();
            if (string.IsNullOrWhiteSpace(safe))
                safe = snippet.Id.Replace(":", "-");
            var name = safe + ".md";
            File.WriteAllText(Path.Combine(AppPaths.SnippetsDir, name), SnippetFrontmatter.Serialize(snippet));
            keep.Add(name);
        }

        foreach (var extra in Directory.EnumerateFiles(AppPaths.SnippetsDir, "*.md"))
        {
            if (keep.Contains(Path.GetFileName(extra)))
                continue;
            try { File.Delete(extra); } catch (Exception) { }
        }
    }

    public void RefreshSnippetHook() =>
        _hotKeys?.SetSnippetKeywords(
            Settings.SnippetsEnabled
                ? Snippets.Select(s => (s.Keyword, s.Id))
                : []);
    public void PersistQuicklinks() => JsonList.Save(AppPaths.QuicklinksFile, Quicklinks);
    public void PersistCustomCommands() => JsonList.Save(AppPaths.CustomCommandsFile, CustomCommands);
    public void PersistCalcHistory() => JsonList.Save(AppPaths.CalcHistoryFile, CalcHistory);
    public void PersistHotKeys()
    {
        JsonList.Save(AppPaths.HotKeysFile, HotKeys);
        _hotKeys?.ReplaceBindings(HotKeys);
    }
    public void PersistLayouts() => JsonList.Save(AppPaths.LayoutsFile, Layouts);
    public void PersistFallbacks() => JsonList.Save(AppPaths.FallbacksFile, Fallbacks);
    public void RefreshFallbacks()
    {
        var argumentLinks = Quicklinks
            .Where(q => q.Destination.Contains("{argument}", StringComparison.OrdinalIgnoreCase)
                || q.Destination.Contains("{query}", StringComparison.OrdinalIgnoreCase))
            .Select(q => q.Id);
        Fallbacks = FallbackCatalog.Merge(JsonList.Load<FallbackSpec>(AppPaths.FallbacksFile), argumentLinks);
        PersistFallbacks();
    }
    public void PersistChat() => JsonList.Save(AppPaths.ChatFile, Chat);
    public void PersistQuickActions() => JsonList.Save(AppPaths.QuickActionsFile, QuickActions);
    public void PersistMcp() => McpHost.Save(McpServers);

    public void ApplyAppearance(Window? window = null)
    {
        var theme = Settings.Appearance switch
        {
            AppAppearance.Light => ElementTheme.Light,
            AppAppearance.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        ApplyTheme(window, theme);
        ApplyTheme(PaletteWindow, theme);
        ApplyTheme(_notes, theme);
        ApplyTheme(_support, theme);
        ApplyTheme(_camera, theme);
        ApplyTheme(_onboarding, theme);
        ApplyTheme(SettingsCoordinator.Window, theme);
        PaletteWindow?.ApplySurface();
    }

    static void ApplyTheme(Window? window, ElementTheme theme)
    {
        if (window?.Content is FrameworkElement root)
            root.RequestedTheme = theme;
    }

    public void ApplyTrayVisibility()
    {
        if (_tray is null)
            return;
        if (Settings.ShowInTray)
            _tray.Add();
        else
            _tray.Remove();
    }

    public void RememberSelection(string text) => LastSelection = text;

    public void ShowMessage(string message, DialogTone tone = DialogTone.Neutral) =>
        MessageHud.Show(message, tone);

    public Task<int> Confirm(DialogRequest request) => Dialogs.Confirm(request);

    public void ShowNotes(string? id = null)
    {
        if (!Settings.NotesEnabled)
        {
            ShowMessage("Notes are off in Settings.");
            return;
        }

        if (_notes is null)
        {
            _notes = new NotesWindow(this);
            _notes.Closed += (_, _) =>
            {
                _notes = null;
            };
        }

        _notes.Activate();
        ApplyAppearance(_notes);
        if (id is not null)
            _notes.Open(id);
    }

    public NoteDocument CreateNote(string title)
    {
        var id = Guid.NewGuid().ToString("n");
        var path = UniqueNotePath(title, id);
        File.WriteAllText(path, "");
        var note = new NoteDocument(id, string.IsNullOrWhiteSpace(title) ? "Untitled" : title, path, "");
        Notes.Add(note);
        return note;
    }

    public void SaveNote(string id, string title, string text)
    {
        var note = Notes.FirstOrDefault(n => n.Id == id);
        if (note is null)
            return;
        var path = UniqueNotePath(title, id, note.Path);
        File.WriteAllText(path, text);
        if (!string.Equals(path, note.Path, StringComparison.OrdinalIgnoreCase)
            && File.Exists(note.Path)
            && Notes.All(n => n.Id == id || !string.Equals(n.Path, note.Path, StringComparison.OrdinalIgnoreCase)))
        {
            try { File.Delete(note.Path); } catch (Exception) { }
        }
        var index = Notes.FindIndex(n => n.Id == id);
        Notes[index] = new NoteDocument(id, string.IsNullOrWhiteSpace(title) ? "Untitled" : title, path, text);
    }

    string UniqueNotePath(string title, string id, string? currentPath = null)
    {
        var safe = string.Join("_", title.Split(Path.GetInvalidFileNameChars())).Trim();
        if (string.IsNullOrWhiteSpace(safe))
            safe = id;
        var candidate = Path.Combine(AppPaths.NotesDir, safe + ".md");
        if (!string.IsNullOrEmpty(currentPath)
            && string.Equals(candidate, currentPath, StringComparison.OrdinalIgnoreCase))
            return candidate;
        if (!File.Exists(candidate)
            && Notes.All(n => n.Id == id || !string.Equals(n.Path, candidate, StringComparison.OrdinalIgnoreCase)))
            return candidate;
        return Path.Combine(AppPaths.NotesDir, safe + "-" + Guid.NewGuid().ToString("n")[..8] + ".md");
    }

    public void DeleteNote(string id)
    {
        var note = Notes.FirstOrDefault(n => n.Id == id);
        if (note is null)
            return;
        if (File.Exists(note.Path))
            File.Delete(note.Path);
        Notes.Remove(note);
    }

    public void ShowSupport()
    {
        if (_support is null)
        {
            _support = new SupportWindow(this);
            _support.Closed += (_, _) => _support = null;
        }

        _support.Activate();
        ApplyAppearance(_support);
    }

    public void ShowCamera()
    {
        if (_camera is null)
        {
            _camera = new CameraWindow(this);
            _camera.Closed += (_, _) => _camera = null;
        }

        _camera.Activate();
        ApplyAppearance(_camera);
    }

    public async void ExportBackup()
    {
        try
        {
            var picker = new FileSavePicker();
            InitializeWithWindow.Initialize(picker, PaletteWindow!.Hwnd);
            picker.SuggestedFileName = "tinycast-backup";
            picker.FileTypeChoices.Add("Tinycast archive", [".tinycast"]);
            var file = await picker.PickSaveFileAsync();
            if (file is null)
                return;
            var path = file.Path;
            ClipboardStore.Checkpoint();
            var categories = BackupCategories.Count == 0
                ? BackupArchive.AllCategories
                : BackupCategories.ToList();
            BackupArchive.Write(
                path,
                categories,
                SettingsSnapshot.Capture(Settings),
                JsonSerializer.Serialize(Snippets),
                AppPaths.NotesDir,
                File.Exists(AppPaths.RankingFile) ? File.ReadAllText(AppPaths.RankingFile) : "[]",
                Path.Combine(AppPaths.ClipboardDir, "clipboard.sqlite"));
            ShowMessage("Backup saved", DialogTone.Success);
        }
        catch (Exception ex)
        {
            ShowMessage(ex.Message, DialogTone.Danger);
        }
    }

    public async void ImportBackup()
    {
        try
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, PaletteWindow!.Hwnd);
            picker.FileTypeFilter.Add(".tinycast");
            picker.FileTypeFilter.Add(".zip");
            var file = await picker.PickSingleFileAsync();
            if (file is null)
                return;
            var reason = BackupArchive.IncompatibleReason(BackupArchive.ReadManifest(file.Path));
            if (reason is not null)
            {
                ShowMessage(reason, DialogTone.Danger);
                return;
            }

            var imported = BackupArchive.ReadSettings(file.Path);
            SettingsSnapshot.ApplyMirrored(Settings, imported);
            Persist();

            var snippetsJson = BackupArchive.ReadEntryText(file.Path, "snippets.json");
            if (!string.IsNullOrWhiteSpace(snippetsJson))
            {
                var incoming = JsonSerializer.Deserialize<List<StoredSnippet>>(snippetsJson) ?? [];
                foreach (var snippet in incoming)
                {
                    var index = Snippets.FindIndex(s => s.Id == snippet.Id);
                    if (index >= 0)
                        Snippets[index] = snippet;
                    else
                        Snippets.Add(snippet);
                }
                PersistSnippets();
            }

            BackupArchive.ExtractPrefix(file.Path, "notes/", AppPaths.NotesDir);
            Notes = LoadNotes();

            var rankingJson = BackupArchive.ReadEntryText(file.Path, "ranking.json");
            if (!string.IsNullOrWhiteSpace(rankingJson))
            {
                var records = JsonSerializer.Deserialize<List<LauncherRankingRecord>>(rankingJson) ?? [];
                Ranking.ReplaceAll(records);
            }

            var sqlitePath = Path.Combine(AppPaths.ClipboardDir, "clipboard.sqlite");
            var sqliteImport = sqlitePath + ".import";
            if (BackupArchive.ExtractFile(file.Path, "clipboard.sqlite", sqliteImport))
            {
                ClipboardStore.Dispose();
                try
                {
                    File.Copy(sqliteImport, sqlitePath, overwrite: true);
                    BackupArchive.ExtractPrefix(file.Path, "clipboard-images/", Path.Combine(AppPaths.ClipboardDir, "images"), uniquifyCollisions: false);
                }
                finally
                {
                    try { File.Delete(sqliteImport); } catch (Exception) { }
                    ClipboardStore = new ClipboardStore(AppPaths.ClipboardDir);
                }
            }

            ApplyAppearance();
            ApplyTrayVisibility();
            SettingsCoordinator.Reload();
            ShowMessage("Imported mirrored settings and archive contents. Capability flags were left untouched.");
        }
        catch (Exception ex)
        {
            ShowMessage(ex.Message, DialogTone.Danger);
        }
    }

    void ScheduleUpdateCheck()
    {
        var timer = PaletteWindow!.DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(30);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => _ = QuietUpdateCheck();
        timer.Start();
    }

    async Task QuietUpdateCheck()
    {
        try
        {
            var latest = await UpdatesClient.FetchLatestAsync();
            if (latest.Version <= UpdatesClient.Installed)
                return;
            _pendingUpdate = latest;
            _updateStatus = latest.Tag + " is ready.";
            var quietNotes = UpdateRelease.NotesSummary(latest.Notes);
            if (quietNotes.Length > 0)
                _updateStatus += "\n" + (quietNotes.Length > 400 ? quietNotes[..400] + "…" : quietNotes);
            PublishUpdateUi();
        }
        catch (Exception ex)
        {
            Log.Write("update check: " + ex.Message);
        }
    }

    public async Task CheckUpdates()
    {
        if (_updateBusy)
            return;
        _updateBusy = true;
        _updateStatus = "Checking GitHub Releases…";
        PublishUpdateUi();
        try
        {
            var latest = await UpdatesClient.FetchLatestAsync();
            if (latest.Version <= UpdatesClient.Installed)
            {
                _pendingUpdate = null;
                _updateStatus = "You’re on " + UpdatesClient.InstalledLabel + ".";
                return;
            }

            _pendingUpdate = latest;
            _updateStatus = latest.Tag + " is ready.";
            var notes = UpdateRelease.NotesSummary(latest.Notes);
            if (notes.Length > 0)
                _updateStatus += "\n" + (notes.Length > 400 ? notes[..400] + "…" : notes);
        }
        catch (Exception ex)
        {
            _updateStatus = "Update failed: " + ex.Message;
        }
        finally
        {
            _updateBusy = false;
            PublishUpdateUi();
        }
    }

    public async Task InstallPendingUpdate()
    {
        if (_pendingUpdate is null || _updateBusy)
            return;

#if DEBUG
        _updateStatus = "Update available: " + _pendingUpdate.Tag + ". Dev builds do not install GitHub updates.";
        PublishUpdateUi();
#else
        var latest = _pendingUpdate;
        _updateBusy = true;
        _updateInstalling = true;
        _updateStatus = "Downloading " + latest.AssetName + "…";
        PublishUpdateUi();
        try
        {
            var payload = await UpdatesClient.DownloadAsync(latest);
            UpdatesClient.LaunchInstaller(payload);
            _updateStatus = "Restarting into " + latest.Tag + "…";
            PublishUpdateUi();
            Quit();
        }
        catch (Exception ex)
        {
            _updateBusy = false;
            _updateInstalling = false;
            _updateStatus = "Update failed: " + ex.Message;
            PublishUpdateUi();
        }
#endif
    }

    void PublishUpdateUi() =>
        PaletteWindow?.DispatcherQueue.TryEnqueue(() => SettingsCoordinator.RefreshAbout());

    public async Task SendChat(string prompt)
    {
        if (!Settings.AiEnabled)
        {
            ShowMessage("AI is off until you enable it in Settings.");
            return;
        }

        Chat.Add(new AiChatMessage("user", prompt));
        Palette.Notify();
        try
        {
            // OpenCode-first (T3 parity): reuse `opencode auth login` models.
            // Falls back to the direct HTTPS endpoint only when OpenCode has
            // no connected providers.
            string reply;
            if (AiCoordinator.IsOpenCodeReady)
            {
                reply = await AiCoordinator.ChatAsync(prompt);
            }
            else if (AiCoordinator.Models.Count == 0
                && !string.IsNullOrWhiteSpace(AiCoordinator.Status)
                && AiCoordinator.Status.Contains("disabled", StringComparison.OrdinalIgnoreCase) == false)
            {
                // No inventory yet: try one refresh (reconnect semantics),
                // then fall back to the direct key path below.
                await AiCoordinator.RefreshAsync();
                reply = AiCoordinator.IsOpenCodeReady
                    ? await AiCoordinator.ChatAsync(prompt)
                    : await DirectChatAsync();
            }
            else
            {
                reply = await DirectChatAsync();
            }

            Chat.Add(new AiChatMessage("assistant", reply));
            PersistChat();
            Palette.Notify();
        }
        catch (Exception ex)
        {
            ShowMessage(ex.Message, DialogTone.Danger);
        }
    }

    async Task<string> DirectChatAsync()
    {
        var key = CredentialStore.Get("ai");
        if (string.IsNullOrWhiteSpace(key))
        {
            if (AiCoordinator.Models.Count > 0)
                throw new InvalidOperationException(AiCoordinator.Status);
            throw new InvalidOperationException("Run `opencode auth login`, then Refresh provider status — or add an API key in Settings → Features.");
        }

        return await AiClient.Complete(Chat, key, Ai);
    }

    public void SaveCurrentLayout()
    {
        var screens = WindowInventory.Screens();
        var slots = WindowInventory.Enumerate()
            .Select(w =>
            {
                var frame = WindowInventory.Frame(w.Hwnd);
                var screen = 0;
                for (var i = 0; i < screens.Count; i++)
                {
                    var b = screens[i].Frame;
                    if (frame.X + frame.Width / 2 >= b.X && frame.X + frame.Width / 2 <= b.X + b.Width
                        && frame.Y + frame.Height / 2 >= b.Y && frame.Y + frame.Height / 2 <= b.Y + b.Height)
                    {
                        screen = i;
                        break;
                    }
                }

                return new WindowLayoutSlot(w.ProcessName, frame, screen, w.Path);
            })
            .ToList();
        Layouts.Add(new WindowLayout(Guid.NewGuid().ToString("n"), "Layout " + (Layouts.Count + 1), slots));
        PersistLayouts();
        ShowMessage("Window layout saved");
    }

    public void RestoreLayout(string id)
    {
        var layout = Layouts.FirstOrDefault(l => l.Id == id);
        if (layout is null)
            return;
        var windows = WindowInventory.Enumerate().ToList();
        foreach (var slot in layout.Slots)
        {
            var match = windows.FirstOrDefault(w => w.ProcessName.Equals(slot.ProcessName, StringComparison.OrdinalIgnoreCase));
            if (match is null && !string.IsNullOrWhiteSpace(slot.Path))
            {
                try { ProcessLauncher.Open(slot.Path); }
                catch (Exception) { }
                continue;
            }

            if (match is not null)
                WindowInventory.Place(match.Hwnd, slot.Frame);
        }
    }

    public void PauseHotKeys() => _hotKeys?.Pause();
    public void ResumeHotKeys() => _hotKeys?.ReplaceBindings(HotKeys);
    public void RecordHotKey(Action<HotKeyChord> done) => _hotKeys?.BeginRecord(done);

    public void MaybeShowOnboarding()
    {
        if (File.Exists(AppPaths.OnboardingFile))
            return;
        if (File.Exists(AppPaths.SettingsFile))
        {
            CompleteOnboarding();
            return;
        }

        PaletteWindow?.DispatcherQueue.TryEnqueue(() => ShowOnboarding());
    }

    public void ShowOnboarding(bool force = false)
    {
        if (!force && File.Exists(AppPaths.OnboardingFile))
            return;
        if (_onboarding is null)
        {
            _onboarding = new OnboardingWindow(this);
            _onboarding.Closed += (_, _) => _onboarding = null;
        }

        ApplyAppearance(_onboarding);
        _onboarding.Activate();
    }

    public void CompleteOnboarding()
    {
        AppPaths.EnsureRoot();
        File.WriteAllText(AppPaths.OnboardingFile, JsonSerializer.Serialize(new OnboardingState
        {
            Completed = true,
            CompletedAt = DateTime.UtcNow,
        }));
    }

    void ExpandSnippetKeyword(string id, string keyword)
    {
        var snippet = Snippets.FirstOrDefault(s => s.Id == id);
        if (snippet is null)
            return;
        var hwnd = NativeMethods.GetForegroundWindow();
        if (!TryPasteSnippet(snippet, hwnd, hidePalette: false, backspace: keyword.Length))
            PaletteCoordinator.ShowPalette(PaletteMode.Snippets);
    }

    public bool TryPasteSnippet(StoredSnippet snippet, IntPtr previousHwnd, bool hidePalette = true, int backspace = 0)
    {
        var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var declared = SnippetTemplateEngine.DeclaredArguments(snippet.Text);
        if (declared.Count > 0 && !string.IsNullOrWhiteSpace(Palette.Query)
            && !Palette.Query.Equals(snippet.Name, StringComparison.OrdinalIgnoreCase)
            && !Palette.Query.Equals(snippet.Keyword, StringComparison.OrdinalIgnoreCase))
            args[declared[0].Name] = Palette.Query;

        var expanded = SnippetTemplateEngine.Expand(snippet.Text, new ExpansionContext
        {
            ClipboardHistory = ClipboardStore.Search("").Select(c => c.Text).ToList(),
            Selection = LastSelection,
            Now = DateTime.Now,
        }, args, Snippets);
        if (expanded.MissingArguments.Count > 0)
        {
            PendingSnippet = snippet;
            ShowMessage("Type a value for " + expanded.MissingArguments[0].Name + ", then Enter.");
            return false;
        }

        PendingSnippet = null;

        if (hidePalette)
            PaletteCoordinator.HidePalette();
        Paster.FocusTarget(previousHwnd);
        for (var i = 0; i < backspace; i++)
            NativeMethods.SendVk(0x08);
        NativeMethods.SendUnicode(expanded.Text);
        if (expanded.CursorOffsetFromEnd is > 0 and var left)
            Paster.MoveCaretLeft(left);
        return true;
    }

    public void JoinNextMeeting()
    {
        var join = MeetingJoinCard.NextJoinable(Meetings, DateTime.Now, Settings.CalendarExcludedIds);
        if (join?.Link is null)
        {
            ShowMessage("No joinable meeting in the next two hours.");
            return;
        }

        PaletteCoordinator.HidePalette(restoreFocus: false);
        ProcessLauncher.OpenUri(join.Link.Url.ToString());
    }

    public void CopyNextMeetingLink()
    {
        var join = MeetingJoinCard.NextJoinable(Meetings, DateTime.Now, Settings.CalendarExcludedIds);
        if (join?.Link is null)
        {
            ShowMessage("No meeting link to copy.");
            return;
        }

        Clipboard.CopyText(join.Link.Url.ToString());
        ShowMessage("Copied meeting link");
        PaletteCoordinator.HidePalette();
    }

    public async Task CreateCalendarEventAsync()
    {
        try
        {
            var appointment = new Windows.ApplicationModel.Appointments.Appointment
            {
                StartTime = DateTimeOffset.Now.AddHours(1),
                Duration = TimeSpan.FromMinutes(30),
                Subject = "",
            };
            await Windows.ApplicationModel.Appointments.AppointmentManager.ShowAddAppointmentAsync(
                appointment,
                new Windows.Foundation.Rect(0, 0, 0, 0));
        }
        catch (Exception ex)
        {
            Log.Write("create event: " + ex.Message);
            ProcessLauncher.OpenUri("ms-calendar:");
        }
    }

    void UpdateTrayTitle()
    {
        var join = MeetingJoinCard.NextJoinable(Meetings, DateTime.Now, Settings.CalendarExcludedIds);
        var tip = join is null ? "Tinycast" : "Tinycast · " + join.Title;
        _tray?.SetTip(tip);
    }

    void DispatchHotKey(string id)
    {
        if (id is BuiltinCommands.TogglePalette or "toggle-palette")
        {
            PaletteCoordinator.TogglePalette();
            return;
        }

        LauncherCoordinator.Activate(id);
    }

    public string McpStatus() => McpHost.Status(McpServers, Settings.McpEnabled);

    public void Quit()
    {
        Log.Write("Quit");
        NativeMethods.RemoveClipboardFormatListener(PaletteWindow?.Hwnd ?? IntPtr.Zero);
        _meetingTimer?.Stop();
        _hook?.Dispose();
        _hotKeys?.Dispose();
        _tray?.Dispose();
        ClipboardStore.Dispose();
        AiCoordinator.Dispose();
        PaletteWindow?.Close();
        Application.Current.Exit();
        _mutex?.Dispose();
    }

    IntPtr? Filter(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (_hotKeys?.HandleMessage(msg, wParam) == true)
            return IntPtr.Zero;
        if (_tray?.HandleMessage(msg, wParam, lParam) == true)
            return IntPtr.Zero;
        if (msg == NativeMethods.WmClipboardUpdate)
        {
            Clipboard.Handle(msg);
            return IntPtr.Zero;
        }

        return null;
    }
}
