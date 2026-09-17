using Microsoft.UI.Dispatching;
using Tinycast.DesignSystem;

namespace Tinycast;

public sealed class MessageHudPresenter
{
    readonly AppCore _core;
    MessageHudWindow? _window;
    DispatcherQueueTimer? _timer;

    public MessageHudPresenter(AppCore core) => _core = core;

    public void Show(string message, DialogTone tone = DialogTone.Neutral)
    {
        var queue = _core.PaletteWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        queue.TryEnqueue(() =>
        {
            _timer?.Stop();
            if (_window is null)
            {
                _window = new MessageHudWindow();
                _window.Closed += (_, _) => _window = null;
            }

            _window.Present(message, tone, _core.Settings.Appearance);
            _core.ApplyAppearance(_window);
            _window.AppWindow.Show(false);

            _timer = queue.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(Theme.Duration.MessageHudSeconds);
            _timer.IsRepeating = false;
            _timer.Tick += (_, _) => _window?.AppWindow.Hide();
            _timer.Start();
        });
    }
}
