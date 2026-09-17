using Tinycast.Features.Ai;

namespace Tinycast.Platform;

/// <summary>
/// Owns the lazy local OpenCode server shared by one provider instance.
/// Mirrors apps/server/src/provider/OpenCodeServerOwner.ts: borrow counting,
/// 30s idle TTL, drop dead servers. External servers bypass ownership.
/// Thread-safe via SemaphoreSlim.
/// </summary>
public sealed class OpenCodeServerOwner : IDisposable
{
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly string _binaryPath;
    readonly string _directory;
    readonly Func<string?> _passwordProvider;
    readonly IReadOnlyDictionary<string, string?>? _environment;

    OpenCodeServerHandle? _server;
    int _borrowers;
    CancellationTokenSource? _idleCts;
    bool _disposed;

    public OpenCodeServerOwner(
        string binaryPath,
        string directory,
        Func<string?> passwordProvider,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        _binaryPath = binaryPath;
        _directory = directory;
        _passwordProvider = passwordProvider;
        _environment = environment;
    }

    /// <summary>
    /// Borrow the shared server for the duration of <paramref name="use"/>.
    /// Spawns on demand; schedules idle close 30s after the last borrower.
    /// </summary>
    public async Task<T> WithServerAsync<T>(
        Func<OpenCodeServerHandle, CancellationToken, Task<T>> use,
        string? externalUrl = null,
        CancellationToken token = default)
    {
        if (!string.IsNullOrWhiteSpace(externalUrl))
        {
            var external = await OpenCodeServerManager.ConnectExternalAsync(
                externalUrl, _passwordProvider(), token);
            return await use(external, token);
        }

        var server = await AcquireAsync(token);
        try
        {
            return await use(server, token);
        }
        finally
        {
            Release(server);
        }
    }

    async Task<OpenCodeServerHandle> AcquireAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            _idleCts?.Cancel();
            _idleCts?.Dispose();
            _idleCts = null;

            if (_server is not null)
            {
                if (IsRunning(_server))
                {
                    _borrowers++;
                    return _server;
                }

                CloseLocked(_server);
                _server = null;
            }

            var handle = await OpenCodeServerManager.StartLocalAsync(
                _binaryPath, _directory, _passwordProvider(), _environment, token);
            _server = handle;
            _borrowers = 1;
            return handle;
        }
        finally
        {
            _gate.Release();
        }
    }

    void Release(OpenCodeServerHandle server)
    {
        _gate.Wait();
        var disposeGate = false;
        try
        {
            if (!ReferenceEquals(_server, server))
                return;
            _borrowers = Math.Max(0, _borrowers - 1);
            if (_borrowers > 0)
                return;
            if (_disposed)
            {
                if (_server is not null)
                {
                    CloseLocked(_server);
                    _server = null;
                }

                disposeGate = true;
                return;
            }
            _idleCts?.Cancel();
            _idleCts?.Dispose();
            _idleCts = new CancellationTokenSource();
            var idleToken = _idleCts.Token;
            _ = Task.Delay(OpenCodeConstants.ServerIdleTtl, idleToken).ContinueWith(t =>
            {
                if (t.IsCanceled || _disposed)
                    return;
                try { _gate.Wait(); }
                catch (ObjectDisposedException) { return; }
                try
                {
                    if (_disposed || !ReferenceEquals(_server, server) || _borrowers > 0)
                        return;
                    CloseLocked(server);
                    _server = null;
                }
                finally
                {
                    try { _gate.Release(); } catch (ObjectDisposedException) { }
                }
            }, TaskScheduler.Default);
        }
        finally
        {
            try { _gate.Release(); } catch (ObjectDisposedException) { }
            if (disposeGate)
            {
                try { _gate.Dispose(); } catch (ObjectDisposedException) { }
            }
        }
    }

    static bool IsRunning(OpenCodeServerHandle handle)
    {
        try
        {
            return handle.Process is not null && !handle.Process.HasExited;
        }
        catch (Exception)
        {
            return false;
        }
    }

    static void CloseLocked(OpenCodeServerHandle handle)
    {
        if (!handle.External)
            OpenCodeServerManager.Kill(handle.Process);
        else
            handle.Process?.Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _idleCts?.Cancel();
        try { _gate.Wait(TimeSpan.FromSeconds(2)); }
        catch (ObjectDisposedException) { return; }
        try
        {
            _idleCts?.Dispose();
            _idleCts = null;
            if (_borrowers > 0)
                return;
            if (_server is not null)
            {
                CloseLocked(_server);
                _server = null;
            }
        }
        finally
        {
            try { _gate.Release(); } catch (ObjectDisposedException) { }
            if (_borrowers == 0)
                _gate.Dispose();
        }
    }
}
