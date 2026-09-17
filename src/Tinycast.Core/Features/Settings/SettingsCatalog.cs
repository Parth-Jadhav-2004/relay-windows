namespace Tinycast.Features.Settings;

public enum SettingsTab
{
    General,
    Permissions,
    Hotkeys,
    Applications,
    SystemSettings,
    SystemActions,
    Commands,
    Quicklinks,
    Fallbacks,
    Ai,
    QuickActions,
    FileSearch,
    Notes,
    Snippets,
    Navigation,
    WindowManagement,
    Clipboard,
    Emoji,
    Calendar,
    Extensions,
    Backup,
    About,
}

public sealed record SettingsPane(SettingsTab Tab, string Section, string Title, IReadOnlyList<string> Keywords);

public static class SettingsCatalog
{
    public static IReadOnlyList<SettingsPane> Panes { get; } =
    [
        P(SettingsTab.General, "General", "General", "appearance", "theme", "tray", "login", "transparency", "scrim"),
        P(SettingsTab.Permissions, "General", "Permissions", "calendar", "camera", "privacy"),
        P(SettingsTab.Hotkeys, "General", "Hotkeys", "shortcut", "chord", "double tap", "hyper", "caps lock", "alt space"),
        P(SettingsTab.Applications, "Launcher", "Applications", "apps", "alias", "hide", "search"),
        P(SettingsTab.SystemSettings, "Launcher", "System Settings", "ms-settings", "windows settings", "display", "network"),
        P(SettingsTab.SystemActions, "Launcher", "System Actions", "lock", "sleep", "volume", "shutdown"),
        P(SettingsTab.Commands, "Launcher", "Commands", "custom", "shell", "executable"),
        P(SettingsTab.Quicklinks, "Launcher", "Quicklinks", "url", "bookmark", "search"),
        P(SettingsTab.Fallbacks, "Launcher", "Fallbacks", "use with", "shell", "search files", "ai"),
        P(SettingsTab.Ai, "Features", "AI", "chat", "opencode", "model", "key", "mcp"),
        P(SettingsTab.QuickActions, "Features", "Quick Actions", "selection", "grammar", "translate"),
        P(SettingsTab.FileSearch, "Features", "File Search", "folders", "ignore", "drive", "volumes"),
        P(SettingsTab.Notes, "Features", "Notes", "markdown", "floating"),
        P(SettingsTab.Snippets, "Features", "Snippets", "keyword", "expand", "template", "argument"),
        P(SettingsTab.Navigation, "Features", "Navigation", "windows", "menu search", "switcher"),
        P(SettingsTab.WindowManagement, "Features", "Window Management", "tile", "gap", "layout", "cycle"),
        P(SettingsTab.Clipboard, "Features", "Clipboard", "history", "ocr", "pin", "filter"),
        P(SettingsTab.Emoji, "Features", "Emoji & Symbols", "grid", "columns", "icons"),
        P(SettingsTab.Calendar, "Features", "Calendar", "meetings", "auto join", "join"),
        P(SettingsTab.Extensions, "Features", "Extensions", "raycast"),
        P(SettingsTab.Backup, "Advanced", "Backup", "export", "import", "archive"),
        P(SettingsTab.About, "Advanced", "About", "version", "updates", "support", "welcome"),
    ];

    public static IReadOnlyList<string> Sections { get; } = ["General", "Launcher", "Features", "Advanced"];

    public static SettingsPane Pane(SettingsTab tab) => Panes.First(p => p.Tab == tab);

    public static IReadOnlyList<SettingsPane> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Panes;
        return Panes.Where(p => Matches(p, query.Trim())).ToList();
    }

    public static bool Matches(SettingsPane pane, string query) =>
        pane.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
        || pane.Section.Contains(query, StringComparison.OrdinalIgnoreCase)
        || pane.Keywords.Any(k => k.Contains(query, StringComparison.OrdinalIgnoreCase));

    static SettingsPane P(SettingsTab tab, string section, string title, params string[] keywords) =>
        new(tab, section, title, keywords);
}
