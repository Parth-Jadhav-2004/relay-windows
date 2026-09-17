using Microsoft.UI.Dispatching;

namespace Tinycast;

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

        _core.PaletteWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            _window = new DialogWindow(_core, request, Finish);
            _core.ApplyAppearance(_window);
            _window.Activate();
        });

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
