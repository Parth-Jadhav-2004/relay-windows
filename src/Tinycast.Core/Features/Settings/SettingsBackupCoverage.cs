namespace Tinycast.Features.Settings;

/// <summary>
/// Every AppSettingsKey must appear in exactly one table.
/// Capability flags are excluded so an import cannot grant listening, AI, or calendar access.
/// </summary>
public static class SettingsBackupCoverage
{
    public static IReadOnlySet<string> Mirrored { get; } = new HashSet<string>
    {
        AppSettingsKey.Appearance,
        AppSettingsKey.ShowInTray,
        AppSettingsKey.PaletteTransparency,
        AppSettingsKey.InterfaceSize,
        AppSettingsKey.ClipboardEnabled,
        AppSettingsKey.WindowManagementEnabled,
        AppSettingsKey.FileSearchEnabled,
        AppSettingsKey.NotesEnabled,
        AppSettingsKey.QuicklinksEnabled,
        AppSettingsKey.CustomCommandsEnabled,
        AppSettingsKey.NavigationEnabled,
        AppSettingsKey.SnippetsShowInLauncher,
        AppSettingsKey.WindowLayoutsShowInLauncher,
        AppSettingsKey.CustomCommandsShowInLauncher,
        AppSettingsKey.FileSearchScopes,
        AppSettingsKey.FileSearchIgnorePatterns,
        AppSettingsKey.WindowGap,
        AppSettingsKey.WindowCycle,
        AppSettingsKey.EmojiColumns,
        AppSettingsKey.ClipboardOcrEnabled,
        AppSettingsKey.ClipboardRetentionDays,
        AppSettingsKey.ClipboardKeepOpen,
        AppSettingsKey.ClipboardDefaultAction,
        AppSettingsKey.ClipboardIgnoredApps,
        AppSettingsKey.EmojiSkinTone,
        AppSettingsKey.CompactPalette,
        AppSettingsKey.PaletteRememberPosition,
        AppSettingsKey.PalettePopToRootSeconds,
        AppSettingsKey.PaletteEscapeClearsQuery,
        AppSettingsKey.CalendarExcludedIds,
        AppSettingsKey.NavigationExcludedApps,
        AppSettingsKey.PaletteLeft,
        AppSettingsKey.PaletteTop,
    };

    public static IReadOnlySet<string> ExternallySourced { get; } = new HashSet<string>
    {
        AppSettingsKey.LaunchAtLogin,
    };

    public static IReadOnlyDictionary<string, string> DeliberatelyExcluded { get; } =
        new Dictionary<string, string>
        {
            [AppSettingsKey.SnippetsEnabled] = "Consent to keystroke listening.",
            [AppSettingsKey.AiEnabled] = "Arms networked chat.",
            [AppSettingsKey.CalendarEnabled] = "Calendar access consent.",
            [AppSettingsKey.AutoJoinMeetings] = "Opens meeting links unattended.",
            [AppSettingsKey.CameraPreview] = "Turns the camera on.",
            [AppSettingsKey.McpEnabled] = "Runs local MCP processes.",
            [AppSettingsKey.ExtensionsEnabled] = "Runs third-party extension code.",
            [AppSettingsKey.QuickActionsEnabled] = "Reads selected text in other apps.",
        };

    public static IEnumerable<string> UncoveredKeys()
    {
        foreach (var key in AppSettingsKey.All)
        {
            var n = 0;
            if (Mirrored.Contains(key)) n++;
            if (ExternallySourced.Contains(key)) n++;
            if (DeliberatelyExcluded.ContainsKey(key)) n++;
            if (n != 1)
                yield return key;
        }
    }
}
