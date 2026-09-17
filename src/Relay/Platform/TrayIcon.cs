using System.Runtime.InteropServices;

namespace Relay.Platform;

internal sealed class TrayIcon : IDisposable
{
    readonly IntPtr _hwnd;
    NativeMethods.NotifyIconData _data;
    IntPtr _icon;
    bool _added;
    bool _sharedIcon;

    public event Action? ShowPalette;
    public event Action? OpenSettings;
    public event Action? Quit;

    public TrayIcon(IntPtr hwnd, string? iconPath)
    {
        _hwnd = hwnd;
        _icon = LoadIcon(hwnd, iconPath, out _sharedIcon);
        _data = new NativeMethods.NotifyIconData
        {
            CbSize = Marshal.SizeOf<NativeMethods.NotifyIconData>(),
            HWnd = hwnd,
            UId = 1,
            UFlags = NativeMethods.NifMessage | NativeMethods.NifIcon | NativeMethods.NifTip,
            UCallbackMessage = NativeMethods.WmTray,
            HIcon = _icon,
            SzTip = "Relay",
        };
    }

    public void SetTip(string tip)
    {
        _data.SzTip = tip.Length > 127 ? tip[..127] : tip;
        if (_added)
            NativeMethods.Shell_NotifyIcon(NativeMethods.NimModify, ref _data);
    }

    public void Add()
    {
        if (_added)
            return;
        if (!NativeMethods.Shell_NotifyIcon(NativeMethods.NimAdd, ref _data))
        {
            Log.Write("Shell_NotifyIcon add err=" + Marshal.GetLastWin32Error());
            return;
        }

        _added = true;
    }

    public void Remove()
    {
        if (!_added)
            return;
        NativeMethods.Shell_NotifyIcon(NativeMethods.NimDelete, ref _data);
        _added = false;
    }

    public bool HandleMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.TaskbarCreated)
        {
            _added = false;
            Add();
            return true;
        }

        if (msg == NativeMethods.WmTray)
        {
            var eventId = lParam.ToInt32() & 0xFFFF;
            if (eventId is (int)NativeMethods.WmLButtonUp)
            {
                ShowPalette?.Invoke();
                return true;
            }

            if (eventId is (int)NativeMethods.WmRButtonUp)
            {
                ShowContextMenu();
                return true;
            }
        }

        if (msg == NativeMethods.WmCommand)
        {
            switch (wParam.ToInt32() & 0xFFFF)
            {
                case NativeMethods.TrayShowPalette:
                    ShowPalette?.Invoke();
                    return true;
                case NativeMethods.TraySettings:
                    OpenSettings?.Invoke();
                    return true;
                case NativeMethods.TrayQuit:
                    Quit?.Invoke();
                    return true;
            }
        }

        return false;
    }

    void ShowContextMenu()
    {
        NativeMethods.GetCursorPos(out var pt);
        var menu = NativeMethods.CreatePopupMenu();
        NativeMethods.AppendMenu(menu, NativeMethods.MfString, (UIntPtr)NativeMethods.TrayShowPalette, "Show Relay");
        NativeMethods.AppendMenu(menu, NativeMethods.MfString, (UIntPtr)NativeMethods.TraySettings, "Settings");
        NativeMethods.AppendMenu(menu, NativeMethods.MfSeparator, UIntPtr.Zero, null);
        NativeMethods.AppendMenu(menu, NativeMethods.MfString, (UIntPtr)NativeMethods.TrayQuit, "Quit Relay");
        NativeMethods.SetForegroundWindow(_hwnd);
        NativeMethods.TrackPopupMenu(
            menu,
            NativeMethods.TpmRightButton | NativeMethods.TpmBottomAlign | NativeMethods.TpmRightAlign,
            pt.X,
            pt.Y,
            0,
            _hwnd,
            IntPtr.Zero);
        NativeMethods.PostMessage(_hwnd, NativeMethods.WmNull, IntPtr.Zero, IntPtr.Zero);
        NativeMethods.DestroyMenu(menu);
    }

    static IntPtr LoadIcon(IntPtr hwnd, string? path, out bool shared)
    {
        shared = false;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            var dpi = NativeMethods.GetDpiForWindow(hwnd);
            if (dpi == 0)
                dpi = 96;
            var size = Math.Max(16, (int)Math.Round(16.0 * dpi / 96.0));
            var loaded = NativeMethods.LoadImage(
                IntPtr.Zero,
                path,
                NativeMethods.ImageIcon,
                size,
                size,
                NativeMethods.LrLoadFromFile);
            if (loaded != IntPtr.Zero)
                return loaded;
        }

        shared = true;
        return NativeMethods.LoadImageResource(
            IntPtr.Zero,
            32512,
            NativeMethods.ImageIcon,
            0,
            0,
            NativeMethods.LrShared | NativeMethods.LrDefaultSize);
    }

    public void Dispose()
    {
        if (_added)
            NativeMethods.Shell_NotifyIcon(NativeMethods.NimDelete, ref _data);
        if (_icon != IntPtr.Zero && !_sharedIcon)
            NativeMethods.DestroyIcon(_icon);
        _added = false;
    }
}
