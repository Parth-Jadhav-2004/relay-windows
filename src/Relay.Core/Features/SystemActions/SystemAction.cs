namespace Relay.Features.SystemActions;

public enum SystemActionConfirmationKind
{
    None,
    Required,
    Computed,
}

public sealed record SystemAction(
    string Id,
    string Name,
    string Glyph,
    SystemActionConfirmationKind Confirmation,
    string? ConfirmTitle = null,
    string? ConfirmMessage = null)
{
    public string EntryId => "system-action:" + Id;
}

public static class SystemActionCatalog
{
    public const string SessionEnding = "Applications with unsaved changes may ask you to save.";

    public static IReadOnlyList<SystemAction> All { get; } =
    [
        A("lock-screen", "Lock Screen", "\uE1F6"),
        A("sleep", "Sleep", "\uE708"),
        A("sleep-displays", "Sleep Displays", "\uE7F4"),
        A("restart", "Restart", "\uE149", SystemActionConfirmationKind.Required, "Restart this PC?", SessionEnding),
        A("shut-down", "Shut Down", "\uE7E8", SystemActionConfirmationKind.Required, "Shut down this PC?", SessionEnding),
        A("log-out", "Log Out", "\uE77B", SystemActionConfirmationKind.Required, "Log out now?", SessionEnding),
        A("show-screen-saver", "Show Screen Saver", "\uE7F7"),
        A("play-pause", "Play / Pause", "\uE769"),
        A("next-track", "Next Track", "\uE893"),
        A("previous-track", "Previous Track", "\uE892"),
        A("toggle-mute", "Toggle Mute", "\uE74F"),
        A("volume-up", "Turn Volume Up", "\uE767"),
        A("volume-down", "Turn Volume Down", "\uE993"),
        A("set-volume", "Set Volume…", "\uE767"),
        A("volume-0", "Set Volume to 0%", "\uE74F"),
        A("volume-25", "Set Volume to 25%", "\uE993"),
        A("volume-50", "Set Volume to 50%", "\uE767"),
        A("volume-75", "Set Volume to 75%", "\uE767"),
        A("volume-100", "Set Volume to 100%", "\uE995"),
        A("show-desktop", "Show Desktop", "\uE7C4"),
        A("toggle-system-appearance", "Toggle System Appearance", "\uE771"),
        A("open-trash", "Open Recycle Bin", "\uE74D"),
        A("empty-trash", "Empty Recycle Bin", "\uE74D", SystemActionConfirmationKind.Required, "Empty Recycle Bin?", "Items will be permanently deleted."),
        A("eject-all-disks", "Eject All Removable Drives", "\uE7DA"),
        A("toggle-hidden-files", "Toggle Hidden Files", "\uE7B3"),
        A("hide-all-apps-except-frontmost", "Hide All Apps Except Frontmost", "\uE7B3"),
        A("unhide-all-hidden-apps", "Unhide All Hidden Apps", "\uE7B3"),
        A("quit-all-apps", "Quit All Applications", "\uE711", SystemActionConfirmationKind.Computed),
        A("dismiss-notifications", "Dismiss Notifications", "\uEA8F"),
        A("toggle-bluetooth", "Toggle Bluetooth", "\uE702"),
    ];

    static readonly Dictionary<string, SystemAction> ById = All.ToDictionary(a => a.Id, StringComparer.Ordinal);
    static readonly Dictionary<string, SystemAction> ByEntry = All.ToDictionary(a => a.EntryId, StringComparer.Ordinal);

    public static SystemAction? Find(string id) =>
        ById.TryGetValue(id, out var a) ? a : ByEntry.GetValueOrDefault(id);

    static SystemAction A(
        string id,
        string name,
        string glyph,
        SystemActionConfirmationKind confirm = SystemActionConfirmationKind.None,
        string? title = null,
        string? message = null) =>
        new(id, name, glyph, confirm, title, message);
}
