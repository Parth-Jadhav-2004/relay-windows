using Microsoft.Win32;

namespace Tinycast.Platform;

internal static class LaunchAtLogin
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "Tinycast";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (key is null)
            return;

        if (enabled)
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(path))
                return;
            key.SetValue(ValueName, $"\"{path}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
