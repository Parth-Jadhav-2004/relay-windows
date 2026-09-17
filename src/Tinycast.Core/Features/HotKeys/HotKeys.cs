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
            parts.Add(KeyName(VirtualKey));
            return string.Join("+", parts);
        }
    }

    static string KeyName(uint vk)
    {
        if (vk == 0x20)
            return "Space";
        if (vk is >= 0x30 and <= 0x39)
            return ((char)vk).ToString();
        if (vk is >= 0x41 and <= 0x5A)
            return ((char)vk).ToString();
        if (vk is >= 0x70 and <= 0x87)
            return "F" + (vk - 0x6F);
        return vk switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x1B => "Esc",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2D => "Insert",
            0x2E => "Delete",
            0x6B => "Plus",
            0x6D => "Minus",
            0xBA => "Oem1",
            0xBB => "OemPlus",
            0xBC => "OemComma",
            0xBD => "OemMinus",
            0xBE => "OemPeriod",
            0xBF => "Oem2",
            0xC0 => "Oem3",
            0xDB => "Oem4",
            0xDC => "Oem5",
            0xDD => "Oem6",
            0xDE => "Oem7",
            0xDF => "Oem8",
            0xE2 => "Oem102",
            _ => $"Vk0x{vk:X2}",
        };
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
                if (!ScopesOverlap(a.AppPath, b.AppPath))
                    continue;
                if (a.Chord is { } ca && b.Chord is { } cb && ca == cb)
                    hits.Add((a, b));
                if (a.DoubleTap is { } da && b.DoubleTap is { } db && da == db)
                    hits.Add((a, b));
            }
        }

        return hits;
    }

    static bool ScopesOverlap(string? a, string? b)
    {
        var left = a ?? "";
        var right = b ?? "";
        if (left.Length == 0 || right.Length == 0)
            return true;
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
