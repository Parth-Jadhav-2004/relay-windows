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
    };

    public static void ApplyMirrored(AppSettings settings, IReadOnlyDictionary<string, string> data)
    {
        foreach (var (key, value) in data)
        {
            if (!SettingsBackupCoverage.Mirrored.Contains(key))
                continue;
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
            }
        }
    }

    static string Flag(bool value) => value ? "true" : "false";
    static bool IsTrue(string value) => value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    static List<string> Lines(string value) =>
        value.Split(['\n', '\r', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
