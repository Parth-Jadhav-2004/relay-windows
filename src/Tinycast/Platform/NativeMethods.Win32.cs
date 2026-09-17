using System.Runtime.InteropServices;
using System.Text;

namespace Tinycast.Platform;

internal static partial class NativeMethods
{
    public const uint WmClipboardUpdate = 0x031D;
    public const uint WmKeyDown = 0x0100;
    public const uint WmSysCommand = 0x0112;
    public const int ScMonitorPower = 0xF170;
    public const int ScTaskList = 0xF130;
    public const int MonitorOff = 2;
    public const uint KeyeventfExtendedKey = 0x0001;
    public const uint KeyeventfKeyUp = 0x0002;
    public const uint KeyeventfUnicode = 0x0004;
    public const uint InputKeyboard = 1;
    public const uint SwShow = 5;
    public const uint SwRestore = 9;
    public const uint SwMinimize = 6;
    public const uint SwMaximize = 3;
    public const uint VkF11 = 0x7A;
    public const uint SwpNoZOrder = 0x0004;
    public const uint SwpNoActivate = 0x0010;
    public const uint SwpShowWindow = 0x0040;
    public const int GwlExStyle = -20;
    public const int WsExToolWindow = 0x00000080;
    public const int WsExAppWindow = 0x00040000;
    public const int WsExNoActivate = 0x08000000;
    public const uint GaRoot = 2;
    public const uint VkLeft = 0x25;
    public const int MaxPath = 260;
    public const uint VkMediaPlayPause = 0xB3;
    public const uint VkMediaNext = 0xB0;
    public const uint VkMediaPrev = 0xB1;
    public const uint VkVolumeMute = 0xAD;
    public const uint VkVolumeDown = 0xAE;
    public const uint VkVolumeUp = 0xAF;
    public const uint VkLwin = 0x5B;
    public const uint VkD = 0x44;
    public const uint VkA = 0x41;
    public const uint VkL = 0x4C;
    public const uint VkN = 0x4E;
    public const int SpiGetWorkArea = 0x0030;
    public const int SmCxVirtualScreen = 78;
    public const int MonitorDefaultToNearest = 2;
    public const int WhKeyboardLl = 13;
    public const uint WmKeyUp = 0x0101;
    public const uint WmSysKeyDown = 0x0104;
    public const uint WmSysKeyUp = 0x0105;
    public const uint LlkhfExtended = 0x01;
    public const int VkMenu = 0x12;
    public const int VkLwinKey = 0x5B;
    public const int VkRwin = 0x5C;
    public const int VkLcontrol = 0xA2;
    public const int VkRcontrol = 0xA3;
    public const int VkLmenu = 0xA4;
    public const int VkRmenu = 0xA5;
    public const int VkLshift = 0xA0;
    public const int VkRshift = 0xA1;
    public const int VkCapital = 0x14;

    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    public struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    public static extern bool SystemParametersInfo(int uiAction, int uiParam, ref Rect pvParam, int fWinIni);

    [DllImport("user32.dll")]
    public static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("powrprof.dll", SetLastError = true)]
    public static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    public static void SendUnicode(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        var inputs = new List<Input>(text.Length * 2);
        foreach (var ch in text)
        {
            inputs.Add(Key((ushort)ch, KeyeventfUnicode));
            inputs.Add(Key((ushort)ch, KeyeventfUnicode | KeyeventfKeyUp));
        }

        var payload = inputs.ToArray();
        var sent = SendInput((uint)payload.Length, payload, Marshal.SizeOf<Input>());
        if (sent != payload.Length)
            Log.Write("SendInput sent " + sent + "/" + payload.Length + " err=" + Marshal.GetLastWin32Error());
    }

    public static void SendVk(uint vk)
    {
        keybd_event((byte)vk, 0, 0, UIntPtr.Zero);
        keybd_event((byte)vk, 0, KeyeventfKeyUp, UIntPtr.Zero);
    }

    static Input Key(ushort scan, uint flags) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput { Vk = 0, Scan = scan, Flags = flags },
        },
    };
}
