namespace Tinycast.Features.Snippets;

public static class SnippetFrontmatter
{
    public static StoredSnippet? Parse(string path, string contents)
    {
        var id = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(contents))
            return new StoredSnippet(id, id, "", "");

        var text = contents.Replace("\r\n", "\n");
        if (!text.StartsWith("---", StringComparison.Ordinal))
            return new StoredSnippet(id, id, "", text.TrimEnd());

        var end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end < 0)
            return new StoredSnippet(id, id, "", text.TrimEnd());

        var header = text[3..end].Trim();
        var body = text[(end + 4)..].TrimStart('\n');
        var name = id;
        var keyword = "";
        var enabled = true;
        var confirm = false;
        foreach (var line in header.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;
            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim().Trim('"');
            if (key.Equals("name", StringComparison.OrdinalIgnoreCase) && value.Length > 0)
                name = value;
            if (key.Equals("keyword", StringComparison.OrdinalIgnoreCase))
                keyword = value;
            if (key.Equals("enabled", StringComparison.OrdinalIgnoreCase))
                enabled = value is "true" or "1";
            if (key.Equals("show_confirmation", StringComparison.OrdinalIgnoreCase))
                confirm = value is "true" or "1";
        }

        return new StoredSnippet(id, name, keyword, body, enabled, confirm);
    }

    public static string Serialize(StoredSnippet snippet) =>
        "---\nname: " + snippet.Name + "\nkeyword: " + snippet.Keyword
        + "\nenabled: " + (snippet.Enabled ? "true" : "false")
        + "\nshow_confirmation: " + (snippet.ShowConfirmation ? "true" : "false")
        + "\n---\n" + snippet.Text + "\n";

    public static IReadOnlyList<string> ConflictingKeywords(IReadOnlyList<StoredSnippet> snippets)
    {
        return snippets
            .Where(s => !string.IsNullOrWhiteSpace(s.Keyword))
            .GroupBy(s => s.Keyword.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
    }
}
