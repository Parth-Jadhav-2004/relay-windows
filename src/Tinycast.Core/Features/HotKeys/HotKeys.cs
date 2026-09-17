namespace Tinycast.Features.HotKeys;

public enum DoubleTapModifier
{
    Control,
    Alt,
    Shift,
    Win,
}

public sealed class DoubleTapDetector
{
    public static readonly TimeSpan MaxHold = TimeSpan.FromSeconds(0.25);
    public static readonly TimeSpan MaxGap = TimeSpan.FromSeconds(0.30);

    HashSet<DoubleTapModifier> _held = [];
    (DoubleTapModifier Modifier, DateTime StartedAt)? _press;
    (DoubleTapModifier Modifier, DateTime ReleasedAt)? _pendingTap;

    public DoubleTapModifier? Handle(IReadOnlyCollection<DoubleTapModifier> modifiers, bool hasOtherModifiers, DateTime now)
    {
        var previous = _held;
        _held = [.. modifiers];
        if (hasOtherModifiers)
        {
            Invalidate();
            return null;
        }

        if (modifiers.Count == 0)
            return CompleteTap(now);
        if (previous.Count != 0 || modifiers.Count != 1)
        {
            Invalidate();
            return null;
        }

        _press = (modifiers.First(), now);
        return null;
    }

    public void OtherInput() => Invalidate();

    public void Reset()
    {
        _held = [];
        Invalidate();
    }

    DoubleTapModifier? CompleteTap(DateTime now)
    {
        if (_press is not { } press || now - press.StartedAt > MaxHold)
        {
            Invalidate();
            return null;
        }

        _press = null;
        if (_pendingTap is { } pending && pending.Modifier == press.Modifier && press.StartedAt - pending.ReleasedAt <= MaxGap)
        {
            _pendingTap = null;
            return press.Modifier;
        }

        _pendingTap = (press.Modifier, now);
        return null;
    }

    void Invalidate()
    {
        _press = null;
        _pendingTap = null;
    }
}

public sealed record HotKeyChord(uint Modifiers, uint VirtualKey)
{
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint HyperModifiers = ModAlt | ModControl | ModShift | ModWin;

    public bool IsHyper => (Modifiers & HyperModifiers) == HyperModifiers;

    public static HotKeyChord Hyper(uint virtualKey) => new(HyperModifiers, virtualKey);

    public string Label
    {
        get
        {
            var parts = new List<string>();
            if ((Modifiers & 0x0002) != 0) parts.Add("Ctrl");
            if ((Modifiers & 0x0001) != 0) parts.Add("Alt");
            if ((Modifiers & 0x0004) != 0) parts.Add("Shift");
            if ((Modifiers & 0x0008) != 0) parts.Add("Win");
            parts.Add(VirtualKey == 0x20 ? "Space" : ((char)VirtualKey).ToString());
            return string.Join("+", parts);
        }
    }
}

public sealed class HotKeyBinding
{
    public string CommandId { get; set; } = "";
    public HotKeyChord? Chord { get; set; }
    public DoubleTapModifier? DoubleTap { get; set; }
    public string? AppPath { get; set; }

    public bool IsEmpty => Chord is null && DoubleTap is null;
}

public static class HotKeyConflicts
{
    public static IReadOnlyList<(HotKeyBinding A, HotKeyBinding B)> Find(IReadOnlyList<HotKeyBinding> bindings)
    {
        var hits = new List<(HotKeyBinding, HotKeyBinding)>();
        for (var i = 0; i < bindings.Count; i++)
        {
            for (var j = i + 1; j < bindings.Count; j++)
            {
                var a = bindings[i];
                var b = bindings[j];
                if (!string.Equals(a.AppPath ?? "", b.AppPath ?? "", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (a.Chord is { } ca && b.Chord is { } cb && ca == cb)
                    hits.Add((a, b));
                if (a.DoubleTap is { } da && b.DoubleTap is { } db && da == db)
                    hits.Add((a, b));
            }
        }

        return hits;
    }
}
