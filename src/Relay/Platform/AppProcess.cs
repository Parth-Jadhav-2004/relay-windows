using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Relay.Features.Launcher;

namespace Relay.Platform;

internal enum AppProcessResult
{
    Restarted,
    Closed,
    NotRunning,
    UnsupportedIdentity,
    IdentityUnavailable,
    Busy,
    Cancelled,
    TimedOut,
    Failed,
}

internal sealed class AppProcess(Action<AppEntry> launch) : IDisposable
{
    readonly object _gate = new();
    readonly Dictionary<string, CancellationTokenSource> _pending = new(StringComparer.OrdinalIgnoreCase);
    bool _disposed;

    public Task<AppProcessResult> QuitAsync(AppEntry app, CancellationToken cancellationToken = default) =>
        RunAsync(app, false, cancellationToken, TimeSpan.FromSeconds(5));

    public Task<AppProcessResult> RestartAsync(
        AppEntry app, CancellationToken cancellationToken = default, TimeSpan? timeout = null) =>
        RunAsync(app, true, cancellationToken, timeout ?? TimeSpan.FromSeconds(5));

    Task<AppProcessResult> RunAsync(AppEntry app, bool restart, CancellationToken cancellationToken, TimeSpan timeout)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(AppProcessResult.Cancelled);
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1)
            return Task.FromResult(AppProcessResult.TimedOut);
        var path = ResolveExecutable(app);
        if (path is null)
            return Task.FromResult(AppProcessResult.UnsupportedIdentity);

        lock (_gate)
        {
            if (_disposed)
                return Task.FromResult(AppProcessResult.Cancelled);
            if (_pending.TryGetValue(path, out var pending))
            {
                if (!restart)
                    pending.Cancel();
                return Task.FromResult(AppProcessResult.Busy);
            }

            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _pending.Add(path, cancellation);
            return CloseAsync(app, path, restart, cancellation, timeout);
        }
    }

    async Task<AppProcessResult> CloseAsync(
        AppEntry app, string path, bool restart, CancellationTokenSource cancellation, TimeSpan timeout)
    {
        var processes = new List<Process>();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
        deadline.CancelAfter(timeout);
        try
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (!Matching(path, processes))
                return AppProcessResult.IdentityUnavailable;
            if (processes.Count == 0)
                return AppProcessResult.NotRunning;

            foreach (var process in processes)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (!process.HasExited)
                {
                    var window = process.MainWindowHandle;
                    if (window != IntPtr.Zero && IsWindowEnabled(window))
                    {
                        GetWindowThreadProcessId(window, out var owner);
                        if (owner != process.Id || process.HasExited || !PostMessage(window, 0x0010, IntPtr.Zero, IntPtr.Zero))
                            return AppProcessResult.Failed;
                    }
                }
            }

            await Task.WhenAll(processes.Select(process => process.WaitForExitAsync(deadline.Token)));
            deadline.Token.ThrowIfCancellationRequested();
            if (!restart)
                return AppProcessResult.Closed;

            var remaining = new List<Process>();
            try
            {
                if (!Matching(path, remaining))
                    return AppProcessResult.IdentityUnavailable;
                if (remaining.Count != 0)
                    return AppProcessResult.Busy;
            }
            finally
            {
                foreach (var process in remaining)
                    process.Dispose();
            }

            if (!string.Equals(ResolveExecutable(app), path, StringComparison.OrdinalIgnoreCase))
                return AppProcessResult.IdentityUnavailable;

            lock (_gate)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (_disposed)
                    return AppProcessResult.Cancelled;
                launch(app);
                return AppProcessResult.Restarted;
            }
        }
        catch (OperationCanceledException)
        {
            return cancellation.IsCancellationRequested ? AppProcessResult.Cancelled : AppProcessResult.TimedOut;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return AppProcessResult.Failed;
        }
        finally
        {
            foreach (var process in processes)
                process.Dispose();
            lock (_gate)
            {
                _pending.Remove(path);
                cancellation.Dispose();
            }
        }
    }

    static bool Matching(string path, List<Process> matches)
    {
        var candidates = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path));
        var certain = true;
        foreach (var process in candidates)
        {
            var retained = false;
            try
            {
                _ = process.SafeHandle;
                if (process.HasExited)
                    continue;
                var executable = process.MainModule?.FileName;
                if (executable is null)
                {
                    certain = false;
                    continue;
                }
                if (!string.Equals(Path.GetFullPath(executable), path, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (process.Id == Environment.ProcessId)
                {
                    certain = false;
                    continue;
                }
                matches.Add(process);
                retained = true;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
            {
                certain = false;
            }
            finally
            {
                if (!retained)
                    process.Dispose();
            }
        }
        return certain;
    }

    internal static string? ResolveExecutable(AppEntry app)
    {
        if (app.Kind != AppEntryKind.Application || string.IsNullOrWhiteSpace(app.Path))
            return null;
        try
        {
            if (!Path.IsPathFullyQualified(app.Path))
                return null;
            var path = Path.GetFullPath(app.Path);
            if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                path = ShortcutTarget(path);
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)
                || !Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                return null;
            path = Path.GetFullPath(path);
            return File.Exists(path) ? path : null;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or COMException)
        {
            return null;
        }
    }

    static string? ShortcutTarget(string path)
    {
        var type = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"));
        if (type is null)
            return null;
        var link = Activator.CreateInstance(type);
        if (link is null)
            return null;
        try
        {
            ((IPersistFile)link).Load(path, 0);
            var target = new StringBuilder(32768);
            ((IShellLinkW)link).GetPath(target, target.Capacity, IntPtr.Zero, 4);
            return Environment.ExpandEnvironmentVariables(target.ToString());
        }
        finally
        {
            Marshal.FinalReleaseComObject(link);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var cancellation in _pending.Values.ToArray())
                cancellation.Cancel();
        }
    }

    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsWindowEnabled(IntPtr window);

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int capacity, IntPtr findData, uint flags);
    }
}
