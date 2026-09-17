namespace Tinycast.Features.Emoji;

public sealed record EmojiItem(string Glyph, string Name, string Group);

public static class EmojiCatalog
{
    public static IReadOnlyList<EmojiItem> All { get; } =
    [
        E("😀", "grinning face", "Smileys"),
        E("😃", "grinning face with big eyes", "Smileys"),
        E("😄", "grinning face with smiling eyes", "Smileys"),
        E("😁", "beaming face", "Smileys"),
        E("😅", "grinning face with sweat", "Smileys"),
        E("😂", "face with tears of joy", "Smileys"),
        E("🤣", "rolling on the floor laughing", "Smileys"),
        E("😊", "smiling face", "Smileys"),
        E("😇", "smiling face with halo", "Smileys"),
        E("🙂", "slightly smiling face", "Smileys"),
        E("😉", "winking face", "Smileys"),
        E("😍", "smiling face with heart eyes", "Smileys"),
        E("😘", "face blowing a kiss", "Smileys"),
        E("😜", "winking face with tongue", "Smileys"),
        E("🤔", "thinking face", "Smileys"),
        E("😐", "neutral face", "Smileys"),
        E("😴", "sleeping face", "Smileys"),
        E("😢", "crying face", "Smileys"),
        E("😭", "loudly crying face", "Smileys"),
        E("😡", "pouting face", "Smileys"),
        E("👍", "thumbs up", "People"),
        E("👎", "thumbs down", "People"),
        E("👏", "clapping hands", "People"),
        E("🙏", "folded hands", "People"),
        E("👋", "waving hand", "People"),
        E("👌", "ok hand", "People"),
        E("✌️", "victory hand", "People"),
        E("👀", "eyes", "People"),
        E("🔥", "fire", "Symbols"),
        E("✅", "check mark", "Symbols"),
        E("❌", "cross mark", "Symbols"),
        E("⭐", "star", "Symbols"),
        E("💡", "light bulb", "Symbols"),
        E("🎉", "party popper", "Symbols"),
        E("❤️", "red heart", "Symbols"),
        E("✨", "sparkles", "Symbols"),
        E("🚀", "rocket", "Travel"),
        E("💻", "laptop", "Objects"),
        E("⌨️", "keyboard", "Objects"),
        E("📱", "mobile phone", "Objects"),
        E("📁", "file folder", "Objects"),
        E("📌", "pushpin", "Objects"),
        E("📝", "memo", "Objects"),
        E("🔍", "magnifying glass", "Objects"),
        E("🗓️", "calendar", "Objects"),
        E("⏰", "alarm clock", "Objects"),
        E("🌙", "crescent moon", "Nature"),
        E("☀️", "sun", "Nature"),
        E("☕", "hot beverage", "Food"),
        E("🍕", "pizza", "Food"),
    ];

    public static IReadOnlyList<EmojiItem> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return All;
        return All.Where(e =>
                e.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || e.Group.Contains(query, StringComparison.OrdinalIgnoreCase)
                || e.Glyph.Contains(query))
            .ToList();
    }

    static EmojiItem E(string glyph, string name, string group) => new(glyph, name, group);
}
