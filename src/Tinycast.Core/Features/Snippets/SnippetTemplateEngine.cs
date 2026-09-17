using System.Globalization;
using System.Text;

namespace Tinycast.Features.Snippets;

public sealed record StoredSnippet(string Id, string Name, string Keyword, string Text, bool Enabled = true, bool ShowConfirmation = false);

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
            if (!IsArgumentToken(token))
                continue;
            var parsed = ParseArgument(token);
            if (parsed.DefaultValue is not null)
                continue;
            if (seen.Add(parsed.Name))
                declared.Add(new MissingArgument(parsed.Name, parsed.Options));
        }

        return declared;
    }

    public static bool UsesSelection(string text) =>
        text.Contains("{selection", StringComparison.OrdinalIgnoreCase)
        || text.Contains("{selectedText", StringComparison.OrdinalIgnoreCase);

    static bool IsArgumentToken(string token)
    {
        var name = TokenName(token);
        return name is "argument" or "query";
    }

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
        var name = TokenName(token);
        var modifiers = TokenModifiers(token);
        if (name == "cursor")
        {
            cursor = position;
            return "";
        }

        if (name is "clipboard")
        {
            var offset = IntParam(token, "offset") ?? (ColonIndex(token) is { } colon ? TryInt(token[(colon + 1)..]) : 0);
            var value = offset >= 0 && offset < context.ClipboardHistory.Count ? context.ClipboardHistory[offset] : "";
            return ApplyModifiers(value, modifiers);
        }

        if (name is "selection" or "selectedtext")
            return ApplyModifiers(context.Selection, modifiers);
        if (name is "uuid")
            return ApplyModifiers(context.MakeUuid(), modifiers);
        if (name is "day")
            return ApplyModifiers(context.Now.ToString("dddd", CultureInfo.CurrentCulture), modifiers);
        if (name is "date" or "time" or "datetime")
            return ApplyModifiers(FormatDate(name, token, context), modifiers);

        if (name is "snippet")
        {
            var id = StringParam(token, "name") ?? AfterColon(token);
            var snippet = snippets.FirstOrDefault(s =>
                s.Id.Equals(id, StringComparison.OrdinalIgnoreCase)
                || s.Name.Equals(id, StringComparison.OrdinalIgnoreCase)
                || s.Keyword.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (snippet is null || !visited.Add(snippet.Id))
                return "{" + token + "}";
            var expanded = ExpandText(snippet.Text, context, args, snippets, depth + 1, visited, missing, missingNames, ref cursor);
            visited.Remove(snippet.Id);
            return expanded;
        }

        if (name is "argument" or "query")
        {
            var parsed = ParseArgument(token);
            if (args.TryGetValue(parsed.Name, out var supplied))
                return ApplyModifiers(supplied, modifiers);
            if (parsed.DefaultValue is not null)
                return ApplyModifiers(parsed.DefaultValue, modifiers);
            if (missingNames.Add(parsed.Name))
                missing.Add(new MissingArgument(parsed.Name, parsed.Options));
            return "";
        }

        return "{" + token + "}";
    }

    static string FormatDate(string name, string token, ExpansionContext context)
    {
        var locale = StringParam(token, "locale");
        var format = StringParam(token, "format");
        var culture = CultureInfo.CurrentCulture;
        if (locale is not null)
        {
            try { culture = CultureInfo.GetCultureInfo(locale); }
            catch (Exception) { }
        }

        var clock = context.Now;
        if (TimeZoneInfo.Local.Id != context.TimeZone.Id)
            clock = TimeZoneInfo.ConvertTime(clock, context.TimeZone);
        if (OffsetParam(token) is { } offset)
            clock = ApplyOffset(clock, offset);

        if (format is not null)
        {
            try { return clock.ToString(format, locale is null ? CultureInfo.InvariantCulture : culture); }
            catch (Exception) { return clock.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        }

        return name switch
        {
            "time" => locale is null ? clock.ToString("HH:mm", CultureInfo.InvariantCulture) : clock.ToString("t", culture),
            "datetime" => locale is null ? clock.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : clock.ToString("g", culture),
            _ => locale is null ? clock.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : clock.ToString("d", culture),
        };
    }

    static DateTime ApplyOffset(DateTime clock, string offset)
    {
        foreach (var part in offset.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var text = part.Trim();
            if (text.Length < 2)
                continue;
            var unit = text[^1];
            if (!int.TryParse(text[..^1], out var amount))
                continue;
            clock = unit switch
            {
                'm' => clock.AddMinutes(amount),
                'h' => clock.AddHours(amount),
                'd' => clock.AddDays(amount),
                'M' => clock.AddMonths(amount),
                'y' => clock.AddYears(amount),
                _ => clock,
            };
        }

        return clock;
    }

    static string TokenName(string token)
    {
        var body = token.Split('|', 2)[0].Trim();
        var end = 0;
        while (end < body.Length && (char.IsLetter(body[end]) || body[end] == '-' || body[end] == '_'))
            end++;
        return body[..end].ToLowerInvariant();
    }

    static IReadOnlyList<string> TokenModifiers(string token) =>
        token.Contains('|')
            ? token.Split('|').Skip(1).Select(m => m.Trim().ToLowerInvariant()).Where(m => m.Length > 0).ToList()
            : [];

    static string? StringParam(string token, string key)
    {
        var body = token.Split('|', 2)[0];
        var needle = key + "=";
        var start = body.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;
        var rest = body[(start + needle.Length)..].Trim();
        if (rest.StartsWith('"'))
        {
            var end = rest.IndexOf('"', 1);
            return end < 0 ? Unquote(rest) : rest[1..end];
        }

        var nextKey = -1;
        for (var i = 1; i < rest.Length - 1; i++)
        {
            if (char.IsWhiteSpace(rest[i - 1]) && char.IsLetter(rest[i]))
            {
                var j = i;
                while (j < rest.Length && (char.IsLetter(rest[j]) || rest[j] == '_'))
                    j++;
                if (j < rest.Length && rest[j] == '=')
                {
                    nextKey = i - 1;
                    break;
                }
            }
        }

        return (nextKey < 0 ? rest : rest[..nextKey]).Trim();
    }

    static int? IntParam(string token, string key)
    {
        var text = StringParam(token, key);
        return int.TryParse(text, out var value) ? value : null;
    }

    static string? OffsetParam(string token) => StringParam(token, "offset");

    static int? ColonIndex(string token)
    {
        var body = token.Split('|', 2)[0];
        var colon = body.IndexOf(':');
        return colon < 0 ? null : colon;
    }

    static int TryInt(string text) => int.TryParse(text.Trim(), out var value) ? value : 0;

    static string AfterColon(string token)
    {
        var body = token.Split('|', 2)[0];
        var colon = body.IndexOf(':');
        return colon < 0 ? "" : body[(colon + 1)..].Trim().Trim('"');
    }

    static string ApplyModifiers(string value, IReadOnlyList<string> modifiers)
    {
        var current = value;
        foreach (var modifier in modifiers)
        {
            current = modifier switch
            {
                "uppercase" => current.ToUpperInvariant(),
                "lowercase" => current.ToLowerInvariant(),
                "trim" => current.Trim(),
                "percent-encode" => Uri.EscapeDataString(current),
                "json-stringify" => JsonEscape(current),
                "raw" => current,
                _ => current,
            };
        }

        return current;
    }

    static string JsonEscape(string value)
    {
        var builder = new StringBuilder();
        foreach (var c in value)
        {
            builder.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => c.ToString(),
            });
        }

        return builder.ToString();
    }

    static ArgumentSpec ParseArgument(string token)
    {
        var name = TokenName(token) == "query" ? "Argument" : "Argument";
        if (TokenName(token) == "query")
            name = "Argument";
        string? defaultValue = StringParam(token, "default");
        var optionsText = StringParam(token, "options");
        var named = StringParam(token, "name");
        if (named is { Length: > 0 })
            name = named;
        else
        {
            var body = token.Split('|', 2)[0].Trim();
            var rest = body;
            if (rest.StartsWith("argument", StringComparison.OrdinalIgnoreCase))
                rest = rest[8..].Trim();
            else if (rest.StartsWith("query", StringComparison.OrdinalIgnoreCase))
                rest = rest[5..].Trim();
            if (rest.Length > 0 && !rest.Contains('='))
                name = Unquote(rest);
        }

        var options = optionsText is null
            ? Array.Empty<string>()
            : optionsText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return new ArgumentSpec(name, defaultValue, options);
    }

    sealed record ArgumentSpec(string Name, string? DefaultValue, IReadOnlyList<string> Options);

    static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
}
