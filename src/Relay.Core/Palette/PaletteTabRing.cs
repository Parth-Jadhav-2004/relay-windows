namespace Relay.Palette;

public static class PaletteTabRing
{
    public static PaletteMode Next(PaletteMode current, bool clipboardEnabled, bool aiEnabled, bool backwards)
    {
        var ring = new List<PaletteMode> { PaletteMode.Launcher };
        if (aiEnabled)
            ring.Add(PaletteMode.AiChat);
        if (clipboardEnabled)
            ring.Add(PaletteMode.Clipboard);

        var index = ring.IndexOf(current);
        if (index < 0)
            return PaletteMode.Launcher;
        var count = ring.Count;
        var next = backwards ? (index - 1 + count) % count : (index + 1) % count;
        return ring[next];
    }

    public static string Hint(PaletteMode current, bool clipboardEnabled, bool aiEnabled)
    {
        var next = Next(current, clipboardEnabled, aiEnabled, backwards: false);
        return next switch
        {
            PaletteMode.Clipboard => "Tab  Clipboard",
            PaletteMode.AiChat => "Tab  Chat",
            _ => "Tab  Search",
        };
    }
}
