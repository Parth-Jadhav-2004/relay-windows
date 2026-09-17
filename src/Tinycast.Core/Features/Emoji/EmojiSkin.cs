namespace Tinycast.Features.Emoji;

public static class EmojiSkin
{
    public static IReadOnlyList<string> Labels { get; } =
        ["Default", "Light", "Medium-light", "Medium", "Medium-dark", "Dark"];

    static readonly string[] Modifiers =
    [
        "",
        "\U0001F3FB",
        "\U0001F3FC",
        "\U0001F3FD",
        "\U0001F3FE",
        "\U0001F3FF",
    ];

    public static string Apply(string glyph, int tone)
    {
        var stripped = Strip(glyph);
        if (tone <= 0 || tone >= Modifiers.Length || !CanTint(stripped))
            return stripped;
        return stripped + Modifiers[tone];
    }

    public static bool CanTint(string glyph)
    {
        var stripped = Strip(glyph);
        return EmojiCatalog.Find(stripped)?.CanTint == true
               || stripped is "👍" or "👎" or "👏" or "🙏" or "👋" or "👌" or "✌️" or "🤞" or "💪";
    }

    public static string Strip(string glyph)
    {
        var value = glyph;
        foreach (var modifier in Modifiers)
        {
            if (modifier.Length > 0)
                value = value.Replace(modifier, "", StringComparison.Ordinal);
        }

        return value;
    }
}
