namespace Tinycast.Features.WindowManagement;

public enum WindowCommandKind { Geometry, Restore, Fullscreen, Space }
public enum WindowCommandGroup { Halves, Quarters, Fourths, Thirds, Sizing, Moving, Fullscreen, Spaces }
public enum WindowCycle { Off, Sizes, Displays }
public enum AxisAnchor { Min, Center, Max }

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double MinX => X;
    public double MinY => Y;
    public double MaxX => X + Width;
    public double MaxY => Y + Height;
    public double MidX => X + Width / 2;
    public double MidY => Y + Height / 2;
    public double Area => Math.Max(0, Width) * Math.Max(0, Height);

    public RectD Intersect(RectD other)
    {
        var x0 = Math.Max(MinX, other.MinX);
        var y0 = Math.Max(MinY, other.MinY);
        var x1 = Math.Min(MaxX, other.MaxX);
        var y1 = Math.Min(MaxY, other.MaxY);
        return x1 <= x0 || y1 <= y0 ? new RectD(0, 0, 0, 0) : new RectD(x0, y0, x1 - x0, y1 - y0);
    }

    public bool Contains(double px, double py) => px >= MinX && px < MaxX && py >= MinY && py < MaxY;

    public RectD Inset(double gap) =>
        new(X + gap, Y + gap, Math.Max(0, Width - 2 * gap), Math.Max(0, Height - 2 * gap));
}

public readonly record struct SizeD(double Width, double Height);

public sealed record WindowCommand(
    string Id,
    string Name,
    WindowCommandKind Kind,
    WindowCommandGroup Group,
    bool CyclesOnRepeat,
    bool Resizes)
{
    public string EntryId => "window-command:" + Id;
}

public static class WindowCommandCatalog
{
    public static IReadOnlyList<WindowCommand> All { get; } =
    [
        C("left-half", "Left Half", WindowCommandKind.Geometry, WindowCommandGroup.Halves, true, true),
        C("right-half", "Right Half", WindowCommandKind.Geometry, WindowCommandGroup.Halves, true, true),
        C("top-half", "Top Half", WindowCommandKind.Geometry, WindowCommandGroup.Halves, true, true),
        C("bottom-half", "Bottom Half", WindowCommandKind.Geometry, WindowCommandGroup.Halves, true, true),
        C("top-left-quarter", "Top Left Quarter", WindowCommandKind.Geometry, WindowCommandGroup.Quarters, false, true),
        C("top-right-quarter", "Top Right Quarter", WindowCommandKind.Geometry, WindowCommandGroup.Quarters, false, true),
        C("bottom-left-quarter", "Bottom Left Quarter", WindowCommandKind.Geometry, WindowCommandGroup.Quarters, false, true),
        C("bottom-right-quarter", "Bottom Right Quarter", WindowCommandKind.Geometry, WindowCommandGroup.Quarters, false, true),
        C("first-three-fourths", "First Three Fourths", WindowCommandKind.Geometry, WindowCommandGroup.Fourths, false, true),
        C("last-three-fourths", "Last Three Fourths", WindowCommandKind.Geometry, WindowCommandGroup.Fourths, false, true),
        C("first-third", "First Third", WindowCommandKind.Geometry, WindowCommandGroup.Thirds, false, true),
        C("center-third", "Center Third", WindowCommandKind.Geometry, WindowCommandGroup.Thirds, false, true),
        C("last-third", "Last Third", WindowCommandKind.Geometry, WindowCommandGroup.Thirds, false, true),
        C("first-two-thirds", "First Two Thirds", WindowCommandKind.Geometry, WindowCommandGroup.Thirds, false, true),
        C("last-two-thirds", "Last Two Thirds", WindowCommandKind.Geometry, WindowCommandGroup.Thirds, false, true),
        C("maximize", "Maximize", WindowCommandKind.Geometry, WindowCommandGroup.Sizing, false, true),
        C("almost-maximize", "Almost Maximize", WindowCommandKind.Geometry, WindowCommandGroup.Sizing, false, true),
        C("reasonable-size", "Reasonable Size", WindowCommandKind.Geometry, WindowCommandGroup.Sizing, false, true),
        C("maximize-height", "Maximize Height", WindowCommandKind.Geometry, WindowCommandGroup.Sizing, false, true),
        C("maximize-width", "Maximize Width", WindowCommandKind.Geometry, WindowCommandGroup.Sizing, false, true),
        C("center", "Center", WindowCommandKind.Geometry, WindowCommandGroup.Sizing, false, true),
        C("center-half", "Center Half", WindowCommandKind.Geometry, WindowCommandGroup.Sizing, false, true),
        C("center-two-thirds", "Center Two Thirds", WindowCommandKind.Geometry, WindowCommandGroup.Sizing, false, true),
        C("make-larger", "Make Larger", WindowCommandKind.Geometry, WindowCommandGroup.Sizing, false, true),
        C("make-smaller", "Make Smaller", WindowCommandKind.Geometry, WindowCommandGroup.Sizing, false, true),
        C("restore", "Restore Window", WindowCommandKind.Restore, WindowCommandGroup.Sizing, false, true),
        C("move-left", "Move Left", WindowCommandKind.Geometry, WindowCommandGroup.Moving, false, false),
        C("move-right", "Move Right", WindowCommandKind.Geometry, WindowCommandGroup.Moving, false, false),
        C("move-up", "Move Up", WindowCommandKind.Geometry, WindowCommandGroup.Moving, false, false),
        C("move-down", "Move Down", WindowCommandKind.Geometry, WindowCommandGroup.Moving, false, false),
        C("next-display", "Move to Next Display", WindowCommandKind.Geometry, WindowCommandGroup.Moving, false, true),
        C("previous-display", "Move to Previous Display", WindowCommandKind.Geometry, WindowCommandGroup.Moving, false, true),
        C("toggle-fullscreen", "Toggle Fullscreen", WindowCommandKind.Fullscreen, WindowCommandGroup.Fullscreen, false, true),
        C("previous-space", "Switch to Previous Desktop", WindowCommandKind.Space, WindowCommandGroup.Spaces, false, false),
        C("next-space", "Switch to Next Desktop", WindowCommandKind.Space, WindowCommandGroup.Spaces, false, false),
    ];

    static readonly Dictionary<string, WindowCommand> ById = All.ToDictionary(c => c.Id, StringComparer.Ordinal);

    public static WindowCommand? Find(string id) =>
        ById.TryGetValue(id, out var c) ? c : All.FirstOrDefault(x => x.EntryId == id);

    static WindowCommand C(string id, string name, WindowCommandKind kind, WindowCommandGroup group, bool cycles, bool resizes) =>
        new(id, name, kind, group, cycles, resizes);
}

public readonly record struct PlacementAnchor(AxisAnchor Horizontal, AxisAnchor Vertical)
{
    public static PlacementAnchor TopLeading { get; } = new(AxisAnchor.Min, AxisAnchor.Min);
    public static PlacementAnchor Centered { get; } = new(AxisAnchor.Center, AxisAnchor.Center);

    public RectD Place(SizeD size, RectD slot)
    {
        static double Origin(AxisAnchor axis, double slotMin, double slotLength, double length) => axis switch
        {
            AxisAnchor.Min => slotMin,
            AxisAnchor.Center => slotMin + (slotLength - length) / 2,
            _ => slotMin + slotLength - length,
        };

        return new RectD(
            Origin(Horizontal, slot.X, slot.Width, size.Width),
            Origin(Vertical, slot.Y, slot.Height, size.Height),
            size.Width,
            size.Height);
    }
}

public readonly record struct ScreenSpec(int Id, RectD Frame, RectD VisibleFrame);

public sealed class PlacementInput
{
    public string Command { get; init; } = "";
    public RectD WindowFrame { get; init; }
    public IReadOnlyList<ScreenSpec> Screens { get; init; } = [];
    public double Gap { get; init; }
    public int Step { get; init; }
    public WindowCycle Cycle { get; init; } = WindowCycle.Off;
    public int? OriginScreenId { get; init; }
    public RectD? RestoreFrame { get; init; }
    public string? LastTileCommand { get; init; }
}

public readonly record struct Placement(RectD Frame, int ScreenId, PlacementAnchor Anchor, bool Resizes);

public static class WindowPlacementEngine
{
    const double StepFraction = 0.05;
    const double AlmostMaximizeFraction = 0.9;
    const double ReasonableSizeFraction = 0.6;
    const double OneThird = 1.0 / 3.0;
    const double TwoThirds = 2.0 / 3.0;
    static readonly double[] SizeCycle = [0.5, OneThird, TwoThirds];

    public static Placement? PlacementFor(PlacementInput input)
    {
        var command = WindowCommandCatalog.Find(input.Command);
        if (command is null || input.Screens.Count == 0)
            return null;
        if (command.Kind is not (WindowCommandKind.Geometry or WindowCommandKind.Restore))
            return null;
        if (command.Kind == WindowCommandKind.Restore)
            return Restore(input);

        var host = ScreenContaining(input.WindowFrame, input.Screens);
        if (host is null || host.Value.VisibleFrame.Width <= 0 || host.Value.VisibleFrame.Height <= 0)
            return null;
        var gap = SanitizedGap(input.Gap, host.Value.VisibleFrame);
        if (input.Command is "next-display" or "previous-display")
            return DisplayPlacement(input, host.Value);

        var step = Wrapped(input.Step, CycleLength(input.Command, input.Screens, input.Cycle));
        if (HalfOf(input.Command) is { } half)
            return HalfPlacement(input, half, host.Value, step);
        if (TileFractions(input.Command) is { } fractions)
            return TilePlacement(fractions, host.Value, gap);

        var canvas = Canvas(host.Value.VisibleFrame, gap);
        if (canvas.Width <= 0 || canvas.Height <= 0)
            return null;
        var current = input.WindowFrame;
        return input.Command switch
        {
            "maximize" => new Placement(canvas, host.Value.Id, PlacementAnchor.TopLeading, true),
            "almost-maximize" => new Placement(
                Rounded(PlacementAnchor.Centered.Place(
                    new SizeD(canvas.Width * AlmostMaximizeFraction, canvas.Height * AlmostMaximizeFraction), canvas)),
                host.Value.Id, PlacementAnchor.Centered, true),
            "reasonable-size" => new Placement(
                Rounded(PlacementAnchor.Centered.Place(
                    new SizeD(Math.Min(canvas.Width * ReasonableSizeFraction, 1025), Math.Min(canvas.Height * ReasonableSizeFraction, 900)),
                    canvas)),
                host.Value.Id, PlacementAnchor.Centered, true),
            "maximize-height" => new Placement(
                Rounded(Clamped(new RectD(current.X, canvas.Y, current.Width, canvas.Height), canvas)),
                host.Value.Id, PlacementAnchor.TopLeading, true),
            "maximize-width" => new Placement(
                Rounded(Clamped(new RectD(canvas.X, current.Y, canvas.Width, current.Height), canvas)),
                host.Value.Id, PlacementAnchor.TopLeading, true),
            "center" => new Placement(
                Rounded(PlacementAnchor.Centered.Place(
                    new SizeD(Math.Min(current.Width, canvas.Width), Math.Min(current.Height, canvas.Height)), canvas)),
                host.Value.Id, PlacementAnchor.Centered, true),
            "make-larger" => new Placement(Resized(current, canvas, true), host.Value.Id, PlacementAnchor.Centered, true),
            "make-smaller" => new Placement(Resized(current, canvas, false), host.Value.Id, PlacementAnchor.Centered, true),
            "move-left" or "move-right" or "move-up" or "move-down" =>
                new Placement(Nudged(current, canvas, input.Command), host.Value.Id, PlacementAnchor.TopLeading, false),
            _ => null,
        };
    }

    public static ScreenSpec? ScreenContaining(RectD frame, IReadOnlyList<ScreenSpec> screens)
    {
        ScreenSpec? best = null;
        var bestArea = 0.0;
        foreach (var screen in screens)
        {
            var area = screen.Frame.Intersect(frame).Area;
            if (area > bestArea)
            {
                bestArea = area;
                best = screen;
            }
        }

        if (best is not null && bestArea > 0)
            return best;
        return screens.FirstOrDefault(s => s.Frame.Contains(frame.MidX, frame.MidY), screens[0]);
    }

    public static IReadOnlyList<ScreenSpec> Ordered(IReadOnlyList<ScreenSpec> screens) =>
        screens.OrderBy(s => s.Frame.X).ThenBy(s => s.Frame.Y).ToList();

    public static int CycleLength(string command, IReadOnlyList<ScreenSpec> screens, WindowCycle cycle)
    {
        var catalog = WindowCommandCatalog.Find(command);
        if (catalog?.CyclesOnRepeat != true)
            return 1;
        return cycle switch
        {
            WindowCycle.Off => 1,
            WindowCycle.Sizes => SizeCycle.Length,
            WindowCycle.Displays => screens.Count > 1 ? screens.Count * 2 : 1,
            _ => 1,
        };
    }

    public static bool IsTileCommand(string command) => TileFractions(command) is not null;

    public static RectD Tile(RectD visible, double x0, double x1, double y0, double y1, double gap)
    {
        var left = visible.MinX + x0 * visible.Width + (x0 == 0 ? gap : gap / 2);
        var right = visible.MinX + x1 * visible.Width - (x1 == 1 ? gap : gap / 2);
        var top = visible.MinY + y0 * visible.Height + (y0 == 0 ? gap : gap / 2);
        var bottom = visible.MinY + y1 * visible.Height - (y1 == 1 ? gap : gap / 2);
        return Rounded(new RectD(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top)));
    }

    public static RectD Canvas(RectD visible, double gap) => Rounded(visible.Inset(gap));

    public static RectD Rounded(RectD rect)
    {
        var minX = Math.Round(rect.MinX);
        var minY = Math.Round(rect.MinY);
        var maxX = Math.Round(rect.MaxX);
        var maxY = Math.Round(rect.MaxY);
        return new RectD(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
    }

    public static RectD Clamped(RectD frame, RectD box)
    {
        var x = Math.Min(Math.Max(frame.MinX, box.MinX), Math.Max(box.MinX, box.MaxX - frame.Width));
        var y = Math.Min(Math.Max(frame.MinY, box.MinY), Math.Max(box.MinY, box.MaxY - frame.Height));
        return new RectD(x, y, frame.Width, frame.Height);
    }

    public static double SanitizedGap(double gap, RectD visible)
    {
        if (double.IsNaN(gap) || double.IsInfinity(gap) || gap <= 0 || visible.Width <= 0 || visible.Height <= 0)
            return 0;
        return Math.Min(gap, Math.Min(visible.Width, visible.Height) / 10);
    }

    sealed record Fractions(double X0, double X1, double Y0, double Y1, PlacementAnchor Anchor);
    sealed record Half(bool Horizontal, bool Leading);

    static Half? HalfOf(string command) => command switch
    {
        "left-half" => new Half(true, true),
        "right-half" => new Half(true, false),
        "top-half" => new Half(false, true),
        "bottom-half" => new Half(false, false),
        _ => null,
    };

    static Fractions HalfFractions(Half half, double fraction)
    {
        var span = half.Leading ? (0.0, fraction) : (1 - fraction, 1.0);
        var along = half.Leading ? AxisAnchor.Min : AxisAnchor.Max;
        return half.Horizontal
            ? new Fractions(span.Item1, span.Item2, 0, 1, new PlacementAnchor(along, AxisAnchor.Min))
            : new Fractions(0, 1, span.Item1, span.Item2, new PlacementAnchor(AxisAnchor.Min, along));
    }

    static Fractions? TileFractions(string command)
    {
        if (HalfOf(command) is { } half)
            return HalfFractions(half, 0.5);
        return command switch
        {
            "top-left-quarter" => new Fractions(0, 0.5, 0, 0.5, PlacementAnchor.TopLeading),
            "top-right-quarter" => new Fractions(0.5, 1, 0, 0.5, new PlacementAnchor(AxisAnchor.Max, AxisAnchor.Min)),
            "bottom-left-quarter" => new Fractions(0, 0.5, 0.5, 1, new PlacementAnchor(AxisAnchor.Min, AxisAnchor.Max)),
            "bottom-right-quarter" => new Fractions(0.5, 1, 0.5, 1, new PlacementAnchor(AxisAnchor.Max, AxisAnchor.Max)),
            "first-three-fourths" => new Fractions(0, 0.75, 0, 1, PlacementAnchor.TopLeading),
            "last-three-fourths" => new Fractions(0.25, 1, 0, 1, new PlacementAnchor(AxisAnchor.Max, AxisAnchor.Min)),
            "first-third" => new Fractions(0, OneThird, 0, 1, PlacementAnchor.TopLeading),
            "center-third" => new Fractions(OneThird, TwoThirds, 0, 1, new PlacementAnchor(AxisAnchor.Center, AxisAnchor.Min)),
            "last-third" => new Fractions(TwoThirds, 1, 0, 1, new PlacementAnchor(AxisAnchor.Max, AxisAnchor.Min)),
            "first-two-thirds" => new Fractions(0, TwoThirds, 0, 1, PlacementAnchor.TopLeading),
            "last-two-thirds" => new Fractions(OneThird, 1, 0, 1, new PlacementAnchor(AxisAnchor.Max, AxisAnchor.Min)),
            "center-half" => new Fractions(0.25, 0.75, 0, 1, new PlacementAnchor(AxisAnchor.Center, AxisAnchor.Min)),
            "center-two-thirds" => new Fractions(OneThird / 2, 1 - OneThird / 2, 0, 1, new PlacementAnchor(AxisAnchor.Center, AxisAnchor.Min)),
            _ => null,
        };
    }

    static Placement TilePlacement(Fractions fractions, ScreenSpec screen, double gap) =>
        new(Tile(screen.VisibleFrame, fractions.X0, fractions.X1, fractions.Y0, fractions.Y1, gap),
            screen.Id, fractions.Anchor, true);

    static Placement HalfPlacement(PlacementInput input, Half half, ScreenSpec host, int step)
    {
        if (input.Cycle != WindowCycle.Displays)
            return TilePlacement(HalfFractions(half, SizeCycle[step]), host, SanitizedGap(input.Gap, host.VisibleFrame));
        var strip = Ordered(input.Screens);
        if (strip.Count <= 1)
            return TilePlacement(HalfFractions(half, 0.5), host, SanitizedGap(input.Gap, host.VisibleFrame));
        var originIndex = strip.ToList().FindIndex(s => s.Id == (input.OriginScreenId ?? host.Id));
        if (originIndex < 0)
            originIndex = strip.ToList().FindIndex(s => s.Id == host.Id);
        if (originIndex < 0)
            return TilePlacement(HalfFractions(half, 0.5), host, SanitizedGap(input.Gap, host.VisibleFrame));
        var leads = half.Leading;
        var slot = Wrapped(originIndex * 2 + (leads ? 0 : 1) + (leads ? -step : step), strip.Count * 2);
        var edgeLeading = slot % 2 == 0;
        var destination = strip[slot / 2];
        return TilePlacement(HalfFractions(new Half(half.Horizontal, edgeLeading), 0.5), destination, SanitizedGap(input.Gap, destination.VisibleFrame));
    }

    static Placement? DisplayPlacement(PlacementInput input, ScreenSpec host)
    {
        var ordered = Ordered(input.Screens);
        if (ordered.Count <= 1)
            return null;
        var index = ordered.ToList().FindIndex(s => s.Id == host.Id);
        if (index < 0)
            return null;
        var offset = input.Command == "next-display" ? 1 : -1;
        var destination = ordered[(index + offset + ordered.Count) % ordered.Count];
        var destGap = SanitizedGap(input.Gap, destination.VisibleFrame);
        RectD frame;
        if (input.LastTileCommand is string last && TileFractions(last) is { } fractions)
            frame = Tile(destination.VisibleFrame, fractions.X0, fractions.X1, fractions.Y0, fractions.Y1, destGap);
        else
        {
            var source = host.VisibleFrame;
            var target = destination.VisibleFrame;
            if (source.Width <= 0 || source.Height <= 0)
                return null;
            var relativeX = (input.WindowFrame.MinX - source.MinX) / source.Width;
            var relativeY = (input.WindowFrame.MinY - source.MinY) / source.Height;
            var scaled = new RectD(
                target.MinX + relativeX * target.Width,
                target.MinY + relativeY * target.Height,
                Math.Min(target.Width, input.WindowFrame.Width / source.Width * target.Width),
                Math.Min(target.Height, input.WindowFrame.Height / source.Height * target.Height));
            frame = Rounded(Clamped(scaled, target));
        }

        return new Placement(frame, destination.Id, PlacementAnchor.Centered, true);
    }

    static Placement? Restore(PlacementInput input)
    {
        if (input.RestoreFrame is not { } restore)
            return null;
        var host = ScreenContaining(restore, input.Screens);
        if (host is null)
            return null;
        var overlap = host.Value.VisibleFrame.Intersect(restore);
        var stranded = overlap.Area == 0 || overlap.Width < 40 || overlap.Height < 40;
        var frame = stranded
            ? Rounded(Clamped(PlacementAnchor.Centered.Place(new SizeD(restore.Width, restore.Height), host.Value.VisibleFrame), host.Value.VisibleFrame))
            : restore;
        return new Placement(frame, host.Value.Id, PlacementAnchor.Centered, true);
    }

    static RectD Resized(RectD frame, RectD canvas, bool larger)
    {
        var direction = larger ? 1 : -1;
        var floorW = Math.Min(canvas.Width, Math.Max(200, canvas.Width * 0.15));
        var floorH = Math.Min(canvas.Height, Math.Max(150, canvas.Height * 0.15));
        var width = Math.Min(canvas.Width, Math.Max(floorW, frame.Width + direction * EvenStep(canvas.Width)));
        var height = Math.Min(canvas.Height, Math.Max(floorH, frame.Height + direction * EvenStep(canvas.Height)));
        var centred = new RectD(frame.MinX - (width - frame.Width) / 2, frame.MinY - (height - frame.Height) / 2, width, height);
        return Rounded(Clamped(centred, canvas));
    }

    static double EvenStep(double dimension) => Math.Max(2, Math.Round(dimension * StepFraction / 2) * 2);

    static RectD Nudged(RectD frame, RectD canvas, string command)
    {
        var dx = Math.Round(canvas.Width * StepFraction);
        var dy = Math.Round(canvas.Height * StepFraction);
        var moved = command switch
        {
            "move-left" => frame with { X = frame.X - dx },
            "move-right" => frame with { X = frame.X + dx },
            "move-up" => frame with { Y = frame.Y - dy },
            "move-down" => frame with { Y = frame.Y + dy },
            _ => frame,
        };
        return Rounded(Clamped(moved, canvas));
    }

    static int Wrapped(int value, int length)
    {
        if (length <= 0)
            return 0;
        return ((value % length) + length) % length;
    }
}

public sealed class WindowActionMemory
{
    readonly Dictionary<nint, RectD> _restore = [];
    readonly Dictionary<nint, (string Command, int Step)> _cycle = [];

    public void Remember(nint hwnd, RectD frame) => _restore[hwnd] = frame;
    public RectD? Restore(nint hwnd) => _restore.TryGetValue(hwnd, out var f) ? f : null;

    public int NextStep(nint hwnd, string command)
    {
        if (_cycle.TryGetValue(hwnd, out var state) && state.Command == command)
        {
            var next = state.Step + 1;
            _cycle[hwnd] = (command, next);
            return next;
        }

        _cycle[hwnd] = (command, 0);
        return 0;
    }

    public string? LastCommand(nint hwnd) =>
        _cycle.TryGetValue(hwnd, out var state) ? state.Command : null;

    public void Forget(nint hwnd)
    {
        _restore.Remove(hwnd);
        _cycle.Remove(hwnd);
    }
}

