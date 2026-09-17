namespace Relay.Platform;

public static class AppPaths
{
    public static string ChannelId { get; } =
#if DEBUG
        "com.relay.windows.dev";
#else
        "com.relay.windows";
#endif

    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Relay",
        ChannelId);

    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string ClipboardDir => Path.Combine(Root, "clipboard");
    public static string RankingFile => Path.Combine(Root, "ranking.json");
    public static string FavoritesFile => Path.Combine(Root, "favorites.json");
    public static string AliasesFile => Path.Combine(Root, "aliases.json");
    public static string VisibilityFile => Path.Combine(Root, "visibility.json");
    public static string SnippetsFile => Path.Combine(Root, "snippets.json");
    public static string SnippetsDir => Path.Combine(Root, "snippets");
    public static string QuicklinksFile => Path.Combine(Root, "quicklinks.json");
    public static string CustomCommandsFile => Path.Combine(Root, "custom-commands.json");
    public static string CalcHistoryFile => Path.Combine(Root, "calc-history.json");
    public static string HotKeysFile => Path.Combine(Root, "hotkeys.json");
    public static string NotesDir => Path.Combine(Root, "notes");
    public static string LayoutsFile => Path.Combine(Root, "layouts.json");
    public static string McpFile => Path.Combine(Root, "mcp.json");
    public static string ChatFile => Path.Combine(Root, "chat.json");
    public static string SupportFile => Path.Combine(Root, "support.json");
    public static string AiConfigFile => Path.Combine(Root, "ai-config.json");
    public static string OpenCodeFile => Path.Combine(Root, "ai-opencode.json");
    public static string QuickActionsFile => Path.Combine(Root, "quick-actions.json");
    public static string IconCacheDir => Path.Combine(Root, "icon-cache");
    public static string UpdatesDir => Path.Combine(Root, "updates");
    public static string EnvFile => Path.Combine(Root, ".env");
    public static string FallbacksFile => Path.Combine(Root, "fallbacks.json");
    public static string OnboardingFile => Path.Combine(Root, "onboarding.json");
    public static string EmojiPinsFile => Path.Combine(Root, "emoji-pinned.json");
    public static string EmojiFrequentFile => Path.Combine(Root, "emoji-frequent.json");

    public static void EnsureRoot()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ClipboardDir);
        Directory.CreateDirectory(NotesDir);
        Directory.CreateDirectory(SnippetsDir);
        Directory.CreateDirectory(IconCacheDir);
        Directory.CreateDirectory(UpdatesDir);
    }
}
