using Tinycast.Features.Ai;

namespace Tinycast.Platform;

public sealed class OpenCodeServerOwner : IDisposable
{
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly object _lifetime = new();
    readonly CancellationTokenSource _shutdown = new();
    readonly string _binaryPath;
    readonly string _directory;
    readonly Func<string?> _passwordProvider;
    readonly IReadOnlyDictionary<string, string?>? _environment;

    OpenCodeServerHandle? _server;
    int _borrowers;
    int _operations;
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

    public async Task<T> WithServerAsync<T>(
        Func<OpenCodeServerHandle, CancellationToken, Task<T>> use,
        string? externalUrl = null,
        CancellationToken token = default)
    {
        BeginOperation();
        try
        {
            using var acquisition = CancellationTokenSource.CreateLinkedTokenSource(token, _shutdown.Token);
            if (!string.IsNullOrWhiteSpace(externalUrl))
            {
                var external = await OpenCodeServerManager.ConnectExternalAsync(
                    externalUrl, _passwordProvider(), acquisition.Token).ConfigureAwait(false);
                acquisition.Token.ThrowIfCancellationRequested();
                return await use(external, token).ConfigureAwait(false);
            }

            var server = await AcquireAsync(acquisition.Token).ConfigureAwait(false);
            try
            {
                acquisition.Token.ThrowIfCancellationRequested();
                return await use(server, token).ConfigureAwait(false);
            }
            finally
            {
                Release(server);
            }
        }
        finally
        {
            EndOperation();
        }
    }

    void BeginOperation()
    {
        lock (_lifetime)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _operations++;
        }
    }

    void EndOperation()
    {
        lock (_lifetime)
        {
            _operations--;
            if (!_disposed || _operations != 0)
                return;
            _idleCts?.Dispose();
            _idleCts = null;
            OpenCodeServerManager.Kill(_server?.Process);
            _server = null;
            _gate.Dispose();
            _shutdown.Dispose();
        }
    }

    async Task<OpenCodeServerHandle> AcquireAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            token.ThrowIfCancellationRequested();
            _idleCts?.Cancel();
            _idleCts = null;

            if (_server is not null)
            {
                if (IsRunning(_server))
                {
                    _borrowers++;
                    return _server;
                }
                OpenCodeServerManager.Kill(_server.Process);
                _server = null;
            }

            var handle = await OpenCodeServerManager.StartLocalAsync(
                _binaryPath, _directory, _passwordProvider(), _environment, token).ConfigureAwait(false);
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
        try
        {
            if (!ReferenceEquals(_server, server))
                return;
            _borrowers--;
            if (_borrowers != 0)
                return;
            lock (_lifetime)
            {
                if (_disposed)
                {
                    OpenCodeServerManager.Kill(server.Process);
                    _server = null;
                    return;
                }
                _operations++;
            }
            _idleCts?.Cancel();
            _idleCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
            _ = CloseAfterIdleAsync(server, _idleCts);
        }
        finally
        {
            _gate.Release();
        }
    }

    async Task CloseAfterIdleAsync(OpenCodeServerHandle server, CancellationTokenSource idle)
    {
        try
        {
            await Task.Delay(OpenCodeConstants.ServerIdleTtl, idle.Token).ConfigureAwait(false);
            await _gate.WaitAsync(idle.Token).ConfigureAwait(false);
            try
            {
                if (!idle.IsCancellationRequested && ReferenceEquals(_idleCts, idle)
                    && ReferenceEquals(_server, server) && _borrowers == 0)
                {
                    OpenCodeServerManager.Kill(server.Process);
                    _server = null;
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (ReferenceEquals(_idleCts, idle))
                    _idleCts = null;
                idle.Dispose();
            }
            finally
            {
                _gate.Release();
                EndOperation();
            }
        }
    }

    static bool IsRunning(OpenCodeServerHandle handle)
    {
        try
        {
            return handle.Process is not null && !handle.Process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        lock (_lifetime)
        {
            if (_disposed)
                return;
            _disposed = true;
            _operations++;
        }
        try
        {
            _shutdown.Cancel();
        }
        finally
        {
            EndOperation();
        }
    }
}
