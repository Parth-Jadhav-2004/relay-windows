using System.Runtime.InteropServices;

namespace Tinycast.Platform;

internal sealed class WindowProcedureHook : IDisposable
{
    public delegate IntPtr? Filter(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    readonly IntPtr _hwnd;
    readonly IntPtr _previous;
    readonly NativeMethods.WndProc _proc;
    readonly Filter _filter;
    bool _disposed;

    public WindowProcedureHook(IntPtr hwnd, Filter filter)
    {
        _hwnd = hwnd;
        _filter = filter;
        _previous = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlWndProc);
        _proc = WndProc;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlWndProc, Marshal.GetFunctionPointerForDelegate(_proc));
    }

    IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        var handled = _filter(hWnd, msg, wParam, lParam);
        return handled ?? NativeMethods.CallWindowProc(_previous, hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GwlWndProc, _previous);
        _disposed = true;
        GC.KeepAlive(_proc);
    }
}
