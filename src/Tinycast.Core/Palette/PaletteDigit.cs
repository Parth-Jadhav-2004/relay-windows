namespace Tinycast.Palette;

public static class PaletteDigit
{
    public static bool ActivatesSlot(string query, bool control, bool alt) =>
        control && !alt && string.IsNullOrEmpty(query);

    public static int SlotIndex(int digit) => digit == 0 ? 9 : digit - 1;
}

public static class CompactFavorites
{
    public const int VisibleSlots = 5;

    public static IReadOnlyList<string> Strip(IReadOnlyList<string> ids, int visible = VisibleSlots) =>
        ids.Take(visible).ToList();

    public static bool ShowsOverflow(int count, int visible = VisibleSlots) => count > visible;
}
