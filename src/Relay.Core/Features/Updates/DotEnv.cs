namespace Relay.Features.Updates;

public static class DotEnv
{
    public static Dictionary<string, string> Parse(string text)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(text))
            return map;

        foreach (var raw in text.Split(['\r', '\n']))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
                line = line[7..].TrimStart();
            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;
            var key = line[..eq].Trim();
            if (key.Length == 0)
                continue;
            map[key] = Unquote(line[(eq + 1)..].Trim());
        }

        return map;
    }

    public static string? Get(IReadOnlyDictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    static string Unquote(string value)
    {
        var stripped = StripUnquotedComment(value).TrimEnd();
        if (stripped.Length >= 2 && stripped[0] == '"' && stripped[^1] == '"')
            return stripped[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal);
        if (stripped.Length >= 2 && stripped[0] == '\'' && stripped[^1] == '\'')
            return stripped[1..^1];
        return stripped;
    }

    static string StripUnquotedComment(string value)
    {
        var inSingle = false;
        var inDouble = false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (inDouble)
            {
                if (c == '\\' && i + 1 < value.Length)
                {
                    i++;
                    continue;
                }
                if (c == '"')
                    inDouble = false;
                continue;
            }

            if (inSingle)
            {
                if (c == '\'')
                    inSingle = false;
                continue;
            }

            if (c == '"')
            {
                inDouble = true;
                continue;
            }

            if (c == '\'')
            {
                inSingle = true;
                continue;
            }

            if (c == '#' && (i == 0 || char.IsWhiteSpace(value[i - 1])))
                return value[..i];
        }

        return value;
    }
}
