using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tinycast.DesignSystem;
using Tinycast.Features.Commands;
using Tinycast.Features.HotKeys;
using Tinycast.Features.Quicklinks;
using Tinycast.Features.Snippets;
using Tinycast.Platform;
using Windows.ApplicationModel.Appointments;

namespace Tinycast;

public sealed partial class SettingsWindow : Window
{
    readonly AppCore _core;
    bool _ready;
    HotKeyChord? _pendingChord;

    public SettingsWindow(AppCore core)
    {
        _core = core;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        WindowChrome.ResizeDips(this, Theme.Size.SettingsWindowWidth, Theme.Size.SettingsWindowHeight + 40);
        Load();
        _ready = true;
    }

    public void Reload()
    {
        _ready = false;
        Load();
        _ready = true;
    }

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
        RefreshHotKeys();
        AboutIdentity.Text = AppPaths.ChannelId + "  ·  " + UpdatesClient.InstalledLabel + "  ·  " + UpdatesClient.Repository;
        AboutMcp.Text = _core.McpStatus();
        GitHubTokenBox.Password = CredentialStore.Get(UpdatesClient.TokenKey) ?? "";
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

    void ShowPane(StackPanel pane)
    {
        GeneralPane.Visibility = Visibility.Collapsed;
        PermissionsPane.Visibility = Visibility.Collapsed;
        FeaturesPane.Visibility = Visibility.Collapsed;
        HotKeysPane.Visibility = Visibility.Collapsed;
        BackupPane.Visibility = Visibility.Collapsed;
        AboutPane.Visibility = Visibility.Collapsed;
        pane.Visibility = Visibility.Visible;
        if (pane == AboutPane)
            AboutMcp.Text = _core.McpStatus();
    }

    void OnGeneralNav(object sender, RoutedEventArgs e) => ShowPane(GeneralPane);
    void OnPermissionsNav(object sender, RoutedEventArgs e) => ShowPane(PermissionsPane);
    void OnFeaturesNav(object sender, RoutedEventArgs e) => ShowPane(FeaturesPane);
    void OnHotKeysNav(object sender, RoutedEventArgs e) => ShowPane(HotKeysPane);
    void OnBackupNav(object sender, RoutedEventArgs e) => ShowPane(BackupPane);
    void OnAboutNav(object sender, RoutedEventArgs e) => ShowPane(AboutPane);

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
        _core.ShowMessage("AI key stored in Credential Locker");
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
        OpenCodeStatus.Text = "OpenCode settings saved. Refresh provider status to reconnect.";
        _core.ShowMessage("OpenCode settings saved (password in Credential Locker, never backed up)");
    }

    async void OnRefreshOpenCode(object sender, RoutedEventArgs e)
    {
        OpenCodeStatus.Text = "Refreshing OpenCode providers…";
        await _core.AiCoordinator.RefreshAsync();
        OpenCodeStatus.Text = _core.AiCoordinator.Status
            + (_core.AiCoordinator.Version is { } v ? $" (v{v})" : "")
            + (_core.AiCoordinator.Models.Count > 0 ? $" · {_core.AiCoordinator.Models.Count} models" : "");
        if (!string.IsNullOrWhiteSpace(_core.AiCoordinator.SelectedModel))
            OpenCodeModelBox.Text = _core.AiCoordinator.SelectedModel;
        _core.Palette.Notify();
    }

    void OnAddSnippet(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SnippetNameBox.Text) || string.IsNullOrWhiteSpace(SnippetTextBox.Text))
            return;
        _core.Snippets.Add(new StoredSnippet(
            "snippet:" + Guid.NewGuid().ToString("n"),
            SnippetNameBox.Text.Trim(),
            SnippetKeywordBox.Text.Trim(),
            SnippetTextBox.Text));
        _core.PersistSnippets();
        SnippetNameBox.Text = SnippetKeywordBox.Text = SnippetTextBox.Text = "";
        _core.ShowMessage("Snippet saved");
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
        LinkNameBox.Text = LinkDestBox.Text = "";
        _core.ShowMessage("Quicklink saved");
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
        _core.ShowMessage("Command saved");
    }

    void OnAddAlias(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AliasIdBox.Text) || string.IsNullOrWhiteSpace(AliasTextBox.Text))
            return;
        _core.Aliases.Set(AliasIdBox.Text.Trim(), AliasTextBox.Text.Trim());
        _core.ShowMessage("Alias saved");
    }

    void OnHideItem(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AliasIdBox.Text))
            return;
        _core.Visibility.Set(AliasIdBox.Text.Trim(), true);
        _core.ShowMessage("Hidden from the launcher");
    }

    void OnShowItem(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AliasIdBox.Text))
            return;
        _core.Visibility.Remove(AliasIdBox.Text.Trim());
        _core.ShowMessage("Visible in the launcher");
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
            ? _pendingChord is { } pending ? "Captured " + pending.Label : "No conflicts."
            : conflicts.Count + " conflict(s). The later binding still registers.";
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

    void OnSaveGitHubToken(object sender, RoutedEventArgs e)
    {
        CredentialStore.Set(UpdatesClient.TokenKey, GitHubTokenBox.Password);
        _core.ShowMessage(string.IsNullOrWhiteSpace(GitHubTokenBox.Password)
            ? "GitHub token cleared"
            : "GitHub token stored in Credential Locker");
    }

    void OnExportBackup(object sender, RoutedEventArgs e) => _core.ExportBackup();
    void OnImportBackup(object sender, RoutedEventArgs e) => _core.ImportBackup();
    void OnCheckUpdates(object sender, RoutedEventArgs e) => _ = _core.CheckUpdates();
    void OnOpenSupport(object sender, RoutedEventArgs e) => _core.ShowSupport();
}
