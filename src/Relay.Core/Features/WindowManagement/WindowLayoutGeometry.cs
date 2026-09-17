namespace Relay.Features.WindowManagement;

public enum WindowLayoutAnchor
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Center,
    Right,
    BottomLeft,
    Bottom,
    BottomRight,
}

public sealed record WindowLayoutDisplay(string Id, RectD VisibleFrame);

public sealed record WindowLayoutEntry(
    string Id,
    string ProcessName,
    string? Path,
    string DisplayId,
    double WidthFraction,
    double HeightFraction,
    WindowLayoutAnchor Anchor,
    double OffsetX,
    double OffsetY,
    string? Argument = null)
{
    public PlacementAnchor Placement => WindowLayoutGeometry.PlacementOf(Anchor);
}

public sealed record WindowLayout(
    string Id,
    string Name,
    IReadOnlyList<WindowLayoutSlot> Slots,
    string? FrontmostEntryId = null,
    bool UsesPreferredGap = false)
{
    public IReadOnlyList<WindowLayoutEntry> Entries =>
        Slots.Select(WindowLayoutGeometry.EntryFromSlot).ToList();

    public WindowLayout WithFrontmost(string? entryId) =>
        this with { FrontmostEntryId = entryId is not null && Slots.Any(s => WindowLayoutGeometry.SlotId(s) == entryId) ? entryId : null };
}

public sealed record WindowLayoutSlot(
    string ProcessName,
    RectD Frame,
    int ScreenId,
    string? Path = null,
    string DisplayId = "",
    string EntryId = "",
    double WidthFraction = 0,
    double HeightFraction = 0,
    string Anchor = "center",
    double OffsetX = 0,
    double OffsetY = 0,
    string? Argument = null);

public static class WindowLayoutGeometry
{
    public static PlacementAnchor PlacementOf(WindowLayoutAnchor anchor) => anchor switch
    {
        WindowLayoutAnchor.TopLeft => new PlacementAnchor(AxisAnchor.Min, AxisAnchor.Min),
        WindowLayoutAnchor.Top => new PlacementAnchor(AxisAnchor.Center, AxisAnchor.Min),
        WindowLayoutAnchor.TopRight => new PlacementAnchor(AxisAnchor.Max, AxisAnchor.Min),
        WindowLayoutAnchor.Left => new PlacementAnchor(AxisAnchor.Min, AxisAnchor.Center),
        WindowLayoutAnchor.Right => new PlacementAnchor(AxisAnchor.Max, AxisAnchor.Center),
        WindowLayoutAnchor.BottomLeft => new PlacementAnchor(AxisAnchor.Min, AxisAnchor.Max),
        WindowLayoutAnchor.Bottom => new PlacementAnchor(AxisAnchor.Center, AxisAnchor.Max),
        WindowLayoutAnchor.BottomRight => new PlacementAnchor(AxisAnchor.Max, AxisAnchor.Max),
        _ => PlacementAnchor.Centered,
    };

    public static WindowLayoutAnchor ParseAnchor(string? value) => value switch
    {
        "top-left" or "topleft" => WindowLayoutAnchor.TopLeft,
        "top" => WindowLayoutAnchor.Top,
        "top-right" or "topright" => WindowLayoutAnchor.TopRight,
        "left" => WindowLayoutAnchor.Left,
        "right" => WindowLayoutAnchor.Right,
        "bottom-left" or "bottomleft" => WindowLayoutAnchor.BottomLeft,
        "bottom" => WindowLayoutAnchor.Bottom,
        "bottom-right" or "bottomright" => WindowLayoutAnchor.BottomRight,
        _ => WindowLayoutAnchor.Center,
    };

    public static string Spell(WindowLayoutAnchor anchor) => anchor switch
    {
        WindowLayoutAnchor.TopLeft => "top-left",
        WindowLayoutAnchor.Top => "top",
        WindowLayoutAnchor.TopRight => "top-right",
        WindowLayoutAnchor.Left => "left",
        WindowLayoutAnchor.Right => "right",
        WindowLayoutAnchor.BottomLeft => "bottom-left",
        WindowLayoutAnchor.Bottom => "bottom",
        WindowLayoutAnchor.BottomRight => "bottom-right",
        _ => "center",
    };

    public static RectD BoxFor(RectD visibleFrame, double gap, bool usesPreferredGap) =>
        usesPreferredGap
            ? WindowPlacementEngine.Canvas(visibleFrame, WindowPlacementEngine.SanitizedGap(gap, visibleFrame))
            : visibleFrame;

    public static RectD Resolve(WindowLayoutEntry entry, RectD box)
    {
        var widthFraction = Finite01(entry.WidthFraction);
        var heightFraction = Finite01(entry.HeightFraction);
        var width = Math.Max(1, Math.Floor(box.Width * widthFraction));
        var height = Math.Max(1, Math.Floor(box.Height * heightFraction));
        var placed = PlacementOf(entry.Anchor).Place(new SizeD(width, height), box);
        var nudged = new RectD(placed.X + entry.OffsetX, placed.Y + entry.OffsetY, placed.Width, placed.Height);
        return WindowPlacementEngine.Rounded(WindowPlacementEngine.Clamped(nudged, box));
    }

    public static WindowLayoutEntry Describe(RectD frame, RectD box, string processName, string? path, string displayId, string? id = null, string? argument = null)
    {
        var clamped = ClampSize(frame, box);
        var widthFraction = box.Width <= 0 ? 1 : clamped.Width / box.Width;
        var heightFraction = box.Height <= 0 ? 1 : clamped.Height / box.Height;
        var anchor = NearestAnchor(clamped, box, new SizeD(clamped.Width, clamped.Height));
        var placed = PlacementOf(anchor).Place(new SizeD(clamped.Width, clamped.Height), box);
        return new WindowLayoutEntry(
            id ?? Guid.NewGuid().ToString("n"),
            processName,
            path,
            displayId,
            widthFraction,
            heightFraction,
            anchor,
            clamped.X - placed.X,
            clamped.Y - placed.Y,
            argument);
    }

    public static WindowLayoutSlot SlotFromEntry(WindowLayoutEntry entry, RectD frame, int screenId) =>
        new(
            entry.ProcessName,
            frame,
            screenId,
            entry.Path,
            entry.DisplayId,
            entry.Id,
            entry.WidthFraction,
            entry.HeightFraction,
            Spell(entry.Anchor),
            entry.OffsetX,
            entry.OffsetY,
            entry.Argument);

    public static WindowLayoutEntry EntryFromSlot(WindowLayoutSlot slot)
    {
        var id = SlotId(slot);
        if (slot.WidthFraction > 0 && slot.HeightFraction > 0)
        {
            return new WindowLayoutEntry(
                id,
                slot.ProcessName,
                slot.Path,
                slot.DisplayId,
                slot.WidthFraction,
                slot.HeightFraction,
                ParseAnchor(slot.Anchor),
                slot.OffsetX,
                slot.OffsetY,
                slot.Argument);
        }

        return Describe(slot.Frame, new RectD(0, 0, Math.Max(1, slot.Frame.Width), Math.Max(1, slot.Frame.Height)), slot.ProcessName, slot.Path, slot.DisplayId, id, slot.Argument)
            with { WidthFraction = 1, HeightFraction = 1, Anchor = WindowLayoutAnchor.TopLeft, OffsetX = 0, OffsetY = 0 };
    }

    public static string SlotId(WindowLayoutSlot slot) =>
        string.IsNullOrWhiteSpace(slot.EntryId) ? slot.ProcessName + ":" + slot.ScreenId : slot.EntryId;

    public static RectD ResolveSlot(WindowLayoutSlot slot, RectD box)
    {
        if (slot.WidthFraction > 0 && slot.HeightFraction > 0)
            return Resolve(EntryFromSlot(slot), box);
        return WindowPlacementEngine.Rounded(WindowPlacementEngine.Clamped(slot.Frame, box));
    }

    public static WindowLayoutDisplay? MatchDisplay(string displayId, int screenId, IReadOnlyList<WindowLayoutDisplay> displays)
    {
        if (!string.IsNullOrWhiteSpace(displayId))
            return displays.FirstOrDefault(d => d.Id.Equals(displayId, StringComparison.OrdinalIgnoreCase));
        if (screenId >= 0 && screenId < displays.Count)
            return displays[screenId];
        return null;
    }

    public static WindowLayoutEntry? BindWindow(
        WindowLayoutEntry entry,
        RectD target,
        IReadOnlyList<(string ProcessName, string? Path, RectD Frame, int Index)> windows,
        IReadOnlySet<int> claimed)
    {
        var candidates = windows
            .Select((w, i) => (w.ProcessName, w.Path, w.Frame, w.Index))
            .Where(w => !claimed.Contains(w.Index) && SameApp(entry, w.ProcessName, w.Path) && string.IsNullOrWhiteSpace(entry.Argument))
            .Select(w => (w.Index, Distance: Distance(Center(w.Frame), Center(target))))
            .OrderBy(w => w.Distance)
            .ThenBy(w => w.Index)
            .ToList();
        if (candidates.Count == 0)
            return null;
        return entry;
    }

    public static int? NearestWindowIndex(
        WindowLayoutEntry entry,
        RectD target,
        IReadOnlyList<(string ProcessName, string? Path, RectD Frame, int Index)> windows,
        IReadOnlySet<int> claimed)
    {
        var best = -1;
        var bestDistance = double.MaxValue;
        foreach (var window in windows)
        {
            if (claimed.Contains(window.Index) || !SameApp(entry, window.ProcessName, window.Path) || !string.IsNullOrWhiteSpace(entry.Argument))
                continue;
            var distance = Distance(Center(window.Frame), Center(target));
            if (distance < bestDistance || (distance == bestDistance && window.Index < best))
            {
                bestDistance = distance;
                best = window.Index;
            }
        }

        return best < 0 ? null : best;
    }

    static bool SameApp(WindowLayoutEntry entry, string processName, string? path)
    {
        if (!string.IsNullOrWhiteSpace(entry.Path) && !string.IsNullOrWhiteSpace(path))
            return entry.Path.Equals(path, StringComparison.OrdinalIgnoreCase);
        return entry.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase);
    }

    static WindowLayoutAnchor NearestAnchor(RectD frame, RectD box, SizeD size)
    {
        WindowLayoutAnchor best = WindowLayoutAnchor.Center;
        var bestDistance = double.MaxValue;
        foreach (var anchor in Enum.GetValues<WindowLayoutAnchor>())
        {
            var placed = PlacementOf(anchor).Place(size, box);
            var distance = Distance(new(placed.X, placed.Y), new(frame.X, frame.Y));
            if (distance < bestDistance || (Math.Abs(distance - bestDistance) < 0.0001 && Prefers(anchor, best)))
            {
                bestDistance = distance;
                best = anchor;
            }
        }

        return best;
    }

    static bool Prefers(WindowLayoutAnchor candidate, WindowLayoutAnchor current)
    {
        static int Rank(WindowLayoutAnchor a) => a switch
        {
            WindowLayoutAnchor.Center => 0,
            WindowLayoutAnchor.Left or WindowLayoutAnchor.Right or WindowLayoutAnchor.Top or WindowLayoutAnchor.Bottom => 1,
            _ => 2,
        };
        return Rank(candidate) < Rank(current);
    }

    static RectD ClampSize(RectD frame, RectD box)
    {
        var width = Math.Min(Math.Max(1, frame.Width), Math.Max(1, box.Width));
        var height = Math.Min(Math.Max(1, frame.Height), Math.Max(1, box.Height));
        return new RectD(frame.X, frame.Y, width, height);
    }

    static double Finite01(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
            return 1;
        return Math.Clamp(value, 0, 1);
    }

    static (double X, double Y) Center(RectD rect) => (rect.MidX, rect.MidY);

    static double Distance((double X, double Y) a, (double X, double Y) b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
