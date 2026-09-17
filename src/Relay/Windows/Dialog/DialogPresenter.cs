using Microsoft.UI.Dispatching;
using Relay.Platform;

namespace Relay;

public sealed class DialogPresenter
{
    readonly AppCore _core;
    DialogWindow? _window;
    TaskCompletionSource<int>? _pending;

    public DialogPresenter(AppCore core) => _core = core;

    public bool IsPresenting => _pending is not null;

    public Task<int> Confirm(DialogRequest request)
    {
        if (_pending is not null)
            return Task.FromResult(request.CancelIndex);

        var pending = new TaskCompletionSource<int>();
        _pending = pending;

        var queue = _core.PaletteWindow?.DispatcherQueue;
        if (queue is null || !queue.TryEnqueue(() =>
        {
            try
            {
                _window = new DialogWindow(_core, request, Finish);
                _core.ApplyAppearance(_window);
                _window.Activate();
            }
            catch (Exception ex)
            {
                Log.Write("dialog: " + ex.Message);
                Finish(request.CancelIndex);
            }
        }))
        {
            _pending = null;
            return Task.FromResult(request.CancelIndex);
        }

        return pending.Task;
    }

    void Finish(int index)
    {
        var pending = _pending;
        _pending = null;
        var window = _window;
        _window = null;
        window?.Close();
        pending?.TrySetResult(index);
    }
}
