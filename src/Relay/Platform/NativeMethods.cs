using System.Runtime.InteropServices;

namespace Relay.Platform;

internal static partial class NativeMethods
{
    public const int GwlWndProc = -4;
    public const uint WmHotkey = 0x0312;
    public const uint WmApp = 0x8000;
    public const uint WmTray = WmApp + 1;
    public const uint WmResumeHotKeys = WmApp + 2;
    public const uint WmLButtonUp = 0x0202;
    public const uint WmRButtonUp = 0x0205;
    public const uint WmDestroy = 0x0002;
    public const uint WmCommand = 0x0111;
    public const uint WmNull = 0x0000;

    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
    public const uint ModNoRepeat = 0x4000;
    public const uint VkSpace = 0x20;
    public const int VkShift = 0x10;
    public const int VkControl = 0x11;

    public const uint NimAdd = 0x00000000;
    public const uint NimModify = 0x00000001;
    public const uint NimDelete = 0x00000002;
    public const uint NifMessage = 0x00000001;
    public const uint NifIcon = 0x00000002;
    public const uint NifTip = 0x00000004;

    public const uint MfString = 0x00000000;
    public const uint MfSeparator = 0x00000800;
    public const uint TpmRightButton = 0x0002;
    public const uint TpmBottomAlign = 0x0020;
    public const uint TpmRightAlign = 0x0008;

    public const int TrayShowPalette = 1;
    public const int TraySettings = 2;
    public const int TrayQuit = 3;

    public const uint ImageIcon = 1;
    public const uint LrDefaultSize = 0x0040;
    public const uint LrLoadFromFile = 0x0010;
    public const uint LrShared = 0x8000;

    public const int DwmwaSystemBackdropType = 38;
    public const int DwmsbtNone = 1;
    public const int DwmwaUseImmersiveDarkMode = 20;
    public const int DwmwaWindowCornerPreference = 33;
    public const int DwmwaBorderColor = 34;
    public const int DwmwaCaptionColor = 35;
    public const int DwmwcpDoNotRound = 1;
    public const int DwmwcpRound = 2;
    public const int DwmwcpRoundSmall = 3;
    public const uint DwmwaColorNone = 0xFFFFFFFE;
    public const int DwmBorderColorDark = 0x00101010;
    public const int DwmBorderColorLight = 0x00ECECEC;
    public const int GwlStyle = -16;
    public const int WsPopup = unchecked((int)0x80000000);
    public const int WsVisible = 0x10000000;
    public const int WsClipSiblings = 0x04000000;
    public const int WsClipChildren = 0x02000000;
    public const int WsCaption = 0x00C00000;
    public const int WsSysMenu = 0x00080000;
    public const int WsThickFrame = 0x00040000;
    public const int WsBorder = 0x00800000;
    public const int WsDlgFrame = 0x00400000;
    public const int WsExWindowEdge = 0x00000100;
    public const int WsExClientEdge = 0x00000200;
    public const int WsExDlgModalFrame = 0x00000001;
    public const int WsExLayered = 0x00080000;
    public const uint LwaAlpha = 0x00000002;
    public const int GclpHbrBackground = -10;
    public const uint SwpNoMove = 0x0002;
    public const uint SwpNoSize = 0x0001;
    public const uint SwpFrameChanged = 0x0020;

    public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint RegisterWindowMessage(string lpString);

    public static readonly uint TaskbarCreated = RegisterWindowMessage("TaskbarCreated");

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    public static extern bool AllowSetForegroundWindow(uint dwProcessId);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    public static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateSolidBrush(int color);

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")]
    public static extern IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    public delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out Point pt);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    public static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    public static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "LoadImageW", SetLastError = true)]
    public static extern IntPtr LoadImageResource(IntPtr hInst, IntPtr name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    public static extern bool LockWorkStation();

    [DllImport("user32.dll")]
    public static extern short GetKeyState(int nVirtKey);

    public static bool IsKeyDown(int virtualKey) => (GetKeyState(virtualKey) & 0x8000) != 0;

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    public const int SmCxIcon = 11;
    public const int SmCyIcon = 12;

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NotifyIconData
    {
        public int CbSize;
        public IntPtr HWnd;
        public uint UId;
        public uint UFlags;
        public uint UCallbackMessage;
        public IntPtr HIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string SzTip;
    }
}
