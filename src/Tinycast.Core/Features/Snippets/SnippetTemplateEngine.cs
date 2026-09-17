using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Tinycast.Features.Snippets;

public sealed record StoredSnippet(string Id, string Name, string Keyword, string Text);

public sealed record MissingArgument(string Name, IReadOnlyList<string> Options);

public sealed record ExpansionResult(string Text, int? CursorOffsetFromEnd, IReadOnlyList<MissingArgument> MissingArguments);

public sealed class ExpansionContext
{
    public IReadOnlyList<string> ClipboardHistory { get; init; } = [];
    public string Selection { get; init; } = "";
    public DateTime Now { get; init; } = DateTime.Now;
    public TimeZoneInfo TimeZone { get; init; } = TimeZoneInfo.Local;
    public Func<string> MakeUuid { get; init; } = () => Guid.NewGuid().ToString();
    public string Clipboard => ClipboardHistory.Count > 0 ? ClipboardHistory[0] : "";
}

public static class SnippetTemplateEngine
{
    const int MaxDepth = 5;

    public static ExpansionResult Expand(string text, ExpansionContext context, IReadOnlyDictionary<string, string>? userArguments = null, IReadOnlyList<StoredSnippet>? snippets = null)
    {
        var missing = new List<MissingArgument>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cursor = (int?)null;
        var output = ExpandText(text, context, userArguments ?? new Dictionary<string, string>(), snippets ?? [], 0, [], missing, seen, ref cursor);
        return new ExpansionResult(output, cursor is int c ? output.Length - c : null, missing);
    }

    public static IReadOnlyList<MissingArgument> DeclaredArguments(string text)
    {
        var declared = new List<MissingArgument>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(text, @"\{argument(?:\s+([^}=]+))?(?:=([^}]*))?\}"))
        {
            var name = (match.Groups[1].Success ? match.Groups[1].Value : "argument").Trim();
            if (match.Value.Contains("default=", StringComparison.OrdinalIgnoreCase))
                continue;
            if (seen.Add(name))
                declared.Add(new MissingArgument(name, []));
        }

        return declared;
    }

    public static bool UsesSelection(string text) =>
        text.Contains("{selection", StringComparison.OrdinalIgnoreCase);

    static string ExpandText(
        string text,
        ExpansionContext context,
        IReadOnlyDictionary<string, string> args,
        IReadOnlyList<StoredSnippet> snippets,
        int depth,
        HashSet<string> visited,
        List<MissingArgument> missing,
        HashSet<string> missingNames,
        ref int? cursor)
    {
        if (depth > MaxDepth)
            return text;
        var output = new StringBuilder();
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] != '{')
            {
                output.Append(text[i]);
                i++;
                continue;
            }

            var end = text.IndexOf('}', i);
            if (end < 0)
            {
                output.Append(text[i..]);
                break;
            }

            var token = text[(i + 1)..end].Trim();
            i = end + 1;
            var replaced = ReplaceToken(token, context, args, snippets, depth, visited, missing, missingNames, output.Length, ref cursor);
            output.Append(replaced);
        }

        return output.ToString();
    }

    static string ReplaceToken(
        string token,
        ExpansionContext context,
        IReadOnlyDictionary<string, string> args,
        IReadOnlyList<StoredSnippet> snippets,
        int depth,
        HashSet<string> visited,
        List<MissingArgument> missing,
        HashSet<string> missingNames,
        int position,
        ref int? cursor)
    {
        var lower = token.ToLowerInvariant();
        if (lower == "cursor")
        {
            cursor = position;
            return "";
        }

        if (lower is "clipboard" or "clipboard:0")
            return ApplyModifier(context.Clipboard, token);
        if (lower.StartsWith("clipboard:", StringComparison.Ordinal))
        {
            if (int.TryParse(lower["clipboard:".Length..], out var index) && index >= 0 && index < context.ClipboardHistory.Count)
                return context.ClipboardHistory[index];
            return "";
        }

        if (lower is "selection" || lower.StartsWith("selection ", StringComparison.Ordinal))
            return ApplyModifier(context.Selection, token);
        if (lower is "uuid")
            return context.MakeUuid();
        if (lower is "date")
            return context.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (lower.StartsWith("date format=", StringComparison.Ordinal)
            || lower.StartsWith("date:", StringComparison.Ordinal))
        {
            var format = lower.StartsWith("date format=", StringComparison.Ordinal)
                ? token[12..].Trim().Trim('"')
                : token[5..].Trim().Trim('"');
            try { return context.Now.ToString(format, CultureInfo.InvariantCulture); }
            catch (Exception) { return context.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        }

        if (lower is "time")
            return context.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (lower is "datetime")
            return context.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        if (lower.StartsWith("snippet:", StringComparison.Ordinal))
        {
            var id = token[8..].Trim();
            var snippet = snippets.FirstOrDefault(s => s.Id == id || s.Name.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (snippet is null || !visited.Add(snippet.Id))
                return "{" + token + "}";
            return ExpandText(snippet.Text, context, args, snippets, depth + 1, visited, missing, missingNames, ref cursor);
        }

        if (lower.StartsWith("argument", StringComparison.Ordinal))
        {
            var name = "argument";
            string? defaultValue = null;
            var parts = token.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1)
            {
                var rest = parts[1];
                var def = Regex.Match(rest, @"default=([^\s}]+)");
                if (def.Success)
                    defaultValue = def.Groups[1].Value;
                var namePart = Regex.Replace(rest, @"default=[^\s}]+", "").Trim();
                if (namePart.Length > 0)
                    name = namePart;
            }

            if (args.TryGetValue(name, out var supplied))
                return supplied;
            if (defaultValue is not null)
                return defaultValue;
            if (missingNames.Add(name))
                missing.Add(new MissingArgument(name, []));
            return "";
        }

        return "{" + token + "}";
    }

    static string ApplyModifier(string value, string token)
    {
        var lower = token.ToLowerInvariant();
        if (lower.Contains("uppercase", StringComparison.Ordinal))
            return value.ToUpperInvariant();
        if (lower.Contains("lowercase", StringComparison.Ordinal))
            return value.ToLowerInvariant();
        if (lower.Contains("trim", StringComparison.Ordinal))
            return value.Trim();
        return value;
    }
}
