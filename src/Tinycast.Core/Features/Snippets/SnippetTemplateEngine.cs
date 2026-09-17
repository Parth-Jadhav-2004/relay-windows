using System.Globalization;
using System.Text;

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
        foreach (var token in Tokens(text))
        {
            if (!token.StartsWith("argument", StringComparison.OrdinalIgnoreCase))
                continue;
            var (name, defaultValue) = ParseArgument(token);
            if (defaultValue is not null)
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
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] is '{' or '}')
            {
                output.Append(text[i + 1]);
                i += 2;
                continue;
            }

            if (text[i] != '{')
            {
                output.Append(text[i]);
                i++;
                continue;
            }

            if (!TryReadBraceToken(text, i, out var end, out var token))
            {
                output.Append(text[i..]);
                break;
            }

            i = end + 1;
            var replaced = ReplaceToken(token, context, args, snippets, depth, visited, missing, missingNames, output.Length, ref cursor);
            output.Append(replaced);
        }

        return output.ToString();
    }

    static IEnumerable<string> Tokens(string text)
    {
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] is '{' or '}')
            {
                i += 2;
                continue;
            }

            if (text[i] != '{')
            {
                i++;
                continue;
            }

            if (!TryReadBraceToken(text, i, out var end, out var token))
                yield break;
            yield return token;
            i = end + 1;
        }
    }

    static bool TryReadBraceToken(string text, int start, out int end, out string token)
    {
        var body = new StringBuilder();
        var depth = 0;
        var i = start + 1;
        while (i < text.Length)
        {
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] is '{' or '}')
            {
                body.Append(text[i + 1]);
                i += 2;
                continue;
            }

            if (text[i] == '{')
            {
                depth++;
                body.Append('{');
                i++;
                continue;
            }

            if (text[i] == '}')
            {
                if (depth == 0)
                {
                    end = i;
                    token = body.ToString().Trim();
                    return true;
                }

                depth--;
                body.Append('}');
                i++;
                continue;
            }

            body.Append(text[i]);
            i++;
        }

        end = -1;
        token = "";
        return false;
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
            var expanded = ExpandText(snippet.Text, context, args, snippets, depth + 1, visited, missing, missingNames, ref cursor);
            visited.Remove(snippet.Id);
            return expanded;
        }

        if (lower.StartsWith("argument", StringComparison.Ordinal))
        {
            var (name, defaultValue) = ParseArgument(token);
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

    static (string Name, string? DefaultValue) ParseArgument(string token)
    {
        var name = "argument";
        string? defaultValue = null;
        if (!token.StartsWith("argument", StringComparison.OrdinalIgnoreCase))
            return (name, defaultValue);
        var rest = token[8..].TrimStart();
        if (rest.Length == 0)
            return (name, defaultValue);

        var defAt = rest.IndexOf("default=", StringComparison.OrdinalIgnoreCase);
        if (defAt >= 0)
        {
            defaultValue = Unquote(rest[(defAt + "default=".Length)..].Trim());
            rest = rest[..defAt].Trim();
        }

        if (rest.StartsWith("name=", StringComparison.OrdinalIgnoreCase))
            rest = rest["name=".Length..].Trim();
        if (rest.Length > 0)
            name = Unquote(rest);
        return (name, defaultValue);
    }

    static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

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
