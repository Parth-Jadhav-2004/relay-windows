namespace Tinycast;

public sealed class AppSettings
{
    public AppAppearance Appearance { get; set; } = AppAppearance.Dark;
    public bool LaunchAtLogin { get; set; }
    public bool ShowInTray { get; set; } = true;
    public int PaletteTransparency { get; set; }
    public string InterfaceSize { get; set; } = "standard";
    public bool SnippetsEnabled { get; set; }
    public bool AiEnabled { get; set; }
    public bool CalendarEnabled { get; set; }
    public bool AutoJoinMeetings { get; set; }
    public bool CameraPreview { get; set; }
    public bool McpEnabled { get; set; }
    public bool ExtensionsEnabled { get; set; }
    public bool QuickActionsEnabled { get; set; }
    public bool ClipboardEnabled { get; set; } = true;
    public bool WindowManagementEnabled { get; set; }
    public bool FileSearchEnabled { get; set; }
    public bool NotesEnabled { get; set; }
    public bool QuicklinksEnabled { get; set; }
    public bool CustomCommandsEnabled { get; set; }
    public bool NavigationEnabled { get; set; }
    public bool SnippetsShowInLauncher { get; set; } = true;
    public bool WindowLayoutsShowInLauncher { get; set; } = true;
    public bool CustomCommandsShowInLauncher { get; set; } = true;
    public List<string> FileSearchScopes { get; set; } = [];
    public List<string> FileSearchIgnorePatterns { get; set; } = [];
    public int WindowGap { get; set; } = 8;
    public string WindowCycle { get; set; } = "Sizes";
    public int EmojiColumns { get; set; } = 8;
    public bool ClipboardOcrEnabled { get; set; }
    public int ClipboardRetentionDays { get; set; } = 90;
    public bool ClipboardKeepOpen { get; set; }
    public string ClipboardDefaultAction { get; set; } = "paste";
    public List<string> ClipboardIgnoredApps { get; set; } = [];
    public int EmojiSkinTone { get; set; }
    public bool CompactPalette { get; set; }
    public bool PaletteRememberPosition { get; set; }
    public int PalettePopToRootSeconds { get; set; }
    public bool PaletteEscapeClearsQuery { get; set; } = true;
    public List<string> CalendarExcludedIds { get; set; } = [];
    public List<string> NavigationExcludedApps { get; set; } = [];
    public double PaletteLeft { get; set; } = -1;
    public double PaletteTop { get; set; } = -1;
}

