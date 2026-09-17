using Relay.Features.FileSearch;

namespace Relay.Platform;

internal static class AccessibleMenu
{
    const uint ObjIdClient = 0xFFFFFFFC;
    static readonly Guid IidIAccessible = new("618736E0-3C3D-11CF-810C-00AA00389B71");

    [System.Runtime.InteropServices.DllImport("oleacc.dll")]
    static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint idObject, ref Guid riid, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.IUnknown)] out object? ppvObject);

    public static IReadOnlyList<MenuSearchItem> Items(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return [];
        try
        {
            var iid = IidIAccessible;
            if (AccessibleObjectFromWindow(hwnd, ObjIdClient, ref iid, out var obj) != 0 || obj is null)
                return [];
            var items = new List<MenuSearchItem>();
            Walk((dynamic)obj, new List<string>(), hwnd, items, 0);
            return MenuSnapshotPolicy.Flatten(items, dropFirstTopLevel: false);
        }
        catch (Exception)
        {
            return [];
        }
    }

    public static bool Press(IntPtr hwnd, string path)
    {
        if (hwnd == IntPtr.Zero)
            return false;
        try
        {
            var iid = IidIAccessible;
            if (AccessibleObjectFromWindow(hwnd, ObjIdClient, ref iid, out var obj) != 0 || obj is null)
                return false;
            return PressNode((dynamic)obj, Array.Empty<string>(), path, 0);
        }
        catch (Exception)
        {
            return false;
        }
    }

    static void Walk(dynamic acc, IReadOnlyList<string> trail, IntPtr hwnd, List<MenuSearchItem> items, int depth)
    {
        if (!MenuSnapshotPolicy.CanEnter(depth, items.Count))
            return;
        int count;
        try { count = (int)acc.accChildCount; }
        catch (Exception) { return; }

        var leaves = 0;
        for (var i = 1; i <= count; i++)
        {
            if (items.Count >= MenuSnapshotPolicy.ItemLimit)
                return;
            object child;
            try { child = acc.accChild[i]; }
            catch (Exception) { continue; }
            if (child is int)
                continue;
            dynamic node;
            try { node = child; }
            catch (Exception) { continue; }

            string name;
            try { name = (node.accName[0] as string ?? "").Replace("&", "", StringComparison.Ordinal).Trim(); }
            catch (Exception) { name = ""; }
            if (name.Length == 0 || name == "-")
                continue;

            int kids;
            try { kids = (int)node.accChildCount; }
            catch (Exception) { kids = 0; }
            var next = trail.Append(name).ToList();
            if (kids > 0)
            {
                Walk(node, next, hwnd, items, depth + 1);
                continue;
            }

            if (leaves >= MenuSnapshotPolicy.PerSubmenuLimit)
                continue;
            leaves++;
            var enabled = true;
            try
            {
                var state = (int)node.accState[0];
                if ((state & 0x1) != 0)
                    enabled = false;
            }
            catch (Exception) { }

            items.Add(new MenuSearchItem(
                MenuSearchItem.Join(next),
                "",
                0,
                hwnd,
                name,
                MenuSearchItem.Join(trail),
                enabled));
        }
    }

    static bool PressNode(dynamic acc, IReadOnlyList<string> trail, string path, int depth)
    {
        if (depth > MenuSnapshotPolicy.MaxDepth)
            return false;
        int count;
        try { count = (int)acc.accChildCount; }
        catch (Exception) { return false; }
        for (var i = 1; i <= count; i++)
        {
            object child;
            try { child = acc.accChild[i]; }
            catch (Exception) { continue; }
            if (child is int)
                continue;
            dynamic node;
            try { node = child; }
            catch (Exception) { continue; }
            string name;
            try { name = (node.accName[0] as string ?? "").Replace("&", "", StringComparison.Ordinal).Trim(); }
            catch (Exception) { name = ""; }
            if (name.Length == 0)
                continue;
            var next = trail.Append(name).ToList();
            var joined = MenuSearchItem.Join(next);
            int kids;
            try { kids = (int)node.accChildCount; }
            catch (Exception) { kids = 0; }
            if (joined == path)
            {
                try
                {
                    node.accDoDefaultAction[0]();
                    return true;
                }
                catch (Exception)
                {
                    try { node.accDoDefaultAction(); return true; }
                    catch (Exception) { return false; }
                }
            }

            if (kids > 0 && PressNode(node, next, path, depth + 1))
                return true;
        }

        return false;
    }
}
