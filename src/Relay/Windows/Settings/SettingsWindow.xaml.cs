using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Relay.DesignSystem;
using Relay.Features.Backup;
using Relay.Features.Commands;
using Relay.Features.Clipboard;
using Relay.Features.HotKeys;
using Relay.Features.Launcher;
using Relay.Features.Quicklinks;
using Relay.Features.Settings;
using Relay.Features.Snippets;
using Relay.Features.SystemActions;
using Relay.Features.WindowManagement;
using Relay.Platform;
using Windows.ApplicationModel.Appointments;

namespace Relay;

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
    string? _selectedCustomId;

    public SettingsWindow(AppCore core)
    {
        _core = core;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        WindowChrome.ResizeDips(this, Theme.Size.SettingsWindowWidth, Theme.Size.SettingsWindowHeight + 40);
        if (Content is Grid root && root.ColumnDefinitions.Count > 0)
            root.ColumnDefinitions[0].Width = new GridLength(Theme.Size.SettingsSidebar + Theme.Size.SettingsSidebarScrollGutter);
        SidebarHost.Margin = new Thickness(0, 0, Theme.Size.SettingsSidebarScrollGutter, 0);
        SettingsSearch.Margin = new Thickness(0, 0, Theme.Size.SettingsSidebarScrollGutter, 8);
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
            ClipboardFilterHint.Text = "Ctrl+P cycles " + ClipboardListFilterLogic.Label(_core.ClipboardCoordinator.Filter) + ". Number keys paste pinned items.";
        if (tab == SettingsTab.Hotkeys)
            RefreshHotKeys();
        if (tab == SettingsTab.Permissions)
            RefreshPermissions();
        if (tab == SettingsTab.Backup)
            RefreshBackupChecks();
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
        InterfaceSizeBox.SelectedIndex = _core.Settings.InterfaceSize switch
        {
            "compact" => 0,
            "large" => 2,
            _ => 1,
        };
        CompactPaletteSwitch.IsOn = _core.Settings.CompactPalette;
        RememberPositionSwitch.IsOn = _core.Settings.PaletteRememberPosition;
        EscapeClearsSwitch.IsOn = _core.Settings.PaletteEscapeClearsQuery;
        PopToRootBox.Value = _core.Settings.PalettePopToRootSeconds;
        ClipboardSwitch.IsOn = _core.Settings.ClipboardEnabled;
        WindowSwitch.IsOn = _core.Settings.WindowManagementEnabled;
        LayoutsShowSwitch.IsOn = _core.Settings.WindowLayoutsShowInLauncher;
        FileSearchSwitch.IsOn = _core.Settings.FileSearchEnabled;
        NotesSwitch.IsOn = _core.Settings.NotesEnabled;
        QuicklinksSwitch.IsOn = _core.Settings.QuicklinksEnabled;
        CustomSwitch.IsOn = _core.Settings.CustomCommandsEnabled;
        CustomShowSwitch.IsOn = _core.Settings.CustomCommandsShowInLauncher;
        NavSwitch.IsOn = _core.Settings.NavigationEnabled;
        SnippetsSwitch.IsOn = _core.Settings.SnippetsEnabled;
        SnippetsShowSwitch.IsOn = _core.Settings.SnippetsShowInLauncher;
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
        EmojiSkinBox.SelectedIndex = Math.Clamp(_core.Settings.EmojiSkinTone, 0, 5);
        ClipboardOcrSwitch.IsOn = _core.Settings.ClipboardOcrEnabled;
        ClipboardKeepOpenSwitch.IsOn = _core.Settings.ClipboardKeepOpen;
        ClipboardActionBox.SelectedIndex = string.Equals(_core.Settings.ClipboardDefaultAction, "copy", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        ClipboardRetentionBox.Value = _core.Settings.ClipboardRetentionDays;
        ClipboardIgnoredBox.Text = string.Join(Environment.NewLine, _core.Settings.ClipboardIgnoredApps);
        NavExcludedBox.Text = string.Join(Environment.NewLine, _core.Settings.NavigationExcludedApps);
        CalendarExcludedBox.Text = string.Join(Environment.NewLine, _core.Settings.CalendarExcludedIds);
        AiModelBox.Text = _core.Ai.Model;
        AiEndpointBox.Text = _core.Ai.Endpoint;
        AiKeyBox.Password = CredentialStore.Get("ai") ?? "";
        OpenCodeBinaryBox.Text = _core.AiCoordinator.Settings.BinaryPath;
        OpenCodeUrlBox.Text = _core.AiCoordinator.Settings.ServerUrl;
        OpenCodePasswordBox.Password = CredentialStore.Get("opencode-server-password") ?? "";
        OpenCodeModelBox.Text = _core.AiCoordinator.SelectedModel;
        OpenCodeStatus.Text = _core.AiCoordinator.Status;
        HotKeyCommandBox.Items.Clear();
        foreach (var id in BindableCommands())
            HotKeyCommandBox.Items.Add(id);
        HotKeyCommandBox.SelectedIndex = 0;
        DoubleTapBox.SelectedIndex = 0;
        SystemSettingsList.ItemsSource = MsSettingsCatalog.All.Select(s => s.Title).ToList();
        SystemActionsList.ItemsSource = SystemActionCatalog.All.Select(a => a.Name).ToList();
        BindCustomCommands();
        CmdEnabledSwitch.IsOn = true;
        CmdConfirmSwitch.IsOn = true;
        BindQuicklinks();
        RefreshHotKeys();
        RefreshAboutUpdates();
        RefreshPermissions();
        RefreshBackupChecks();
    }

    public void RefreshAboutUpdates()
    {
        AboutIdentity.Text = UpdatesClient.InstalledLabel;
        AboutMcp.Visibility = Visibility.Collapsed;
        AboutUpdateStatus.Text = _core.AboutUpdateCopy;
        UpdatesButton.Content = _core.UpdatesButtonLabel;
        UpdatesButton.IsEnabled = !_core.UpdatesBusy;
    }

    IReadOnlyList<string> BindableCommands()
    {
        var ids = new List<string>
        {
            BuiltinCommands.TogglePalette,
            BuiltinCommands.Settings,
            BuiltinCommands.Clipboard,
            BuiltinCommands.Emoji,
            BuiltinCommands.FileSearch,
            BuiltinCommands.Notes,
            BuiltinCommands.SearchNotes,
            BuiltinCommands.RevealNotes,
            BuiltinCommands.CalculatorHistory,
            BuiltinCommands.Snippets,
            BuiltinCommands.Quicklinks,
            BuiltinCommands.SwitchWindows,
            BuiltinCommands.MenuSearch,
            BuiltinCommands.Uninstall,
            BuiltinCommands.Support,
            BuiltinCommands.Backup,
            BuiltinCommands.Updates,
            BuiltinCommands.Camera,
            BuiltinCommands.AiChat,
            BuiltinCommands.Schedule,
            BuiltinCommands.SaveLayout,
            BuiltinCommands.CreateLayout,
            BuiltinCommands.JoinNext,
            BuiltinCommands.CreateEvent,
            BuiltinCommands.CopyMeetingLink,
            BuiltinCommands.OpenCalendar,
        };
        ids.AddRange(WindowCommandCatalog.All.Select(c => c.EntryId));
        ids.AddRange(SystemActionCatalog.All.Select(a => a.EntryId));
        ids.AddRange(_core.CustomCommands.Select(c => c.Id));
        ids.AddRange(_core.Layouts.Select(l => "layout:" + l.Id));
        ids.AddRange(_core.Quicklinks.Select(q => q.Id));
        return ids.Where(CommandAvailability.IsBindable).ToList();
    }

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

    void BindQuicklinks()
    {
        QuicklinksList.ItemsSource = _core.Quicklinks.Select(q => q.Name + "  ·  " + q.Destination).ToList();
    }

    void BindSnippets()
    {
        SnippetsList.ItemsSource = _core.Snippets.Select(s => s.Name + (string.IsNullOrWhiteSpace(s.Keyword) ? "" : "  ·  " + s.Keyword)).ToList();
        var conflicts = SnippetFrontmatter.ConflictingKeywords(_core.Snippets);
        SnippetConflictText.Text = conflicts.Count == 0
            ? "Markdown files with name/keyword frontmatter load from the snippets folder."
            : "Conflicting keywords: " + string.Join(", ", conflicts);
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
        SnippetEnabledSwitch.IsOn = snippet.Enabled;
        SnippetConfirmSwitch.IsOn = snippet.ShowConfirmation;
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
                    SnippetTextBox.Text,
                    SnippetEnabledSwitch.IsOn,
                    SnippetConfirmSwitch.IsOn);
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
            SnippetTextBox.Text,
            SnippetEnabledSwitch.IsOn,
            SnippetConfirmSwitch.IsOn);
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
            s.ProcessName + "  " + (int)s.Frame.X + "  " + (int)s.Frame.Y + "  " + (int)s.Frame.Width + "  " + (int)s.Frame.Height
            + (string.IsNullOrWhiteSpace(s.Path) ? "" : "  " + s.Path)));
        var first = layout.Slots.FirstOrDefault();
        if (first is not null)
        {
            LayoutWidthBox.Value = first.WidthFraction > 0 ? first.WidthFraction : 0.5;
            LayoutHeightBox.Value = first.HeightFraction > 0 ? first.HeightFraction : 0.5;
            var spell = string.IsNullOrWhiteSpace(first.Anchor) ? "center" : first.Anchor;
            for (var i = 0; i < LayoutAnchorBox.Items.Count; i++)
            {
                if (LayoutAnchorBox.Items[i] is ComboBoxItem item && (item.Tag?.ToString() ?? "") == spell)
                    LayoutAnchorBox.SelectedIndex = i;
            }
        }
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
        var existing = _core.Layouts[index];
        var displays = WindowInventory.Displays();
        var anchor = WindowLayoutGeometry.ParseAnchor((LayoutAnchorBox.SelectedItem as ComboBoxItem)?.Tag?.ToString());
        var widthFrac = LayoutWidthBox.Value is double wFrac && wFrac > 0 ? wFrac : 0;
        var heightFrac = LayoutHeightBox.Value is double hFrac && hFrac > 0 ? hFrac : 0;
        var slots = new List<WindowLayoutSlot>();
        foreach (var line in LayoutSlotsBox.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
                continue;
            string? path = null;
            double x, y, w, h;
            string name;
            if (parts.Length >= 6
                && double.TryParse(parts[^5], out x) && double.TryParse(parts[^4], out y)
                && double.TryParse(parts[^3], out w) && double.TryParse(parts[^2], out h))
            {
                path = parts[^1];
                name = string.Join(' ', parts.Take(parts.Length - 5));
            }
            else if (double.TryParse(parts[^4], out x) && double.TryParse(parts[^3], out y)
                     && double.TryParse(parts[^2], out w) && double.TryParse(parts[^1], out h))
            {
                name = string.Join(' ', parts.Take(parts.Length - 4));
            }
            else
                continue;

            var frame = new RectD(x, y, w, h);
            var previous = existing.Slots.FirstOrDefault(s => s.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase));
            var display = WindowLayoutGeometry.MatchDisplay(previous?.DisplayId ?? "", previous?.ScreenId ?? 0, displays)
                          ?? displays.FirstOrDefault();
            if (display is null)
            {
                slots.Add(new WindowLayoutSlot(name, frame, 0, path));
                continue;
            }

            var entry = WindowLayoutGeometry.Describe(frame, display.VisibleFrame, name, path ?? previous?.Path, display.Id, previous?.EntryId);
            if (widthFrac > 0 && heightFrac > 0)
                entry = entry with { WidthFraction = widthFrac, HeightFraction = heightFrac, Anchor = anchor };
            var screen = Math.Max(0, displays.ToList().FindIndex(d => d.Id == display.Id));
            slots.Add(WindowLayoutGeometry.SlotFromEntry(entry, WindowLayoutGeometry.Resolve(entry, display.VisibleFrame), screen));
        }

        var nameText = string.IsNullOrWhiteSpace(LayoutNameBox.Text) ? existing.Name : LayoutNameBox.Text.Trim();
        _core.Layouts[index] = new WindowLayout(_selectedLayoutId, nameText, slots, existing.FrontmostEntryId, existing.UsesPreferredGap);
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
        _core.Settings.WindowLayoutsShowInLauncher = LayoutsShowSwitch.IsOn;
        _core.Settings.FileSearchEnabled = FileSearchSwitch.IsOn;
        _core.Settings.NotesEnabled = NotesSwitch.IsOn;
        _core.Settings.QuicklinksEnabled = QuicklinksSwitch.IsOn;
        _core.Settings.CustomCommandsEnabled = CustomSwitch.IsOn;
        _core.Settings.CustomCommandsShowInLauncher = CustomShowSwitch.IsOn;
        _core.Settings.NavigationEnabled = NavSwitch.IsOn;
        _core.Settings.SnippetsEnabled = SnippetsSwitch.IsOn;
        _core.Settings.SnippetsShowInLauncher = SnippetsShowSwitch.IsOn;
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

    void BindCustomCommands()
    {
        CustomCommandsList.ItemsSource = _core.CustomCommands.Select(c =>
            (c.Enabled ? "" : "Off  ·  ") + c.Name + "  ·  " + c.FileName).ToList();
    }

    void OnCustomSelected(object sender, SelectionChangedEventArgs e)
    {
        if (CustomCommandsList.SelectedIndex < 0 || CustomCommandsList.SelectedIndex >= _core.CustomCommands.Count)
        {
            _selectedCustomId = null;
            return;
        }

        var command = _core.CustomCommands[CustomCommandsList.SelectedIndex];
        _selectedCustomId = command.Id;
        CmdNameBox.Text = command.Name;
        CmdFileBox.Text = command.FileName;
        CmdArgsBox.Text = string.Join(' ', command.Arguments);
        CmdCwdBox.Text = command.WorkingDirectory;
        CmdParamsBox.Text = string.Join(Environment.NewLine, command.Parameters.Select(p => p.Name));
        CmdEnabledSwitch.IsOn = command.Enabled;
        CmdConfirmSwitch.IsOn = command.Confirm;
        CmdOutputSwitch.IsOn = command.ShowOutput;
        CmdEnvSwitch.IsOn = command.LoadEnvironment;
        CmdHudSwitch.IsOn = command.ShowConfirmation;
    }

    void OnAddCustom(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CmdNameBox.Text) || string.IsNullOrWhiteSpace(CmdFileBox.Text))
            return;
        var parameters = SplitLines(CmdParamsBox.Text)
            .Select(name => new CustomCommandParameter { Name = name, Required = true })
            .ToList();
        var command = _selectedCustomId is not null
            ? _core.CustomCommands.FirstOrDefault(c => c.Id == _selectedCustomId)
            : null;
        if (command is null)
        {
            command = new CustomCommand { Id = "custom:" + Guid.NewGuid().ToString("n") };
            _core.CustomCommands.Add(command);
            _selectedCustomId = command.Id;
        }

        command.Name = CmdNameBox.Text.Trim();
        command.FileName = CmdFileBox.Text.Trim();
        command.Arguments = SplitArgs(CmdArgsBox.Text).ToList();
        command.WorkingDirectory = CmdCwdBox.Text.Trim();
        command.Parameters = parameters;
        command.Enabled = CmdEnabledSwitch.IsOn;
        command.Confirm = CmdConfirmSwitch.IsOn;
        command.ShowOutput = CmdOutputSwitch.IsOn;
        command.LoadEnvironment = CmdEnvSwitch.IsOn;
        command.ShowConfirmation = CmdHudSwitch.IsOn;
        _core.PersistCustomCommands();
        BindCustomCommands();
        _core.ShowMessage("Command saved");
    }

    async void OnImportCommandScript(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add(".ps1");
        picker.FileTypeFilter.Add(".cmd");
        picker.FileTypeFilter.Add(".bat");
        picker.FileTypeFilter.Add(".py");
        picker.FileTypeFilter.Add(".js");
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowChrome.Hwnd(this));
        var file = await picker.PickSingleFileAsync();
        if (file is null)
            return;
        var parsed = RaycastScriptImport.Parse(file.Path, await File.ReadAllTextAsync(file.Path));
        if (parsed is null)
        {
            _core.ShowMessage("Need a shebang and @raycast.title header.", DialogTone.Neutral);
            return;
        }

        _core.CustomCommands.Add(parsed);
        _core.PersistCustomCommands();
        BindCustomCommands();
        _core.ShowMessage("Imported " + parsed.Name);
    }

    void OnRemoveCustom(object sender, RoutedEventArgs e)
    {
        if (CustomCommandsList.SelectedIndex < 0 || CustomCommandsList.SelectedIndex >= _core.CustomCommands.Count)
            return;
        _core.CustomCommands.RemoveAt(CustomCommandsList.SelectedIndex);
        _selectedCustomId = null;
        var live = new HashSet<string>(BindableCommands(), StringComparer.OrdinalIgnoreCase);
        _core.HotKeys.RemoveAll(b => !live.Contains(b.CommandId));
        _core.PersistCustomCommands();
        _core.PersistHotKeys();
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
            ? _pendingChord is { } pending ? "Captured " + pending.Label : "Select a row and Rebind selected to replace its chord."
            : string.Join(" · ", conflicts.Select(c => c.A.CommandId + " conflicts with " + c.B.CommandId));
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
    void OnRevealNotes(object sender, RoutedEventArgs e) => ProcessLauncher.Open(AppPaths.NotesDir);
    void OnExportBackup(object sender, RoutedEventArgs e)
    {
        SyncBackupCategories();
        _core.ExportBackup();
    }
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

    void OnPaletteChromeChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;
        SavePaletteChrome();
    }

    void OnInterfaceSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready)
            return;
        SavePaletteChrome();
    }

    void SavePaletteChrome()
    {
        if (InterfaceSizeBox.SelectedItem is ComboBoxItem size)
            _core.Settings.InterfaceSize = size.Tag?.ToString() ?? "standard";
        _core.Settings.CompactPalette = CompactPaletteSwitch.IsOn;
        _core.Settings.PaletteRememberPosition = RememberPositionSwitch.IsOn;
        _core.Settings.PaletteEscapeClearsQuery = EscapeClearsSwitch.IsOn;
        _core.Persist();
        _core.PaletteWindow?.ResizePalette();
        _core.PaletteWindow?.ApplySurface();
    }

    void OnPopToRootChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_ready)
            return;
        _core.Settings.PalettePopToRootSeconds = (int)Math.Clamp(PopToRootBox.Value, 0, 120);
        _core.Persist();
    }

    void OnClipboardPolicyChanged(object sender, RoutedEventArgs e) => SaveClipboardPolicy();
    void OnClipboardActionChanged(object sender, SelectionChangedEventArgs e) => SaveClipboardPolicy();

    void SaveClipboardPolicy()
    {
        if (!_ready)
            return;
        _core.Settings.ClipboardOcrEnabled = ClipboardOcrSwitch.IsOn;
        _core.Settings.ClipboardKeepOpen = ClipboardKeepOpenSwitch.IsOn;
        if (ClipboardActionBox.SelectedItem is ComboBoxItem action)
            _core.Settings.ClipboardDefaultAction = action.Tag?.ToString() ?? "paste";
        _core.Settings.ClipboardIgnoredApps = SplitLines(ClipboardIgnoredBox.Text);
        _core.Persist();
    }

    void OnClipboardRetentionChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_ready)
            return;
        _core.Settings.ClipboardRetentionDays = (int)Math.Clamp(ClipboardRetentionBox.Value, 0, 3650);
        _core.Persist();
    }

    void OnEmojiSkinChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || EmojiSkinBox.SelectedItem is not ComboBoxItem item)
            return;
        if (int.TryParse(item.Tag?.ToString(), out var tone))
            _core.Settings.EmojiSkinTone = Math.Clamp(tone, 0, 5);
        _core.Persist();
        _core.Palette.Notify();
    }

    void OnNavigationExcludedChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;
        _core.Settings.NavigationExcludedApps = SplitLines(NavExcludedBox.Text);
        _core.Persist();
    }

    void OnCalendarExcludedChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;
        _core.Settings.CalendarExcludedIds = SplitLines(CalendarExcludedBox.Text);
        _core.Persist();
        _ = _core.RefreshCalendarAsync();
    }

    void OnRecordSelectedHotKey(object sender, RoutedEventArgs e)
    {
        var index = HotKeyList.SelectedIndex;
        if (index < 0 || index >= _core.HotKeys.Count)
        {
            HotKeyStatus.Text = "Select a binding first.";
            return;
        }

        HotKeyStatus.Text = "Press a shortcut…";
        var target = index;
        _core.RecordHotKey(chord =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _core.HotKeys[target].Chord = chord;
                _core.PersistHotKeys();
                RefreshHotKeys();
                HotKeyStatus.Text = "Updated " + _core.HotKeys[target].CommandId + " to " + chord.Label;
            });
        });
    }

    void RefreshPermissions()
    {
        var lines = new List<string>
        {
            "Calendar: " + (_core.Settings.CalendarEnabled ? "enabled in Relay" : "off until you request access"),
            "Camera preview: " + (_core.Settings.CameraPreview ? "on" : "off"),
            "Microphone is only used if an app you launch asks for it.",
        };
        PermissionsStatus.Text = string.Join(Environment.NewLine, lines);
    }

    void OnOpenPrivacyCalendar(object sender, RoutedEventArgs e) => ProcessLauncher.OpenUri("ms-settings:privacy-calendar");
    void OnOpenPrivacyCamera(object sender, RoutedEventArgs e) => ProcessLauncher.OpenUri("ms-settings:privacy-webcam");
    void OnOpenPrivacyMicrophone(object sender, RoutedEventArgs e) => ProcessLauncher.OpenUri("ms-settings:privacy-microphone");
    void OnOpenWindowsSettings(object sender, RoutedEventArgs e) => ProcessLauncher.OpenUri("ms-settings:");

    void RefreshBackupChecks()
    {
        BackupSettingsCheck.IsChecked = _core.BackupCategories.Contains(BackupArchive.SettingsAndShortcuts);
        BackupSnippetsCheck.IsChecked = _core.BackupCategories.Contains(BackupArchive.Snippets);
        BackupNotesCheck.IsChecked = _core.BackupCategories.Contains(BackupArchive.Notes);
        BackupLearningCheck.IsChecked = _core.BackupCategories.Contains(BackupArchive.Learning);
        BackupClipboardCheck.IsChecked = _core.BackupCategories.Contains(BackupArchive.Clipboard);
        BackupStatus.Text = "Imports refuse archives from a newer schema. Capability flags are never restored.";
    }

    void SyncBackupCategories()
    {
        _core.BackupCategories.Clear();
        if (BackupSettingsCheck.IsChecked == true)
            _core.BackupCategories.Add(BackupArchive.SettingsAndShortcuts);
        if (BackupSnippetsCheck.IsChecked == true)
            _core.BackupCategories.Add(BackupArchive.Snippets);
        if (BackupNotesCheck.IsChecked == true)
            _core.BackupCategories.Add(BackupArchive.Notes);
        if (BackupLearningCheck.IsChecked == true)
            _core.BackupCategories.Add(BackupArchive.Learning);
        if (BackupClipboardCheck.IsChecked == true)
            _core.BackupCategories.Add(BackupArchive.Clipboard);
    }

    async void OnExportQuicklinks(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowChrome.Hwnd(this));
            picker.SuggestedFileName = "relay-quicklinks";
            picker.FileTypeChoices.Add("JSON", [".json"]);
            var file = await picker.PickSaveFileAsync();
            if (file is null)
                return;
            await File.WriteAllTextAsync(file.Path, System.Text.Json.JsonSerializer.Serialize(_core.Quicklinks, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            _core.ShowMessage("Quicklinks exported");
        }
        catch (Exception ex)
        {
            _core.ShowMessage(ex.Message, DialogTone.Danger);
        }
    }

    async void OnImportQuicklinks(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowChrome.Hwnd(this));
            picker.FileTypeFilter.Add(".json");
            var file = await picker.PickSingleFileAsync();
            if (file is null)
                return;
            var incoming = System.Text.Json.JsonSerializer.Deserialize<List<Quicklink>>(await File.ReadAllTextAsync(file.Path)) ?? [];
            foreach (var link in incoming)
            {
                if (_core.Quicklinks.Any(q => q.Id == link.Id))
                    continue;
                _core.Quicklinks.Add(link);
            }

            _core.PersistQuicklinks();
            _core.RefreshFallbacks();
            BindQuicklinks();
            _core.ShowMessage("Quicklinks imported");
        }
        catch (Exception ex)
        {
            _core.ShowMessage(ex.Message, DialogTone.Danger);
        }
    }

    static IReadOnlyList<string> SplitArgs(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
