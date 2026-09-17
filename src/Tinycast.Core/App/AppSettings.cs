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
    public bool WindowManagementEnabled { get; set; } = true;
    public bool FileSearchEnabled { get; set; } = true;
    public bool NotesEnabled { get; set; } = true;
    public bool QuicklinksEnabled { get; set; } = true;
    public bool CustomCommandsEnabled { get; set; } = true;
    public bool NavigationEnabled { get; set; } = true;
    public List<string> FileSearchScopes { get; set; } = [];
    public List<string> FileSearchIgnorePatterns { get; set; } = [];
}
