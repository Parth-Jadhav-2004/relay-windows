namespace Tinycast.Features.Launcher;

public static class MsSettingsCatalog
{
    public static IReadOnlyList<(string Title, string Uri, string Glyph)> All { get; } =
    [
        ("Settings", "ms-settings:", "\uE713"),
        ("Display", "ms-settings:display", "\uE7F4"),
        ("Sound", "ms-settings:sound", "\uE767"),
        ("Notifications", "ms-settings:notifications", "\uEA8F"),
        ("Focus", "ms-settings:quiethours", "\uE7C4"),
        ("Power & battery", "ms-settings:powersleep", "\uE7E8"),
        ("Storage", "ms-settings:storagesense", "\uEDA2"),
        ("Bluetooth & devices", "ms-settings:bluetooth", "\uE702"),
        ("Printers & scanners", "ms-settings:printers", "\uE749"),
        ("Network & internet", "ms-settings:network", "\uE839"),
        ("Wi-Fi", "ms-settings:network-wifi", "\uE701"),
        ("VPN", "ms-settings:network-vpn", "\uE792"),
        ("Personalization", "ms-settings:personalization", "\uE771"),
        ("Colors", "ms-settings:colors", "\uE790"),
        ("Themes", "ms-settings:themes", "\uE771"),
        ("Taskbar", "ms-settings:taskbar", "\uE7C4"),
        ("Apps", "ms-settings:appsfeatures", "\uE71D"),
        ("Installed apps", "ms-settings:appsfeatures", "\uE71D"),
        ("Default apps", "ms-settings:defaultapps", "\uE774"),
        ("Startup apps", "ms-settings:startupapps", "\uE7E8"),
        ("Accounts", "ms-settings:yourinfo", "\uE77B"),
        ("Sign-in options", "ms-settings:signinoptions", "\uE1F6"),
        ("Windows Update", "ms-settings:windowsupdate", "\uE895"),
        ("Privacy & security", "ms-settings:privacy", "\uE72E"),
        ("Clipboard settings", "ms-settings:clipboard", "\uE16D"),
        ("Date & time", "ms-settings:dateandtime", "\uE787"),
        ("Language", "ms-settings:regionlanguage", "\uF2B7"),
        ("Accessibility", "ms-settings:easeofaccess", "\uE776"),
        ("For developers", "ms-settings:developers", "\uE943"),
        ("About", "ms-settings:about", "\uE946"),
    ];
}
