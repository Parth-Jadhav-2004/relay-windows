using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tinycast.DesignSystem;
using Tinycast.Features.Commands;
using Tinycast.Features.Clipboard;
using Tinycast.Features.HotKeys;
using Tinycast.Features.Launcher;
using Tinycast.Features.Quicklinks;
using Tinycast.Features.Settings;
using Tinycast.Features.Snippets;
using Tinycast.Features.SystemActions;
using Tinycast.Features.WindowManagement;
using Tinycast.Platform;
using Windows.ApplicationModel.Appointments;

namespace Tinycast;

public sealed partial class SettingsWindow : Window
{
    readonly AppCore _core;
    readonly Dictionary<SettingsTab, StackPanel> _panes;
    bool _ready;
    HotKeyChord? _pendingChord;
    SettingsTab _tab = SettingsTab.General;
    string? _selectedAppId;
    string? _selectedSnippetId;
    string? _selectedLayoutId;

    public SettingsWindow(AppCore core)
    {
        _core = core;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        WindowChrome.ResizeDips(this, Theme.Size.SettingsWindowWidth, Theme.Size.SettingsWindowHeight + 40);
        _panes = new Dictionary<SettingsTab, StackPanel>
        {
            [SettingsTab.General] = GeneralPane,
            [SettingsTab.Permissions] = PermissionsPane,
            [SettingsTab.Hotkeys] = HotkeysPane,
            [SettingsTab.Applications] = ApplicationsPane,
            [SettingsTab.SystemSettings] = SystemSettingsPane,
            [SettingsTab.SystemActions] = SystemActionsPane,
            [SettingsTab.Commands] = CommandsPane,
            [SettingsTab.Quicklinks] = QuicklinksPane,
            [SettingsTab.Fallbacks] = FallbacksPane,
            [SettingsTab.Ai] = AiPane,
            [SettingsTab.QuickActions] = QuickActionsPane,
            [SettingsTab.FileSearch] = FileSearchPane,
            [SettingsTab.Notes] = NotesPane,
            [SettingsTab.Snippets] = SnippetsPane,
            [SettingsTab.Navigation] = NavigationPane,
            [SettingsTab.WindowManagement] = WindowManagementPane,
            [SettingsTab.Clipboard] = ClipboardPane,
            [SettingsTab.Emoji] = EmojiPane,
            [SettingsTab.Calendar] = CalendarPane,
            [SettingsTab.Extensions] = ExtensionsPane,
            [SettingsTab.Backup] = BackupPane,
            [SettingsTab.About] = AboutPane,
        };
        Load();
        SelectTab(SettingsTab.General);
        _ready = true;
    }

    public void Reload()
    {
        _ready = false;
        Load();
        SelectTab(_tab);
        _ready = true;
    }

    public void SelectTab(SettingsTab tab)
    {
        _tab = tab;
        foreach (var pane in _panes.Values)
            pane.Visibility = Visibility.Collapsed;
        _panes[tab].Visibility = Visibility.Visible;
        RebuildSidebar();
        if (tab == SettingsTab.About)
            RefreshAboutUpdates();

        if (tab == SettingsTab.Applications)
            BindApplications();
        if (tab == SettingsTab.Fallbacks)
            BindFallbacks();
        if (tab == SettingsTab.Snippets)
            BindSnippets();
        if (tab == SettingsTab.WindowManagement)
            BindLayouts();
        if (tab == SettingsTab.Clipboard)
            ClipboardFilterHint.Text = "Ctrl+P cycles " + ClipboardListFilterLogic.Label(_core.ClipboardCoordinator.Filter) + ".";
        if (tab == SettingsTab.Hotkeys)
            RefreshHotKeys();
    }

    void RebuildSidebar()
    {
        SidebarHost.Children.Clear();
        var query = SettingsSearch.Text?.Trim() ?? "";
        var panes = SettingsCatalog.Search(query);
        string? section = null;
        foreach (var pane in panes)
        {
            if (string.IsNullOrWhiteSpace(query) && pane.Section != section)
            {
                section = pane.Section;
                SidebarHost.Children.Add(new TextBlock
                {
                    Text = section,
                    Margin = new Thickness(8, 10, 8, 4),
                    Opacity = 0.6,
                    FontSize = 12,
                });
            }

            var tab = pane.Tab;
            var button = new Button
            {
                Content = pane.Title,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Tag = tab,
            };
            if (tab == _tab)
                button.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            button.Click += (_, _) => SelectTab(tab);
            SidebarHost.Children.Add(button);
        }
    }

    void OnSettingsSearch(object sender, TextChangedEventArgs e) => RebuildSidebar();

    void Load()
    {
        AppearanceBox.SelectedIndex = _core.Settings.Appearance switch
        {
            AppAppearance.Light => 1,
            AppAppearance.System => 2,
            _ => 0,
        };
        LaunchSwitch.IsOn = _core.Settings.LaunchAtLogin;
        TraySwitch.IsOn = _core.Settings.ShowInTray;
        TransparencySlider.Value = _core.Settings.PaletteTransparency;
        ClipboardSwitch.IsOn = _core.Settings.ClipboardEnabled;
        WindowSwitch.IsOn = _core.Settings.WindowManagementEnabled;
        FileSearchSwitch.IsOn = _core.Settings.FileSearchEnabled;
        NotesSwitch.IsOn = _core.Settings.NotesEnabled;
        QuicklinksSwitch.IsOn = _core.Settings.QuicklinksEnabled;
        CustomSwitch.IsOn = _core.Settings.CustomCommandsEnabled;
        NavSwitch.IsOn = _core.Settings.NavigationEnabled;
        SnippetsSwitch.IsOn = _core.Settings.SnippetsEnabled;
        AiSwitch.IsOn = _core.Settings.AiEnabled;
        CalendarSwitch.IsOn = _core.Settings.CalendarEnabled;
        AutoJoinSwitch.IsOn = _core.Settings.AutoJoinMeetings;
        CameraSwitch.IsOn = _core.Settings.CameraPreview;
        McpSwitch.IsOn = _core.Settings.McpEnabled;
        QuickActionsSwitch.IsOn = _core.Settings.QuickActionsEnabled;
        ExtensionsSwitch.IsOn = _core.Settings.ExtensionsEnabled;
        FileSearchScopesBox.Text = string.Join(Environment.NewLine, _core.Settings.FileSearchScopes);
        FileSearchIgnoreBox.Text = string.Join(Environment.NewLine, _core.Settings.FileSearchIgnorePatterns);
        GapSlider.Value = _core.Settings.WindowGap;
        CycleBox.SelectedIndex = _core.Settings.WindowCycle switch
        {
            "Off" => 0,
            "Displays" => 2,
            _ => 1,
        };
        EmojiColumnsSlider.Value = _core.Settings.EmojiColumns;
        AiModelBox.Text = _core.Ai.Model;
        AiEndpointBox.Text = _core.Ai.Endpoint;
        AiKeyBox.Password = CredentialStore.Get("ai") ?? "";
        OpenCodeBinaryBox.Text = _core.AiCoordinator.Settings.BinaryPath;
        OpenCodeUrlBox.Text = _core.AiCoordinator.Settings.ServerUrl;
        OpenCodePasswordBox.Password = CredentialStore.Get("opencode-server-password") ?? "";
        OpenCodeModelBox.Text = _core.AiCoordinator.SelectedModel;
        OpenCodeStatus.Text = _core.AiCoordinator.Status;
        HotKeyCommandBox.Items.Clear();
        foreach (var id in BindableCommands)
            HotKeyCommandBox.Items.Add(id);
        HotKeyCommandBox.SelectedIndex = 0;
        DoubleTapBox.SelectedIndex = 0;
        SystemSettingsList.ItemsSource = MsSettingsCatalog.All.Select(s => s.Title).ToList();
        SystemActionsList.ItemsSource = SystemActionCatalog.All.Select(a => a.Name).ToList();
        BindCustomCommands();
        BindQuicklinks();
        RefreshHotKeys();
        RefreshAboutUpdates();
    }

    public void RefreshAboutUpdates()
    {
        AboutIdentity.Text = AppPaths.ChannelId + "  ·  " + UpdatesClient.InstalledLabel + "  ·  " + UpdatesClient.Repository;
        AboutMcp.Text = _core.McpStatus();
        AboutUpdateStatus.Text = _core.AboutUpdateCopy;
        UpdatesButton.Content = _core.UpdatesButtonLabel;
        UpdatesButton.IsEnabled = !_core.UpdatesBusy;
    }

    static readonly string[] BindableCommands =
    [
        BuiltinCommands.TogglePalette,
        BuiltinCommands.Settings,
        BuiltinCommands.Clipboard,
        BuiltinCommands.Emoji,
        BuiltinCommands.FileSearch,
        BuiltinCommands.Notes,
        BuiltinCommands.Snippets,
        BuiltinCommands.Quicklinks,
        BuiltinCommands.SwitchWindows,
        BuiltinCommands.MenuSearch,
        BuiltinCommands.AiChat,
        BuiltinCommands.Schedule,
        BuiltinCommands.Camera,
        BuiltinCommands.Quit,
    ];

    void BindApplications()
    {
        var filter = ApplicationsFilter.Text?.Trim() ?? "";
        ApplicationsList.ItemsSource = _core.Apps
            .Where(a => filter.Length == 0 || a.Title.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Title)
            .Take(200)
            .Select(a => (_core.Visibility.IsHidden(a.Id) ? "Hidden  ·  " : "") + a.Title)
            .ToList();
    }

    void OnApplicationsFilter(object sender, TextChangedEventArgs e) => BindApplications();

    void OnApplicationSelected(object sender, SelectionChangedEventArgs e)
    {
        var filter = ApplicationsFilter.Text?.Trim() ?? "";
        var apps = _core.Apps
            .Where(a => filter.Length == 0 || a.Title.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Title)
            .Take(200)
            .ToList();
        _selectedAppId = ApplicationsList.SelectedIndex >= 0 && ApplicationsList.SelectedIndex < apps.Count
            ? apps[ApplicationsList.SelectedIndex].Id
            : null;
        ApplicationAliasBox.Text = _selectedAppId is null ? "" : _core.Aliases.Get(_selectedAppId) ?? "";
    }

    void OnSetApplicationAlias(object sender, RoutedEventArgs e)
    {
        if (_selectedAppId is null || string.IsNullOrWhiteSpace(ApplicationAliasBox.Text))
            return;
        _core.Aliases.Set(_selectedAppId, ApplicationAliasBox.Text.Trim());
        _core.ShowMessage("Alias saved");
    }

    void OnHideApplication(object sender, RoutedEventArgs e)
    {
        if (_selectedAppId is null)
            return;
        _core.Visibility.Set(_selectedAppId, true);
        BindApplications();
    }

    void OnShowApplication(object sender, RoutedEventArgs e)
    {
        if (_selectedAppId is null)
            return;
        _core.Visibility.Remove(_selectedAppId);
        BindApplications();
    }

    void BindFallbacks()
    {
        FallbacksList.ItemsSource = _core.Fallbacks
            .Select(f => (f.Enabled ? "On  ·  " : "Off  ·  ") + FallbackTitle(f.Id))
            .ToList();
    }

    string FallbackTitle(string id)
    {
        if (id.StartsWith("quicklink:", StringComparison.Ordinal))
        {
            var link = _core.Quicklinks.FirstOrDefault(q => q.Id == id);
            return link is null ? id : "Quicklink  ·  " + link.Name;
        }

        return FallbackCatalog.Title(id);
    }

    void OnToggleFallback(object sender, RoutedEventArgs e)
    {
        var index = FallbacksList.SelectedIndex;
        if (index < 0 || index >= _core.Fallbacks.Count)
            return;
        _core.Fallbacks[index].Enabled = !_core.Fallbacks[index].Enabled;
        _core.PersistFallbacks();
        BindFallbacks();
        FallbacksList.SelectedIndex = index;
    }

    void OnMoveFallbackUp(object sender, RoutedEventArgs e) => MoveFallback(-1);
    void OnMoveFallbackDown(object sender, RoutedEventArgs e) => MoveFallback(1);

    void MoveFallback(int delta)
    {
        var index = FallbacksList.SelectedIndex;
        var next = index + delta;
        if (index < 0 || next < 0 || next >= _core.Fallbacks.Count)
            return;
        (_core.Fallbacks[index], _core.Fallbacks[next]) = (_core.Fallbacks[next], _core.Fallbacks[index]);
        _core.PersistFallbacks();
        BindFallbacks();
        FallbacksList.SelectedIndex = next;
    }

    void BindCustomCommands()
    {
        CustomCommandsList.ItemsSource = _core.CustomCommands.Select(c => c.Name + "  ·  " + c.FileName).ToList();
    }

    void BindQuicklinks()
    {
        QuicklinksList.ItemsSource = _core.Quicklinks.Select(q => q.Name + "  ·  " + q.Destination).ToList();
    }

    void BindSnippets()
    {
        SnippetsList.ItemsSource = _core.Snippets.Select(s => s.Name + (string.IsNullOrWhiteSpace(s.Keyword) ? "" : "  ·  " + s.Keyword)).ToList();
    }

    void OnSnippetSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SnippetsList.SelectedIndex < 0 || SnippetsList.SelectedIndex >= _core.Snippets.Count)
        {
            _selectedSnippetId = null;
            return;
        }

        var snippet = _core.Snippets[SnippetsList.SelectedIndex];
        _selectedSnippetId = snippet.Id;
        SnippetNameBox.Text = snippet.Name;
        SnippetKeywordBox.Text = snippet.Keyword;
        SnippetTextBox.Text = snippet.Text;
    }

    void OnSaveSnippet(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SnippetNameBox.Text) || string.IsNullOrWhiteSpace(SnippetTextBox.Text))
            return;
        if (_selectedSnippetId is not null)
        {
            var index = _core.Snippets.FindIndex(s => s.Id == _selectedSnippetId);
            if (index >= 0)
            {
                _core.Snippets[index] = new StoredSnippet(
                    _selectedSnippetId,
                    SnippetNameBox.Text.Trim(),
                    SnippetKeywordBox.Text.Trim(),
                    SnippetTextBox.Text);
                _core.PersistSnippets();
                BindSnippets();
                _core.ShowMessage("Snippet saved");
                return;
            }
        }

        var created = new StoredSnippet(
            "snippet:" + Guid.NewGuid().ToString("n"),
            SnippetNameBox.Text.Trim(),
            SnippetKeywordBox.Text.Trim(),
            SnippetTextBox.Text);
        _core.Snippets.Add(created);
        _selectedSnippetId = created.Id;
        _core.PersistSnippets();
        BindSnippets();
        _core.ShowMessage("Snippet saved");
    }

    void OnRemoveSnippet(object sender, RoutedEventArgs e)
    {
        if (_selectedSnippetId is null)
            return;
        _core.Snippets.RemoveAll(s => s.Id == _selectedSnippetId);
        _selectedSnippetId = null;
        SnippetNameBox.Text = SnippetKeywordBox.Text = SnippetTextBox.Text = "";
        _core.PersistSnippets();
        BindSnippets();
    }

    void BindLayouts()
    {
        LayoutsList.ItemsSource = _core.Layouts.Select(l => l.Name + "  ·  " + l.Slots.Count).ToList();
    }

    void OnLayoutSelected(object sender, SelectionChangedEventArgs e)
    {
        if (LayoutsList.SelectedIndex < 0 || LayoutsList.SelectedIndex >= _core.Layouts.Count)
        {
            _selectedLayoutId = null;
            return;
        }

        var layout = _core.Layouts[LayoutsList.SelectedIndex];
        _selectedLayoutId = layout.Id;
        LayoutNameBox.Text = layout.Name;
        LayoutSlotsBox.Text = string.Join(Environment.NewLine, layout.Slots.Select(s =>
            s.ProcessName + "  " + (int)s.Frame.X + "  " + (int)s.Frame.Y + "  " + (int)s.Frame.Width + "  " + (int)s.Frame.Height));
    }

    void OnCaptureLayout(object sender, RoutedEventArgs e)
    {
        _core.SaveCurrentLayout();
        BindLayouts();
        if (_core.Layouts.Count > 0)
            LayoutsList.SelectedIndex = _core.Layouts.Count - 1;
    }

    void OnSaveLayoutEdit(object sender, RoutedEventArgs e)
    {
        if (_selectedLayoutId is null)
            return;
        var index = _core.Layouts.FindIndex(l => l.Id == _selectedLayoutId);
        if (index < 0)
            return;
        var slots = new List<WindowLayoutSlot>();
        foreach (var line in LayoutSlotsBox.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
                continue;
            if (!double.TryParse(parts[^4], out var x) || !double.TryParse(parts[^3], out var y)
                || !double.TryParse(parts[^2], out var w) || !double.TryParse(parts[^1], out var h))
                continue;
            var name = string.Join(' ', parts.Take(parts.Length - 4));
            slots.Add(new WindowLayoutSlot(name, new RectD(x, y, w, h), 0));
        }

        var nameText = string.IsNullOrWhiteSpace(LayoutNameBox.Text) ? _core.Layouts[index].Name : LayoutNameBox.Text.Trim();
        _core.Layouts[index] = new WindowLayout(_selectedLayoutId, nameText, slots);
        _core.PersistLayouts();
        BindLayouts();
        _core.ShowMessage("Layout saved");
    }

    void OnDeleteLayout(object sender, RoutedEventArgs e)
    {
        if (_selectedLayoutId is null)
            return;
        _core.Layouts.RemoveAll(l => l.Id == _selectedLayoutId);
        _selectedLayoutId = null;
        LayoutNameBox.Text = LayoutSlotsBox.Text = "";
        _core.PersistLayouts();
        BindLayouts();
    }

    void OnGapChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_ready)
            return;
        _core.Settings.WindowGap = (int)GapSlider.Value;
        _core.Persist();
    }

    void OnCycleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || CycleBox.SelectedItem is not ComboBoxItem item)
            return;
        _core.Settings.WindowCycle = item.Tag?.ToString() ?? "Sizes";
        _core.Persist();
    }

    void OnEmojiColumnsChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_ready)
            return;
        _core.Settings.EmojiColumns = (int)EmojiColumnsSlider.Value;
        _core.Persist();
        _core.Palette.Notify();
    }

    void OnAppearanceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || AppearanceBox.SelectedItem is not ComboBoxItem item)
            return;
        _core.Settings.Appearance = item.Tag?.ToString() switch
        {
            "Light" => AppAppearance.Light,
            "System" => AppAppearance.System,
            _ => AppAppearance.Dark,
        };
        _core.Persist();
        _core.ApplyAppearance(this);
    }

    void OnLaunchToggled(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;
        _core.Settings.LaunchAtLogin = LaunchSwitch.IsOn;
        LaunchAtLogin.SetEnabled(LaunchSwitch.IsOn);
        _core.Persist();
    }

    void OnTrayToggled(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;
        _core.Settings.ShowInTray = TraySwitch.IsOn;
        _core.Persist();
        _core.ApplyTrayVisibility();
    }

    void OnTransparencyChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_ready)
            return;
        _core.Settings.PaletteTransparency = (int)TransparencySlider.Value;
        _core.Persist();
        _core.PaletteWindow?.ApplySurface();
    }

    void OnFeatureToggled(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;
        _core.Settings.ClipboardEnabled = ClipboardSwitch.IsOn;
        _core.Settings.WindowManagementEnabled = WindowSwitch.IsOn;
        _core.Settings.FileSearchEnabled = FileSearchSwitch.IsOn;
        _core.Settings.NotesEnabled = NotesSwitch.IsOn;
        _core.Settings.QuicklinksEnabled = QuicklinksSwitch.IsOn;
        _core.Settings.CustomCommandsEnabled = CustomSwitch.IsOn;
        _core.Settings.NavigationEnabled = NavSwitch.IsOn;
        _core.Settings.SnippetsEnabled = SnippetsSwitch.IsOn;
        _core.Settings.AiEnabled = AiSwitch.IsOn;
        _core.Settings.CalendarEnabled = CalendarSwitch.IsOn;
        _core.Settings.AutoJoinMeetings = AutoJoinSwitch.IsOn;
        _core.Settings.CameraPreview = CameraSwitch.IsOn;
        _core.Settings.McpEnabled = McpSwitch.IsOn;
        _core.Settings.QuickActionsEnabled = QuickActionsSwitch.IsOn;
        _core.Settings.ExtensionsEnabled = ExtensionsSwitch.IsOn;
        _core.Persist();
        _core.PaletteWindow?.ApplySurface();
        _core.RefreshSnippetHook();
        if (CalendarSwitch.IsOn)
            _ = _core.RefreshCalendarAsync();
    }

    void OnFileSearchPolicyChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;
        _core.Settings.FileSearchScopes = SplitLines(FileSearchScopesBox.Text);
        _core.Settings.FileSearchIgnorePatterns = SplitLines(FileSearchIgnoreBox.Text);
        _core.Persist();
        _core.FileSearchCoordinator.Reset();
    }

    static List<string> SplitLines(string text) =>
        text.Split(['\n', '\r', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    async void OnRequestCalendar(object sender, RoutedEventArgs e)
    {
        try
        {
            await AppointmentManager.RequestStoreAsync(AppointmentStoreAccessType.AllCalendarsReadOnly);
            _core.Settings.CalendarEnabled = true;
            CalendarSwitch.IsOn = true;
            _core.Persist();
            await _core.RefreshCalendarAsync();
            _core.ShowMessage("Calendar store requested");
        }
        catch (Exception ex)
        {
            _core.ShowMessage(ex.Message, DialogTone.Danger);
        }
    }

    void OnSaveAi(object sender, RoutedEventArgs e)
    {
        CredentialStore.Set("ai", AiKeyBox.Password);
        _core.Ai.Model = string.IsNullOrWhiteSpace(AiModelBox.Text) ? "gpt-4o-mini" : AiModelBox.Text.Trim();
        _core.Ai.Endpoint = string.IsNullOrWhiteSpace(AiEndpointBox.Text)
            ? "https://api.openai.com/v1/chat/completions"
            : AiEndpointBox.Text.Trim();
        AiClient.Save(_core.Ai);
        _core.ShowMessage("AI key stored");
    }

    void OnSaveOpenCode(object sender, RoutedEventArgs e)
    {
        CredentialStore.Set("opencode-server-password", OpenCodePasswordBox.Password);
        _core.AiCoordinator.Settings.BinaryPath = string.IsNullOrWhiteSpace(OpenCodeBinaryBox.Text)
            ? "opencode"
            : OpenCodeBinaryBox.Text.Trim();
        _core.AiCoordinator.Settings.ServerUrl = OpenCodeUrlBox.Text.Trim();
        var model = OpenCodeModelBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(model))
            _core.AiCoordinator.SelectedModel = model;
        _core.AiCoordinator.Save();
        OpenCodeStatus.Text = "Saved. Refresh to reconnect.";
    }

    async void OnRefreshOpenCode(object sender, RoutedEventArgs e)
    {
        OpenCodeStatus.Text = "Refreshing…";
        await _core.AiCoordinator.RefreshAsync();
        OpenCodeStatus.Text = _core.AiCoordinator.Status
            + (_core.AiCoordinator.Version is { } v ? $" (v{v})" : "")
            + (_core.AiCoordinator.Models.Count > 0 ? $" · {_core.AiCoordinator.Models.Count} models" : "");
        if (!string.IsNullOrWhiteSpace(_core.AiCoordinator.SelectedModel))
            OpenCodeModelBox.Text = _core.AiCoordinator.SelectedModel;
        _core.Palette.Notify();
    }

    void OnAddQuicklink(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(LinkNameBox.Text) || string.IsNullOrWhiteSpace(LinkDestBox.Text))
            return;
        var dest = LinkDestBox.Text.Trim();
        _core.Quicklinks.Add(new Quicklink(
            "quicklink:" + Guid.NewGuid().ToString("n"),
            LinkNameBox.Text.Trim(),
            dest,
            QuicklinkDestination.Classify(dest),
            null));
        _core.PersistQuicklinks();
        _core.RefreshFallbacks();
        LinkNameBox.Text = LinkDestBox.Text = "";
        BindQuicklinks();
    }

    void OnRemoveQuicklink(object sender, RoutedEventArgs e)
    {
        if (QuicklinksList.SelectedIndex < 0 || QuicklinksList.SelectedIndex >= _core.Quicklinks.Count)
            return;
        _core.Quicklinks.RemoveAt(QuicklinksList.SelectedIndex);
        _core.PersistQuicklinks();
        _core.RefreshFallbacks();
        BindQuicklinks();
    }

    void OnAddCustom(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CmdNameBox.Text) || string.IsNullOrWhiteSpace(CmdFileBox.Text))
            return;
        _core.CustomCommands.Add(new CustomCommand(
            "custom:" + Guid.NewGuid().ToString("n"),
            CmdNameBox.Text.Trim(),
            CmdFileBox.Text.Trim(),
            [],
            true));
        _core.PersistCustomCommands();
        CmdNameBox.Text = CmdFileBox.Text = "";
        BindCustomCommands();
    }

    void OnRemoveCustom(object sender, RoutedEventArgs e)
    {
        if (CustomCommandsList.SelectedIndex < 0 || CustomCommandsList.SelectedIndex >= _core.CustomCommands.Count)
            return;
        _core.CustomCommands.RemoveAt(CustomCommandsList.SelectedIndex);
        _core.PersistCustomCommands();
        BindCustomCommands();
    }

    void RefreshHotKeys()
    {
        HotKeyList.Items.Clear();
        foreach (var binding in _core.HotKeys)
        {
            var chord = binding.Chord?.Label ?? "—";
            var tap = binding.DoubleTap is { } d ? " double-" + d : "";
            var app = string.IsNullOrEmpty(binding.AppPath) ? "" : " @ " + binding.AppPath;
            HotKeyList.Items.Add(binding.CommandId + "  " + chord + tap + app);
        }

        var conflicts = HotKeyConflicts.Find(_core.HotKeys);
        HotKeyStatus.Text = conflicts.Count == 0
            ? _pendingChord is { } pending ? "Captured " + pending.Label : ""
            : conflicts.Count + " conflict(s).";
    }

    void OnRecordHotKey(object sender, RoutedEventArgs e)
    {
        HotKeyStatus.Text = "Press a shortcut…";
        _core.RecordHotKey(chord =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _pendingChord = chord;
                HotKeyStatus.Text = "Captured " + chord.Label + (chord.IsHyper ? " (Hyper)" : "");
            });
        });
    }

    void OnAddHotKey(object sender, RoutedEventArgs e)
    {
        var command = HotKeyCommandBox.SelectedItem as string ?? BuiltinCommands.TogglePalette;
        DoubleTapModifier? tap = null;
        if (DoubleTapBox.SelectedItem is ComboBoxItem item && item.Tag is string tag && tag.Length > 0
            && Enum.TryParse<DoubleTapModifier>(tag, out var parsed))
            tap = parsed;
        if (_pendingChord is null && tap is null)
        {
            HotKeyStatus.Text = "Record a chord or pick a double-tap.";
            return;
        }

        _core.HotKeys.Add(new HotKeyBinding
        {
            CommandId = command,
            Chord = _pendingChord,
            DoubleTap = tap,
            AppPath = string.IsNullOrWhiteSpace(HotKeyAppBox.Text) ? null : HotKeyAppBox.Text.Trim(),
        });
        _pendingChord = null;
        _core.PersistHotKeys();
        RefreshHotKeys();
    }

    void OnRemoveHotKey(object sender, RoutedEventArgs e)
    {
        var index = HotKeyList.SelectedIndex;
        if (index < 0 || index >= _core.HotKeys.Count)
            return;
        _core.HotKeys.RemoveAt(index);
        _core.PersistHotKeys();
        RefreshHotKeys();
    }

    void OnOpenNotes(object sender, RoutedEventArgs e) => _core.ShowNotes();
    void OnExportBackup(object sender, RoutedEventArgs e) => _core.ExportBackup();
    void OnImportBackup(object sender, RoutedEventArgs e) => _core.ImportBackup();
    void OnUpdatesButton(object sender, RoutedEventArgs e)
    {
        if (_core.CanInstallUpdate)
            _ = _core.InstallPendingUpdate();
        else
            _ = _core.CheckUpdates();
    }
    void OnOpenSupport(object sender, RoutedEventArgs e) => _core.ShowSupport();
    void OnReplayOnboarding(object sender, RoutedEventArgs e) => _core.ShowOnboarding(force: true);
}
