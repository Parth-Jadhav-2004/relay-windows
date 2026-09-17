using System.Globalization;

namespace Tinycast.Features.Settings;

public static class SettingsSnapshot
{
    public static Dictionary<string, string> Capture(AppSettings settings) => new()
    {
        [AppSettingsKey.Appearance] = settings.Appearance.ToString(),
        [AppSettingsKey.LaunchAtLogin] = settings.LaunchAtLogin ? "true" : "false",
        [AppSettingsKey.ShowInTray] = settings.ShowInTray ? "true" : "false",
        [AppSettingsKey.PaletteTransparency] = settings.PaletteTransparency.ToString(CultureInfo.InvariantCulture),
        [AppSettingsKey.InterfaceSize] = settings.InterfaceSize,
        [AppSettingsKey.SnippetsEnabled] = Flag(settings.SnippetsEnabled),
        [AppSettingsKey.AiEnabled] = Flag(settings.AiEnabled),
        [AppSettingsKey.CalendarEnabled] = Flag(settings.CalendarEnabled),
        [AppSettingsKey.AutoJoinMeetings] = Flag(settings.AutoJoinMeetings),
        [AppSettingsKey.CameraPreview] = Flag(settings.CameraPreview),
        [AppSettingsKey.McpEnabled] = Flag(settings.McpEnabled),
        [AppSettingsKey.ExtensionsEnabled] = Flag(settings.ExtensionsEnabled),
        [AppSettingsKey.QuickActionsEnabled] = Flag(settings.QuickActionsEnabled),
        [AppSettingsKey.ClipboardEnabled] = Flag(settings.ClipboardEnabled),
        [AppSettingsKey.WindowManagementEnabled] = Flag(settings.WindowManagementEnabled),
        [AppSettingsKey.FileSearchEnabled] = Flag(settings.FileSearchEnabled),
        [AppSettingsKey.NotesEnabled] = Flag(settings.NotesEnabled),
        [AppSettingsKey.QuicklinksEnabled] = Flag(settings.QuicklinksEnabled),
        [AppSettingsKey.CustomCommandsEnabled] = Flag(settings.CustomCommandsEnabled),
        [AppSettingsKey.NavigationEnabled] = Flag(settings.NavigationEnabled),
        [AppSettingsKey.FileSearchScopes] = string.Join("\n", settings.FileSearchScopes),
        [AppSettingsKey.FileSearchIgnorePatterns] = string.Join("\n", settings.FileSearchIgnorePatterns),
        [AppSettingsKey.WindowGap] = settings.WindowGap.ToString(CultureInfo.InvariantCulture),
        [AppSettingsKey.WindowCycle] = settings.WindowCycle,
        [AppSettingsKey.EmojiColumns] = settings.EmojiColumns.ToString(CultureInfo.InvariantCulture),
        [AppSettingsKey.ClipboardOcrEnabled] = Flag(settings.ClipboardOcrEnabled),
        [AppSettingsKey.ClipboardRetentionDays] = settings.ClipboardRetentionDays.ToString(CultureInfo.InvariantCulture),
        [AppSettingsKey.ClipboardKeepOpen] = Flag(settings.ClipboardKeepOpen),
        [AppSettingsKey.ClipboardDefaultAction] = settings.ClipboardDefaultAction,
        [AppSettingsKey.ClipboardIgnoredApps] = string.Join("\n", settings.ClipboardIgnoredApps),
        [AppSettingsKey.EmojiSkinTone] = settings.EmojiSkinTone.ToString(CultureInfo.InvariantCulture),
        [AppSettingsKey.CompactPalette] = Flag(settings.CompactPalette),
        [AppSettingsKey.PaletteRememberPosition] = Flag(settings.PaletteRememberPosition),
        [AppSettingsKey.PalettePopToRootSeconds] = settings.PalettePopToRootSeconds.ToString(CultureInfo.InvariantCulture),
        [AppSettingsKey.PaletteEscapeClearsQuery] = Flag(settings.PaletteEscapeClearsQuery),
        [AppSettingsKey.CalendarExcludedIds] = string.Join("\n", settings.CalendarExcludedIds),
        [AppSettingsKey.NavigationExcludedApps] = string.Join("\n", settings.NavigationExcludedApps),
        [AppSettingsKey.PaletteLeft] = settings.PaletteLeft.ToString(CultureInfo.InvariantCulture),
        [AppSettingsKey.PaletteTop] = settings.PaletteTop.ToString(CultureInfo.InvariantCulture),
    };

    public static void ApplyMirrored(AppSettings settings, IReadOnlyDictionary<string, string> data)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(data);
        var mirrored = data.Where(kv => SettingsBackupCoverage.Mirrored.Contains(kv.Key)).ToArray();
        foreach (var (key, value) in mirrored)
        {
            if (!IsValid(key, value))
                throw new InvalidDataException($"Invalid backup setting: {key}.");
        }
        foreach (var (key, value) in mirrored)
        {
            switch (key)
            {
                case AppSettingsKey.Appearance:
                    settings.Appearance = Enum.TryParse<AppAppearance>(value, true, out var appearance)
                        ? appearance
                        : settings.Appearance;
                    break;
                case AppSettingsKey.ShowInTray:
                    settings.ShowInTray = IsTrue(value);
                    break;
                case AppSettingsKey.PaletteTransparency:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var transparency))
                        settings.PaletteTransparency = transparency;
                    break;
                case AppSettingsKey.InterfaceSize:
                    settings.InterfaceSize = value;
                    break;
                case AppSettingsKey.ClipboardEnabled:
                    settings.ClipboardEnabled = IsTrue(value);
                    break;
                case AppSettingsKey.WindowManagementEnabled:
                    settings.WindowManagementEnabled = IsTrue(value);
                    break;
                case AppSettingsKey.FileSearchEnabled:
                    settings.FileSearchEnabled = IsTrue(value);
                    break;
                case AppSettingsKey.NotesEnabled:
                    settings.NotesEnabled = IsTrue(value);
                    break;
                case AppSettingsKey.QuicklinksEnabled:
                    settings.QuicklinksEnabled = IsTrue(value);
                    break;
                case AppSettingsKey.CustomCommandsEnabled:
                    settings.CustomCommandsEnabled = IsTrue(value);
                    break;
                case AppSettingsKey.NavigationEnabled:
                    settings.NavigationEnabled = IsTrue(value);
                    break;
                case AppSettingsKey.FileSearchScopes:
                    settings.FileSearchScopes = Lines(value);
                    break;
                case AppSettingsKey.FileSearchIgnorePatterns:
                    settings.FileSearchIgnorePatterns = Lines(value);
                    break;
                case AppSettingsKey.WindowGap:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gap))
                        settings.WindowGap = Math.Clamp(gap, 0, 64);
                    break;
                case AppSettingsKey.WindowCycle:
                    settings.WindowCycle = value;
                    break;
                case AppSettingsKey.EmojiColumns:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cols))
                        settings.EmojiColumns = Math.Clamp(cols, 6, 10);
                    break;
                case AppSettingsKey.ClipboardOcrEnabled:
                    settings.ClipboardOcrEnabled = IsTrue(value);
                    break;
                case AppSettingsKey.ClipboardRetentionDays:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days))
                        settings.ClipboardRetentionDays = Math.Clamp(days, 0, 3650);
                    break;
                case AppSettingsKey.ClipboardKeepOpen:
                    settings.ClipboardKeepOpen = IsTrue(value);
                    break;
                case AppSettingsKey.ClipboardDefaultAction:
                    settings.ClipboardDefaultAction = value;
                    break;
                case AppSettingsKey.ClipboardIgnoredApps:
                    settings.ClipboardIgnoredApps = Lines(value);
                    break;
                case AppSettingsKey.EmojiSkinTone:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tone))
                        settings.EmojiSkinTone = Math.Clamp(tone, 0, 5);
                    break;
                case AppSettingsKey.CompactPalette:
                    settings.CompactPalette = IsTrue(value);
                    break;
                case AppSettingsKey.PaletteRememberPosition:
                    settings.PaletteRememberPosition = IsTrue(value);
                    break;
                case AppSettingsKey.PalettePopToRootSeconds:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pop))
                        settings.PalettePopToRootSeconds = Math.Clamp(pop, 0, 120);
                    break;
                case AppSettingsKey.PaletteEscapeClearsQuery:
                    settings.PaletteEscapeClearsQuery = IsTrue(value);
                    break;
                case AppSettingsKey.CalendarExcludedIds:
                    settings.CalendarExcludedIds = Lines(value);
                    break;
                case AppSettingsKey.NavigationExcludedApps:
                    settings.NavigationExcludedApps = Lines(value);
                    break;
                case AppSettingsKey.PaletteLeft:
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var left))
                        settings.PaletteLeft = left;
                    break;
                case AppSettingsKey.PaletteTop:
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var top))
                        settings.PaletteTop = top;
                    break;
            }
        }
    }

    static bool IsValid(string key, string? value)
    {
        if (value is null)
            return false;
        return key switch
        {
            AppSettingsKey.Appearance => Enum.TryParse<AppAppearance>(value, true, out var appearance) && Enum.IsDefined(appearance),
            AppSettingsKey.PaletteTransparency or AppSettingsKey.WindowGap or AppSettingsKey.EmojiColumns
                or AppSettingsKey.ClipboardRetentionDays or AppSettingsKey.EmojiSkinTone
                or AppSettingsKey.PalettePopToRootSeconds => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            AppSettingsKey.PaletteLeft or AppSettingsKey.PaletteTop =>
                double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate) && double.IsFinite(coordinate),
            AppSettingsKey.InterfaceSize => value is "compact" or "standard" or "large",
            AppSettingsKey.WindowCycle => value is "Off" or "Sizes" or "Displays",
            AppSettingsKey.ClipboardDefaultAction => value is "paste" or "copy",
            AppSettingsKey.FileSearchScopes or AppSettingsKey.FileSearchIgnorePatterns
                or AppSettingsKey.ClipboardIgnoredApps or AppSettingsKey.CalendarExcludedIds
                or AppSettingsKey.NavigationExcludedApps => true,
            AppSettingsKey.ShowInTray or AppSettingsKey.ClipboardEnabled or AppSettingsKey.WindowManagementEnabled
                or AppSettingsKey.FileSearchEnabled or AppSettingsKey.NotesEnabled or AppSettingsKey.QuicklinksEnabled
                or AppSettingsKey.CustomCommandsEnabled or AppSettingsKey.NavigationEnabled or AppSettingsKey.ClipboardOcrEnabled
                or AppSettingsKey.ClipboardKeepOpen or AppSettingsKey.CompactPalette or AppSettingsKey.PaletteRememberPosition
                or AppSettingsKey.PaletteEscapeClearsQuery => bool.TryParse(value, out _) || value is "0" or "1",
            _ => false,
        };
    }

    static string Flag(bool value) => value ? "true" : "false";
    static bool IsTrue(string value) => (bool.TryParse(value, out var flag) && flag) || value == "1";
    static List<string> Lines(string value) =>
        value.Split(['\n', '\r', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
