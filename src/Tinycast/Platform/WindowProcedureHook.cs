using System.Runtime.InteropServices;

namespace Tinycast.Platform;

internal sealed class WindowProcedureHook : IDisposable
{
    public delegate IntPtr? Filter(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    const uint WmNcDestroy = 0x0082;
    static readonly SubclassProc Callback = Dispatch;
    readonly IntPtr _hwnd;
    readonly uint _threadId;
    readonly nuint _subclassId;
    Filter? _filter;
    GCHandle _root;
    volatile bool _disposed;
    bool _destroying;

    public WindowProcedureHook(IntPtr hwnd, Filter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        _threadId = GetWindowThreadProcessId(hwnd, out _);
        if (_threadId == 0)
            throw new ArgumentException("A valid window handle is required.", nameof(hwnd));
        VerifyThread();
        _hwnd = hwnd;
        _filter = filter;
        _root = GCHandle.Alloc(this);
        _subclassId = (nuint)GCHandle.ToIntPtr(_root);
        try
        {
            if (!SetWindowSubclass(hwnd, Callback, _subclassId, _subclassId))
                throw new InvalidOperationException("SetWindowSubclass failed to install the window hook.");
        }
        catch
        {
            ReleaseRoot();
            throw;
        }
    }

    static IntPtr Dispatch(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, nuint subclassId, nuint referenceData)
    {
        var hook = (WindowProcedureHook)GCHandle.FromIntPtr((IntPtr)referenceData).Target!;
        return hook.WndProc(hwnd, msg, wParam, lParam);
    }

    IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WmNcDestroy)
        {
            _destroying = true;
            try
            {
                if (!RemoveWindowSubclass(hwnd, Callback, _subclassId))
                    ReportFailure("RemoveWindowSubclass failed during window destruction.");
                InvokeFilter(hwnd, msg, wParam, lParam);
                return DefSubclassProc(hwnd, msg, wParam, lParam);
            }
            finally
            {
                ReleaseRoot();
            }
        }

        var handled = InvokeFilter(hwnd, msg, wParam, lParam);
        if (_destroying)
            return handled ?? IntPtr.Zero;
        return handled ?? DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    IntPtr? InvokeFilter(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            return _filter?.Invoke(hwnd, msg, wParam, lParam);
        }
        catch (Exception exception)
        {
            ReportFailure($"WindowProcedureHook filter failed for message 0x{msg:X}: {exception.GetType().Name}");
            return null;
        }
    }

    static void ReportFailure(string message)
    {
        try
        {
            Log.Write(message);
        }
        catch (Exception)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        VerifyThread();
        if (_destroying)
            return;
        if (!RemoveWindowSubclass(_hwnd, Callback, _subclassId))
            throw new InvalidOperationException("RemoveWindowSubclass failed; the window hook remains rooted.");
        ReleaseRoot();
    }

    void VerifyThread()
    {
        if (GetCurrentThreadId() != _threadId)
            throw new InvalidOperationException("Window hooks must be installed and removed on the window's owning thread.");
    }

    void ReleaseRoot()
    {
        _filter = null;
        if (_root.IsAllocated)
            _root.Free();
        _disposed = true;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr SubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, nuint subclassId, nuint referenceData);

    [DllImport("comctl32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc callback, nuint subclassId, nuint referenceData);

    [DllImport("comctl32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc callback, nuint subclassId);

    [DllImport("comctl32.dll", ExactSpelling = true)]
    static extern IntPtr DefSubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", ExactSpelling = true)]
    static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    static extern uint GetCurrentThreadId();
}
