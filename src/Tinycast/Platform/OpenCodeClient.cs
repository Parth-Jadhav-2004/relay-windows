using System.Net;
using System.Text;
using System.Text.Json;
using Tinycast.Features.Ai;

namespace Tinycast.Platform;

/// <summary>
/// Minimal OpenCode HTTP client. Mirrors the @opencode-ai/sdk/v2 calls T3 uses
/// (global.health, provider.list, session.create/prompt/abort, event.subscribe).
/// No client-side requests-per-minute cap (T3 parity): upstream 429/rate_limit
/// is surfaced verbatim; only transient SQLite-lock CLI retries exist upstream
/// and do not apply to this HTTP path.
/// </summary>
public sealed class OpenCodeClient : IDisposable
{
    readonly HttpClient _http;
    readonly string _baseUrl;
    bool _disposed;

    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public OpenCodeClient(string baseUrl, string? serverPassword, TimeSpan? timeout = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = OpenCodeServerManager.BuildHttp(serverPassword, timeout ?? TimeSpan.FromMinutes(5));
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _http.Dispose();
    }

    public async Task<IReadOnlyList<AiModelRef>> ListModelsAsync(
        IEnumerable<AiModelRef>? customModels = null, CancellationToken token = default)
    {
        using var response = await _http.GetAsync(_baseUrl + "/provider", token);
        await ThrowIfAuthAsync(response, "provider.list");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(token);
        using var doc = JsonDocument.Parse(body);

        var connected = new List<string>();
        if (doc.RootElement.TryGetProperty("connected", out var conn) && conn.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in conn.EnumerateArray())
            {
                if (item.GetString() is { } id && !string.IsNullOrWhiteSpace(id))
                    connected.Add(id);
            }
        }

        var providers = new List<OpenCodeInventory.ProviderEntry>();
        if (doc.RootElement.TryGetProperty("all", out var all) && all.ValueKind == JsonValueKind.Array)
        {
            foreach (var provider in all.EnumerateArray())
            {
                var id = provider.TryGetProperty("id", out var pid) ? pid.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                var name = provider.TryGetProperty("name", out var pname) ? pname.GetString() ?? id : id;
                var models = new Dictionary<string, OpenCodeInventory.ProviderModel>(StringComparer.Ordinal);
                if (provider.TryGetProperty("models", out var modelsEl))
                {
                    if (modelsEl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in modelsEl.EnumerateObject())
                        {
                            var modelName = prop.Value.ValueKind == JsonValueKind.Object
                                && prop.Value.TryGetProperty("name", out var mn)
                                ? mn.GetString() ?? prop.Name
                                : prop.Name;
                            var modelId = prop.Value.ValueKind == JsonValueKind.Object
                                && prop.Value.TryGetProperty("id", out var mid) && !string.IsNullOrWhiteSpace(mid.GetString())
                                ? mid.GetString()!
                                : prop.Name;
                            models[modelId] = new OpenCodeInventory.ProviderModel(modelId, modelName);
                        }
                    }
                    else if (modelsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in modelsEl.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object
                                && item.TryGetProperty("id", out var mid)
                                && mid.GetString() is { } modelId)
                            {
                                var modelName = item.TryGetProperty("name", out var mn) ? mn.GetString() ?? modelId : modelId;
                                models[modelId] = new OpenCodeInventory.ProviderModel(modelId, modelName);
                            }
                            else if (item.ValueKind == JsonValueKind.String && item.GetString() is { } sid)
                            {
                                models[sid] = new OpenCodeInventory.ProviderModel(sid, sid);
                            }
                        }
                    }
                }

                providers.Add(new OpenCodeInventory.ProviderEntry(id, name, models));
            }
        }

        return OpenCodeInventory.Flatten(providers, connected, customModels);
    }

    /// <summary>
    /// POST /session with T3's supervised ruleset (ask by default, allow reads,
    /// .env needs approval, .env.example allowed). Mirrors buildOpenCodePermissionRules.
    /// </summary>
    public async Task<string> CreateSessionAsync(string? title = null, CancellationToken token = default)
    {
        var payload = new
        {
            title = title ?? ("tinycast " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm")),
            permission = SupervisedPermissionRules(),
        };
        using var response = await _http.PostAsync(_baseUrl + "/session",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), token);
        await ThrowIfAuthAsync(response, "session.create");
        var body = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
            throw new OpenCodeException("session.create", "OpenCode HTTP " + (int)response.StatusCode + ": " + Truncate(body));
        using var doc = JsonDocument.Parse(body);
        // SDK returns the session object directly.
        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("id", out var id)
            && id.GetString() is { } sessionId && !string.IsNullOrWhiteSpace(sessionId))
            return sessionId;
        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("data", out var data)
            && data.TryGetProperty("id", out var did)
            && did.GetString() is { } wrapped && !string.IsNullOrWhiteSpace(wrapped))
            return wrapped;
        throw new OpenCodeException("session.create", "OpenCode session.create returned no session payload.");
    }

    /// <summary>
    /// POST /session/:id/message (blocking, like OpenCodeTextGeneration's
    /// session.prompt). 10s submission parity is not applied to the full wait;
    /// cancellation aborts via POST /session/:id/abort by the caller.
    /// Surfaces rate_limit / 429 verbatim (no client throttle, T3 parity).
    /// </summary>
    public async Task<string> PromptAsync(
        string sessionId,
        string providerId,
        string modelId,
        string text,
        string agent = "build",
        CancellationToken token = default)
    {
        var payload = new
        {
            model = new { providerID = providerId, modelID = modelId },
            agent,
            parts = new object[] { new { type = "text", text } },
        };
        using var response = await _http.PostAsync(
            _baseUrl + "/session/" + Uri.EscapeDataString(sessionId) + "/message",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), token);
        await ThrowIfAuthAsync(response, "session.prompt");
        var body = await response.Content.ReadAsStringAsync(token);
        if (response.StatusCode == (HttpStatusCode)429)
            throw new OpenCodeException("session.prompt", "Upstream rate limit reached. Try again later. " + Truncate(body));
        if (!response.IsSuccessStatusCode)
        {
            if (body.Contains("rate_limit", StringComparison.OrdinalIgnoreCase)
                || body.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
                throw new OpenCodeException("session.prompt", "Upstream rate limit reached. Try again later. " + Truncate(body));
            throw new OpenCodeException("session.prompt", "OpenCode HTTP " + (int)response.StatusCode + ": " + Truncate(body));
        }

        var reply = ExtractAssistantText(body);
        if (string.IsNullOrWhiteSpace(reply))
            throw new OpenCodeException("session.prompt", "OpenCode response had no content.");
        return reply.Trim();
    }

    public async Task AbortAsync(string sessionId, CancellationToken token = default)
    {
        using var response = await _http.PostAsync(
            _baseUrl + "/session/" + Uri.EscapeDataString(sessionId) + "/abort",
            new StringContent("{}", Encoding.UTF8, "application/json"), token);
        await ThrowIfAuthAsync(response, "session.abort");
    }

    /// <summary>
    /// GET /event SSE stream (server.connected first, then bus events).
    /// Caller cancels to stop (mirrors T3's event.subscribe pump).
    /// </summary>
    public async IAsyncEnumerable<string> SubscribeEventsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + "/event");
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        await ThrowIfAuthAsync(response, "event.subscribe");
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (!token.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(token);
            if (line is null)
                yield break;
            if (!string.IsNullOrWhiteSpace(line))
                yield return line;
        }
    }

    static object[] SupervisedPermissionRules() =>
    [
        new { permission = "*", pattern = "*", action = "ask" },
        new { permission = "read", pattern = "*", action = "allow" },
        new { permission = "read", pattern = "*.env", action = "ask" },
        new { permission = "read", pattern = "*.env.*", action = "ask" },
        new { permission = "read", pattern = "*.env.example", action = "allow" },
        new { permission = "glob", pattern = "*", action = "allow" },
        new { permission = "grep", pattern = "*", action = "allow" },
        new { permission = "lsp", pattern = "*", action = "allow" },
        new { permission = "skill", pattern = "*", action = "allow" },
        new { permission = "todowrite", pattern = "*", action = "allow" },
        new { permission = "bash", pattern = "*", action = "ask" },
        new { permission = "edit", pattern = "*", action = "ask" },
        new { permission = "webfetch", pattern = "*", action = "ask" },
        new { permission = "websearch", pattern = "*", action = "ask" },
    ];

    static string ExtractAssistantText(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            // { info, parts: [{type:"text", text}] }
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("parts", out var parts))
                return JoinTextParts(parts);
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data))
            {
                if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("parts", out var dp))
                    return JoinTextParts(dp);
                if (data.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var entry in data.EnumerateArray())
                    {
                        if (entry.TryGetProperty("parts", out var ep))
                            sb.Append(JoinTextParts(ep));
                    }

                    return sb.ToString();
                }
            }

            return "";
        }
        catch (JsonException)
        {
            return "";
        }
    }

    static string JoinTextParts(JsonElement parts)
    {
        if (parts.ValueKind != JsonValueKind.Array)
            return "";
        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.ValueKind != JsonValueKind.Object)
                continue;
            var type = part.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (!string.Equals(type, "text", StringComparison.OrdinalIgnoreCase))
                continue;
            if (part.TryGetProperty("text", out var text) && text.GetString() is { } s)
                sb.Append(s);
        }

        return sb.ToString();
    }

    static async Task ThrowIfAuthAsync(HttpResponseMessage response, string operation)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            var body = "";
            try { body = await response.Content.ReadAsStringAsync(); } catch (Exception) { }
            throw new OpenCodeException(operation,
                "OpenCode server rejected authentication. Check the server URL and password. " + Truncate(body));
        }
    }

    static string Truncate(string text) => text.Length <= 300 ? text : text[..300] + "…";
}
