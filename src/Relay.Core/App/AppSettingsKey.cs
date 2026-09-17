namespace Relay;

/// <summary>Every persisted setting has one key. SettingsBackupCoverage must name each.</summary>
public static class AppSettingsKey
{
    public const string Appearance = "appearance";
    public const string LaunchAtLogin = "launchAtLogin";
    public const string ShowInTray = "showInTray";
    public const string PaletteTransparency = "paletteTransparency";
    public const string InterfaceSize = "interfaceSize";
    public const string SnippetsEnabled = "snippetsEnabled";
    public const string AiEnabled = "aiEnabled";
    public const string CalendarEnabled = "calendarEnabled";
    public const string AutoJoinMeetings = "autoJoinMeetings";
    public const string CameraPreview = "cameraPreview";
    public const string McpEnabled = "mcpEnabled";
    public const string ExtensionsEnabled = "extensionsEnabled";
    public const string QuickActionsEnabled = "quickActionsEnabled";
    public const string ClipboardEnabled = "clipboardEnabled";
    public const string WindowManagementEnabled = "windowManagementEnabled";
    public const string FileSearchEnabled = "fileSearchEnabled";
    public const string NotesEnabled = "notesEnabled";
    public const string QuicklinksEnabled = "quicklinksEnabled";
    public const string CustomCommandsEnabled = "customCommandsEnabled";
    public const string NavigationEnabled = "navigationEnabled";
    public const string SnippetsShowInLauncher = "snippetsShowInLauncher";
    public const string WindowLayoutsShowInLauncher = "windowLayoutsShowInLauncher";
    public const string CustomCommandsShowInLauncher = "customCommandsShowInLauncher";
    public const string FileSearchScopes = "fileSearchScopes";
    public const string FileSearchIgnorePatterns = "fileSearchIgnorePatterns";
    public const string WindowGap = "windowGap";
    public const string WindowCycle = "windowCycle";
    public const string EmojiColumns = "emojiColumns";
    public const string ClipboardOcrEnabled = "clipboardOcrEnabled";
    public const string ClipboardRetentionDays = "clipboardRetentionDays";
    public const string ClipboardKeepOpen = "clipboardKeepOpen";
    public const string ClipboardDefaultAction = "clipboardDefaultAction";
    public const string ClipboardIgnoredApps = "clipboardIgnoredApps";
    public const string EmojiSkinTone = "emojiSkinTone";
    public const string CompactPalette = "compactPalette";
    public const string PaletteRememberPosition = "paletteRememberPosition";
    public const string PalettePopToRootSeconds = "palettePopToRootSeconds";
    public const string PaletteEscapeClearsQuery = "paletteEscapeClearsQuery";
    public const string CalendarExcludedIds = "calendarExcludedIds";
    public const string NavigationExcludedApps = "navigationExcludedApps";
    public const string PaletteLeft = "paletteLeft";
    public const string PaletteTop = "paletteTop";

    public static IReadOnlyList<string> All { get; } =
    [
        Appearance, LaunchAtLogin, ShowInTray, PaletteTransparency, InterfaceSize,
        SnippetsEnabled, AiEnabled, CalendarEnabled, AutoJoinMeetings, CameraPreview,
        McpEnabled, ExtensionsEnabled, QuickActionsEnabled, ClipboardEnabled,
        WindowManagementEnabled, FileSearchEnabled, NotesEnabled, QuicklinksEnabled,
        CustomCommandsEnabled, NavigationEnabled, SnippetsShowInLauncher, WindowLayoutsShowInLauncher,
        CustomCommandsShowInLauncher, FileSearchScopes, FileSearchIgnorePatterns,
        WindowGap, WindowCycle, EmojiColumns,
        ClipboardOcrEnabled, ClipboardRetentionDays, ClipboardKeepOpen, ClipboardDefaultAction,
        ClipboardIgnoredApps, EmojiSkinTone, CompactPalette, PaletteRememberPosition,
        PalettePopToRootSeconds, PaletteEscapeClearsQuery, CalendarExcludedIds,
        NavigationExcludedApps, PaletteLeft, PaletteTop,
    ];
}
