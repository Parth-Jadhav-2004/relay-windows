using System.Runtime.InteropServices;
using System.Text;
using Relay.Features.FileSearch;

namespace Relay.Platform;

internal static class MenuProbe
{
    [DllImport("user32.dll")] static extern IntPtr GetMenu(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int GetMenuItemCount(IntPtr hMenu);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetMenuString(IntPtr hMenu, uint uIDItem, StringBuilder lpString, int nMaxCount, uint uFlag);
    [DllImport("user32.dll")] static extern IntPtr GetSubMenu(IntPtr hMenu, int nPos);
    [DllImport("user32.dll")] static extern uint GetMenuItemID(IntPtr hMenu, int nPos);
    [DllImport("user32.dll")] static extern uint GetMenuState(IntPtr hMenu, uint uId, uint uFlags);
    const uint MfByPosition = 0x00000400;
    const uint MfGrayed = 0x00000001;
    const uint MfDisabled = 0x00000002;
    const uint MfSeparator = 0x00000800;

    public static IReadOnlyList<MenuSearchItem> Items(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return [];
        var menu = GetMenu(hwnd);
        if (menu == IntPtr.Zero)
            return [];
        var items = new List<MenuSearchItem>();
        Walk(menu, Array.Empty<string>(), hwnd, items, 0);
        return MenuSnapshotPolicy.Flatten(items, dropFirstTopLevel: false);
    }

    static void Walk(IntPtr menu, IReadOnlyList<string> trail, IntPtr hwnd, List<MenuSearchItem> items, int depth)
    {
        if (!MenuSnapshotPolicy.CanEnter(depth, items.Count))
            return;
        var count = GetMenuItemCount(menu);
        var leaves = 0;
        for (var i = 0; i < count; i++)
        {
            if (items.Count >= MenuSnapshotPolicy.ItemLimit)
                return;
            var state = GetMenuState(menu, (uint)i, MfByPosition);
            var buffer = new StringBuilder(256);
            if (GetMenuString(menu, (uint)i, buffer, buffer.Capacity, MfByPosition) <= 0)
                continue;
            var raw = buffer.ToString();
            var tab = raw.IndexOf('\t');
            var shortcut = tab >= 0 ? raw[(tab + 1)..] : "";
            var label = (tab >= 0 ? raw[..tab] : raw).Replace("&", "", StringComparison.Ordinal).Trim();
            if (label.Length == 0 || (state & MfSeparator) != 0)
                continue;
            var next = trail.Append(label).ToList();
            var sub = GetSubMenu(menu, i);
            if (sub != IntPtr.Zero)
            {
                Walk(sub, next, hwnd, items, depth + 1);
                continue;
            }

            if (leaves >= MenuSnapshotPolicy.PerSubmenuLimit)
                continue;
            leaves++;
            var enabled = (state & (MfGrayed | MfDisabled)) == 0;
            items.Add(new MenuSearchItem(
                MenuSearchItem.Join(next),
                shortcut,
                GetMenuItemID(menu, i),
                hwnd,
                label,
                MenuSearchItem.Join(trail),
                enabled));
        }
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
