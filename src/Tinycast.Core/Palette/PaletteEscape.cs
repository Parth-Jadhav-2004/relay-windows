namespace Tinycast.Palette;

public static class PaletteEscape
{
    public static bool ClearsQueryFirst(string query, bool enabled) =>
        enabled && !string.IsNullOrEmpty(query);
}
