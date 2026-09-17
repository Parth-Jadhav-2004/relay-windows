using System.Runtime.InteropServices;
using System.Text;
using Tinycast.Features.FileSearch;

namespace Tinycast.Platform;

internal static class MenuProbe
{
    [DllImport("user32.dll")] static extern IntPtr GetMenu(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int GetMenuItemCount(IntPtr hMenu);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetMenuString(IntPtr hMenu, uint uIDItem, StringBuilder lpString, int nMaxCount, uint uFlag);
    [DllImport("user32.dll")] static extern IntPtr GetSubMenu(IntPtr hMenu, int nPos);
    [DllImport("user32.dll")] static extern uint GetMenuItemID(IntPtr hMenu, int nPos);
    const uint MfByPosition = 0x00000400;

    public static IReadOnlyList<MenuSearchItem> Items(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return [];
        var menu = GetMenu(hwnd);
        if (menu == IntPtr.Zero)
            return [];
        return Walk(menu, "", hwnd);
    }

    public static IReadOnlyList<MenuSearchItem> ForegroundItems() =>
        Items(NativeMethods.GetForegroundWindow());

    static List<MenuSearchItem> Walk(IntPtr menu, string prefix, IntPtr hwnd)
    {
        var items = new List<MenuSearchItem>();
        var count = GetMenuItemCount(menu);
        for (var i = 0; i < count; i++)
        {
            var buffer = new StringBuilder(256);
            if (GetMenuString(menu, (uint)i, buffer, buffer.Capacity, MfByPosition) <= 0)
                continue;
            var label = buffer.ToString().Replace("&", "", StringComparison.Ordinal);
            if (label.Length == 0)
                continue;
            var path = string.IsNullOrEmpty(prefix) ? label : prefix + " › " + label;
            var sub = GetSubMenu(menu, i);
            if (sub != IntPtr.Zero)
                items.AddRange(Walk(sub, path, hwnd));
            else
            {
                var id = GetMenuItemID(menu, i);
                var tab = label.IndexOf('\t');
                var shortcut = tab >= 0 ? label[(tab + 1)..] : "";
                var title = tab >= 0 ? label[..tab] : path;
                items.Add(new MenuSearchItem(string.IsNullOrEmpty(prefix) ? title : prefix + " › " + title, shortcut, id, hwnd));
            }
        }

        return items;
    }
}

internal static class Recycle
{
    const uint FoDelete = 0x0003;
    const uint FofAllowUndo = 0x0040;
    const uint FofNoConfirmation = 0x0010;
    const uint FofSilent = 0x0004;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct ShFileOp
    {
        public IntPtr Hwnd;
        public uint Func;
        public IntPtr From;
        public IntPtr To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool AnyOperationsAborted;
        public IntPtr NameMappings;
        public IntPtr ProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHFileOperation(ref ShFileOp lpFileOp);

    public static void Send(string path)
    {
        var bytes = Encoding.Unicode.GetBytes(path + "\0\0");
        var from = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, from, bytes.Length);
            var op = new ShFileOp
            {
                Func = FoDelete,
                From = from,
                Flags = (ushort)(FofAllowUndo | FofNoConfirmation | FofSilent),
            };
            SHFileOperation(ref op);
        }
        finally
        {
            Marshal.FreeHGlobal(from);
        }
    }
}
