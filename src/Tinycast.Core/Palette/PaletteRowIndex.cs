namespace Tinycast.Palette;

/// <summary>Flat selection index must match visible row order. Section headers consume no index.</summary>
public static class PaletteRowIndex
{
    public static int Clamp(int selection, int rowCount)
    {
        if (rowCount <= 0)
            return 0;
        if (selection < 0)
            return 0;
        if (selection >= rowCount)
            return rowCount - 1;
        return selection;
    }

    public static int Move(int selection, int delta, int rowCount)
    {
        if (rowCount <= 0)
            return 0;
        return Clamp(selection + delta, rowCount);
    }
}
