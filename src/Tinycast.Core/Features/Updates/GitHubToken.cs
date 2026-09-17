namespace Tinycast.Features.Updates;

public static class GitHubToken
{
    public const string EnvName = "TINYCAST_GITHUB_TOKEN";
    public const string FallbackEnvName = "GITHUB_TOKEN";

    public static string? Sanitize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var token = raw.Trim().Trim('\uFEFF');
        if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
            token = token[1..^1].Trim();
        else if (token.Length >= 2 && token[0] == '\'' && token[^1] == '\'')
            token = token[1..^1].Trim();

        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            token = token[7..].Trim();
        else if (token.StartsWith("token ", StringComparison.OrdinalIgnoreCase))
            token = token[6..].Trim();

        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}
