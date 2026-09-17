namespace Relay.Palette;

public sealed class PaletteState
{
    readonly List<PaletteFrame> _stack = [];

    public PaletteMode Mode { get; private set; } = PaletteMode.Launcher;
    public string Query { get; set; } = "";
    public int Selection { get; set; }
    public bool ForceExpanded { get; set; }
    public Guid FocusToken { get; private set; } = Guid.NewGuid();
    public IReadOnlyList<PaletteFrame> Stack => _stack;
    public event Action? Changed;

    public void Prepare(PaletteMode mode)
    {
        _stack.Clear();
        Mode = mode;
        Query = "";
        Selection = 0;
        ForceExpanded = false;
        FocusToken = Guid.NewGuid();
        Notify();
    }

    public void Replace(PaletteMode mode)
    {
        Mode = mode;
        Query = "";
        Selection = 0;
        FocusToken = Guid.NewGuid();
        Notify();
    }

    public void Push(PaletteMode mode)
    {
        _stack.Add(new PaletteFrame(Mode, Query, Selection));
        Mode = mode;
        Query = "";
        Selection = 0;
        FocusToken = Guid.NewGuid();
        Notify();
    }

    public void PushCarryingQuery(PaletteMode mode)
    {
        _stack.Add(new PaletteFrame(Mode, Query, Selection));
        Mode = mode;
        Selection = 0;
        FocusToken = Guid.NewGuid();
        Notify();
    }

    public bool Pop()
    {
        if (_stack.Count == 0)
            return false;

        var frame = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        Mode = frame.Mode;
        Query = frame.Query;
        Selection = frame.Selection;
        Notify();
        return true;
    }

    public void Notify() => Changed?.Invoke();
}
