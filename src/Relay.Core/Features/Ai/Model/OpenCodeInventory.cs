namespace Relay.Features.Ai;

/// <summary>
/// Pure OpenCode inventory + protocol helpers.
/// Ports apps/server/src/provider/opencodeRuntime.ts
/// (parseServerUrlFromOutput, parseOpenCodeModelSlug, resolveOpenCodeConfigContent,
/// resolveOpenCodeServerPassword, compareSemverVersions) and
/// Layers/OpenCodeProvider.ts (flattenOpenCodeModels) without any I/O.
/// </summary>
public static class OpenCodeInventory
{
    public sealed record ProviderModel(string Id, string Name);
    public sealed record ProviderEntry(string Id, string Name, IReadOnlyDictionary<string, ProviderModel> Models);

    /// <summary>
    /// Mirrors flattenOpenCodeModels: keep only connected providers, skip empty
    /// names, emit provider/model slugs, sort by display name, append customs.
    /// </summary>
    public static IReadOnlyList<AiModelRef> Flatten(
        IEnumerable<ProviderEntry> all,
        IEnumerable<string> connected,
        IEnumerable<AiModelRef>? customModels = null)
    {
        var connectedSet = new HashSet<string>(connected ?? [], StringComparer.Ordinal);
        var models = new List<AiModelRef>();
        foreach (var provider in all ?? [])
        {
            if (!connectedSet.Contains(provider.Id))
                continue;
            if (provider.Models is null)
                continue;
            foreach (var model in provider.Models.Values)
            {
                var name = model.Name?.Trim();
                if (string.IsNullOrEmpty(name))
                    continue;
                var sub = string.IsNullOrWhiteSpace(provider.Name) ? null : provider.Name.Trim();
                models.Add(new AiModelRef(provider.Id, model.Id, name, sub, IsCustom: false));
            }
        }

        models.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCulture));
        if (customModels is not null)
            models.AddRange(customModels.Where(m => m is not null));
        return models;
    }

    /// <summary>
    /// Mirrors parseServerUrlFromOutput: first line starting with
    /// "opencode server listening", URL after "on ".
    /// </summary>
    public static string? ParseServerUrlFromOutput(string? output)
    {
        if (string.IsNullOrEmpty(output))
            return null;
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (!line.StartsWith(OpenCodeConstants.ReadyPrefix, StringComparison.Ordinal))
                continue;
            var onIndex = line.IndexOf("on ", StringComparison.Ordinal);
            if (onIndex < 0)
                continue;
            var candidate = line[(onIndex + 3)..].Trim().Split([' ', '\t'], 2)[0].Trim();
            if (candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    /// <summary>Parses "1.15.13" out of "opencode version 1.15.13" etc.</summary>
    public static string? ParseGenericCliVersion(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;
        foreach (var token in output.Split([' ', '\t', '\r', '\n', 'v', 'V'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryParseSemver(token) is not null)
                return token;
        }

        return null;
    }

    public static int[]? TryParseSemver(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return null;
        var core = version.Trim().TrimStart('v', 'V').Split(['-', '+'], 2)[0];
        var parts = core.Split('.');
        if (parts.Length < 2 || parts.Length > 4)
            return null;
        var numbers = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out numbers[i]) || numbers[i] < 0)
                return null;
        }

        return numbers;
    }

    /// <summary>Mirrors compareSemverVersions: -1 / 0 / 1.</summary>
    public static int CompareSemver(string left, string right)
    {
        var l = TryParseSemver(left) ?? [0];
        var r = TryParseSemver(right) ?? [0];
        for (var i = 0; i < Math.Max(l.Length, r.Length); i++)
        {
            var a = i < l.Length ? l[i] : 0;
            var b = i < r.Length ? r[i] : 0;
            if (a != b)
                return a < b ? -1 : 1;
        }

        return 0;
    }

    public static bool IsVersionSupported(string? version) =>
        !string.IsNullOrWhiteSpace(version)
        && TryParseSemver(version) is not null
        && CompareSemver(version, OpenCodeConstants.MinimumVersion) >= 0;

    /// <summary>
    /// Mirrors resolveOpenCodeConfigContent: explicit instance value wins, then
    /// inherited env, then "{}". Never clobber user config with unconditional "{}".
    /// </summary>
    public static string ResolveConfigContent(
        IReadOnlyDictionary<string, string?>? instanceEnv,
        IReadOnlyDictionary<string, string?>? inheritedEnv = null)
    {
        if (instanceEnv is not null
            && instanceEnv.TryGetValue("OPENCODE_CONFIG_CONTENT", out var explicitValue)
            && !string.IsNullOrEmpty(explicitValue))
            return explicitValue;
        if (inheritedEnv is not null
            && inheritedEnv.TryGetValue("OPENCODE_CONFIG_CONTENT", out var inheritedValue)
            && !string.IsNullOrEmpty(inheritedValue))
            return inheritedValue;
        return OpenCodeConstants.EmptyConfigContent;
    }

    /// <summary>
    /// Mirrors resolveOpenCodeServerPassword: explicit wins; external + none
    /// yields none (do NOT forward local env); local + none inherits env.
    /// </summary>
    public static string? ResolveServerPassword(
        bool external,
        string? configuredPassword,
        IReadOnlyDictionary<string, string?>? instanceEnv = null,
        IReadOnlyDictionary<string, string?>? inheritedEnv = null)
    {
        if (!string.IsNullOrEmpty(configuredPassword))
            return configuredPassword;
        if (external)
            return null;
        if (instanceEnv is not null)
            return instanceEnv.TryGetValue("OPENCODE_SERVER_PASSWORD", out var v) && !string.IsNullOrEmpty(v) ? v : null;
        if (inheritedEnv is not null)
            return inheritedEnv.TryGetValue("OPENCODE_SERVER_PASSWORD", out var w) && !string.IsNullOrEmpty(w) ? w : null;
        return null;
    }

    public static string BasicAuthHeader(string serverPassword)
    {
        var raw = "opencode:" + serverPassword;
        return "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
    }

    /// <summary>
    /// Mirrors formatOpenCodeProbeError mapping in Layers/OpenCodeProvider.ts.
    /// </summary>
    public static string ProbeError(bool external, string? serverUrl, Exception? cause, int? statusCode = null)
    {
        if (statusCode is 401 or 403)
            return "OpenCode server rejected authentication. Check the server URL and password.";
        var message = cause?.Message ?? "";
        if (external && !string.IsNullOrWhiteSpace(serverUrl)
            && (message.Contains("refused", StringComparison.OrdinalIgnoreCase)
                || message.Contains("ENOTFOUND", StringComparison.OrdinalIgnoreCase)
                || message.Contains("failed", StringComparison.OrdinalIgnoreCase)
                || cause is HttpRequestException or TaskCanceledException))
            return $"Couldn't reach the configured OpenCode server at {serverUrl}. Check the URL, then refresh provider status.";
        if (!external && cause is System.ComponentModel.Win32Exception)
            return "OpenCode CLI (opencode) is not installed or not on PATH. Install it, then run `opencode auth login`.";
        if (!string.IsNullOrWhiteSpace(message))
            return message.Trim();
        return "OpenCode probe failed.";
    }
}
