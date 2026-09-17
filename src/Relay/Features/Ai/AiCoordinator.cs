using System.Text.Json;
using Relay.Features.Ai;
using Relay.Platform;

namespace Relay;

/// <summary>
/// Owns the OpenCode bridge. Created once by AppCore.Start(), disposed in Quit().
/// Views reach this coordinator, never the owner/client directly.
/// Mirrors T3's OpenCodeDriver + OpenCodeServerOwner + checkOpenCodeProviderStatus.
/// </summary>
public sealed class AiCoordinator : IDisposable
{
    readonly AppCore _core;
    OpenCodeServerOwner? _owner;
    string _ownerBinary = "";
    string? _sessionId;
    string? _sessionKey;
    bool _disposed;

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public OpenCodeSettings Settings { get; private set; } = new();
    public string SelectedModel { get; set; } = OpenCodeConstants.DefaultModelSlug;

    public IReadOnlyList<AiModelRef> Models { get; private set; } = [];
    public string Status { get; private set; } = "OpenCode provider status has not been checked in this session yet.";
    public string? Version { get; private set; }
    public bool Installed { get; private set; }
    public DateTimeOffset CheckedAt { get; private set; }
    public bool IsRefreshing { get; private set; }

    public AiCoordinator(AppCore core) => _core = core;

    public void Start()
    {
        Load();
        RecreateOwnerIfNeeded();
        // T3 parity: no probe on boot (OpenCode is off-by-default / opt-in).
        // Status is resolved on explicit Refresh, matching
        // "Refresh provider status" semantics.
    }

    void Load()
    {
        try
        {
            if (!File.Exists(AppPaths.OpenCodeFile))
                return;
            using var doc = JsonDocument.Parse(File.ReadAllText(AppPaths.OpenCodeFile));
            var root = doc.RootElement;
            if (root.TryGetProperty("binaryPath", out var bp) && bp.GetString() is { } b && !string.IsNullOrWhiteSpace(b))
                Settings.BinaryPath = b.Trim();
            if (root.TryGetProperty("serverUrl", out var su) && su.GetString() is { } s)
                Settings.ServerUrl = s.Trim();
            if (root.TryGetProperty("selectedModel", out var sm) && sm.GetString() is { } m && AiModelRef.ParseSlug(m) is not null)
                SelectedModel = m.Trim();
            if (root.TryGetProperty("customModels", out var cm) && cm.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in cm.EnumerateArray())
                {
                    var slug = item.ValueKind == JsonValueKind.String
                        ? item.GetString()
                        : item.TryGetProperty("slug", out var sl) ? sl.GetString() : null;
                    if (AiModelRef.ParseSlug(slug) is { } parsed)
                        Settings.CustomModels.Add(parsed);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Write("opencode load: " + ex.Message);
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.OpenCodeFile)!);
            var payload = new
            {
                binaryPath = Settings.BinaryPath,
                serverUrl = Settings.ServerUrl,
                selectedModel = SelectedModel,
                customModels = Settings.CustomModels.Select(m => m.Slug).ToArray(),
            };
            File.WriteAllText(AppPaths.OpenCodeFile, JsonSerializer.Serialize(payload, Json));
        }
        catch (Exception ex)
        {
            Log.Write("opencode save: " + ex.Message);
        }

        RecreateOwnerIfNeeded();
    }

    void RecreateOwnerIfNeeded()
    {
        if (_owner is not null && string.Equals(_ownerBinary, Settings.BinaryPath, StringComparison.OrdinalIgnoreCase))
            return;
        _owner?.Dispose();
        _sessionId = null;
        _sessionKey = null;
        _ownerBinary = Settings.BinaryPath;
        _owner = new OpenCodeServerOwner(
            Settings.BinaryPath,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            () => CredentialStore.Get("opencode-server-password"));
    }

    /// <summary>
    /// Explicit refresh only (T3 parity: periodic health checks do not refresh;
    /// reconnect/Refresh button does). Re-reads logins/config via provider.list.
    /// </summary>
    public async Task RefreshAsync(CancellationToken token = default)
    {
        if (IsRefreshing)
            return;
        IsRefreshing = true;
        try
        {
            CheckedAt = DateTimeOffset.UtcNow;
            if (!_core.Settings.AiEnabled)
            {
                Installed = false;
                Version = null;
                Models = [];
                Status = string.IsNullOrWhiteSpace(Settings.ServerUrl)
                    ? "OpenCode is disabled in settings."
                    : "OpenCode is disabled in settings. A server URL is configured.";
                return;
            }

            var external = Settings.ServerUrl.Trim();
            if (string.IsNullOrEmpty(external))
            {
                var version = await OpenCodeServerManager.ProbeCliVersionAsync(Settings.BinaryPath, token);
                if (string.IsNullOrWhiteSpace(version))
                {
                    Installed = false;
                    Version = null;
                    Models = [];
                    Status = "OpenCode CLI (opencode) is not installed or not on PATH. Install it, then run `opencode auth login`.";
                    return;
                }

                Installed = true;
                if (!OpenCodeInventory.IsVersionSupported(version))
                {
                    Version = version;
                    Models = [.. Settings.CustomModels];
                    Status = $"OpenCode v{version} is too old. Upgrade to v{OpenCodeConstants.MinimumVersion} or newer.";
                    return;
                }
            }
            else
            {
                Installed = true;
            }

            if (_owner is null)
                return;
            var models = await _owner.WithServerAsync(async (server, ct) =>
            {
                using var client = new OpenCodeClient(server.Url, server.ServerPassword);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                return await client.ListModelsAsync(Settings.CustomModels, timeout.Token);
            }, string.IsNullOrWhiteSpace(external) ? null : external, token);

            Models = models;
            Version ??= await ResolveVersionAsync(external, token);
            if (models.Count > 0)
            {
                var via = string.IsNullOrWhiteSpace(external) ? "OpenCode" : "the configured OpenCode server";
                Status = $"{models.Count} upstream providers connected through {via}.";
            }
            else
            {
                Status = "OpenCode did not report any connected upstream providers. Run `opencode auth login`, wait 30s for the helper to go idle, then refresh again.";
            }

            // Keep the thread's selected model even if it disappears (T3 parity).
            if (AiModelRef.ParseSlug(SelectedModel) is null && models.Count > 0)
                SelectedModel = models[0].Slug;
        }
        catch (OpenCodeException ex)
        {
            Models = [.. Settings.CustomModels];
            Status = OpenCodeInventory.ProbeError(Settings.IsExternal, Settings.ServerUrl, ex, null);
            Log.Write("opencode refresh: " + ex.Detail);
        }
        catch (Exception ex)
        {
            Models = [.. Settings.CustomModels];
            Status = OpenCodeInventory.ProbeError(Settings.IsExternal, Settings.ServerUrl, ex);
            Log.Write("opencode refresh: " + ex.Message);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    async Task<string?> ResolveVersionAsync(string external, CancellationToken token)
    {
        try
        {
            if (_owner is null)
                return Version;
            return await _owner.WithServerAsync(async (server, _) =>
            {
                await Task.CompletedTask;
                return server.Version;
            }, string.IsNullOrWhiteSpace(external) ? null : external, token);
        }
        catch (Exception)
        {
            return Version;
        }
    }

    public bool IsOpenCodeReady => _core.Settings.AiEnabled && Models.Count > 0;

    /// <summary>
    /// Chat via OpenCode. No client-side per-minute cap (T3 parity); upstream
    /// 429/rate_limit surfaces verbatim. If the selected model is rejected,
    /// pick an available one and retry once (T3 parity).
    /// </summary>
    public async Task<string> ChatAsync(string prompt, CancellationToken token = default)
    {
        if (!_core.Settings.AiEnabled)
            throw new InvalidOperationException("AI is off until you enable it in Settings.");
        if (_owner is null)
            throw new InvalidOperationException("OpenCode bridge is not started.");
        if (Models.Count == 0)
            throw new InvalidOperationException(Status);

        var slug = AiModelRef.ParseSlug(SelectedModel) ?? AiModelRef.ParseSlug(Models[0].Slug)!;
        var external = Settings.ServerUrl.Trim();

        try
        {
            return await RunPromptAsync(slug, prompt, external, token);
        }
        catch (OpenCodeException ex) when (IsModelRejected(ex) && Models.Count > 1)
        {
            var fallback = Models.FirstOrDefault(m => m.Slug != slug.Slug) ?? Models[0];
            SelectedModel = fallback.Slug;
            _sessionId = null;
            _sessionKey = null;
            try
            {
                return await RunPromptAsync(
                    AiModelRef.ParseSlug(fallback.Slug)!, prompt, external, token);
            }
            finally
            {
                Save();
            }
        }
    }

    async Task<string> RunPromptAsync(AiModelRef model, string prompt, string external, CancellationToken token)
    {
        if (_owner is null)
            throw new InvalidOperationException("OpenCode bridge is not started.");
        return await _owner.WithServerAsync(async (server, ct) =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, token);
            using var client = new OpenCodeClient(server.Url, server.ServerPassword);
            var key = server.Url + "\n" + model.Slug;
            if (string.IsNullOrEmpty(_sessionId) || _sessionKey != key)
            {
                _sessionId = await client.CreateSessionAsync("relay chat", linked.Token);
                _sessionKey = key;
            }

            try
            {
                return await client.PromptAsync(
                    _sessionId, model.ProviderId, model.ModelId, prompt,
                    OpenCodeConstants.DefaultAgent, linked.Token);
            }
            catch (OpenCodeException)
            {
                _sessionId = await client.CreateSessionAsync("relay chat", linked.Token);
                _sessionKey = key;
                return await client.PromptAsync(
                    _sessionId, model.ProviderId, model.ModelId, prompt,
                    OpenCodeConstants.DefaultAgent, linked.Token);
            }
            catch (OperationCanceledException)
            {
                try { await client.AbortAsync(_sessionId!); } catch (Exception) { }
                throw;
            }
        }, string.IsNullOrWhiteSpace(external) ? null : external, token);
    }

    static bool IsModelRejected(OpenCodeException ex) =>
        ex.Detail.Contains("model", StringComparison.OrdinalIgnoreCase)
        && (ex.Detail.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || ex.Detail.Contains("unknown", StringComparison.OrdinalIgnoreCase)
            || ex.Detail.Contains("rejected", StringComparison.OrdinalIgnoreCase)
            || ex.Detail.Contains("400", StringComparison.Ordinal)
            || ex.Detail.Contains("404", StringComparison.Ordinal));

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _owner?.Dispose();
    }
}
