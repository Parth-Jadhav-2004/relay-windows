using Windows.Devices.Radios;

namespace Tinycast.Platform;

internal static class RadioToggle
{
    public static async Task<string> ToggleBluetoothAsync()
    {
        var radios = await Radio.GetRadiosAsync();
        var bt = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
        if (bt is null)
        {
            ProcessLauncher.OpenUri("ms-settings:bluetooth");
            return "Bluetooth radio not found. Opened Settings.";
        }

        if (bt.State == RadioState.On)
        {
            var result = await bt.SetStateAsync(RadioState.Off);
            return result == RadioAccessStatus.Allowed ? "Bluetooth off" : "Could not turn Bluetooth off.";
        }

        var on = await bt.SetStateAsync(RadioState.On);
        return on == RadioAccessStatus.Allowed ? "Bluetooth on" : "Could not turn Bluetooth on.";
    }
}
