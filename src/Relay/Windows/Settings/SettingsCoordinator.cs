using Microsoft.UI.Xaml;
using Relay.Features.Settings;

namespace Relay;

public sealed class SettingsCoordinator
{
    readonly AppCore _core;
    SettingsWindow? _window;

    public SettingsCoordinator(AppCore core) => _core = core;

    public Window? Window => _window;

    public void Show(SettingsTab? tab = null)
    {
        if (_window is null)
        {
            _window = new SettingsWindow(_core);
            _window.Closed += (_, _) => _window = null;
        }

        if (tab is { } selected)
            _window.SelectTab(selected);
        _window.Activate();
        _core.ApplyAppearance(_window);
    }

    public void Reload()
    {
        _window?.Reload();
        if (_window is not null)
            _core.ApplyAppearance(_window);
    }

    public void RefreshAbout() => _window?.RefreshAboutUpdates();
}
