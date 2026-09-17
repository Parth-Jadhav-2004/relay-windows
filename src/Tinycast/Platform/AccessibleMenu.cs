using System.Runtime.InteropServices;
using Tinycast.Features.FileSearch;

namespace Tinycast.Platform;

internal static class AccessibleMenu
{
    const uint ObjIdClient = 0xFFFFFFFC;
    static readonly Guid IidIAccessible = new("618736E0-3C3D-11CF-810C-00AA00389B71");

    [DllImport("oleacc.dll")]
    static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint idObject, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object? ppvObject);

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
            Walk((dynamic)obj, "", hwnd, items, 0);
            return items;
        }
        catch (Exception)
        {
            return [];
        }
    }

    static void Walk(dynamic acc, string prefix, IntPtr hwnd, List<MenuSearchItem> items, int depth)
    {
        if (depth > 6 || items.Count > 400)
            return;
        int count;
        try { count = (int)acc.accChildCount; }
        catch (Exception) { return; }

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

            var path = string.IsNullOrEmpty(prefix) ? name : prefix + " › " + name;
            int kids;
            try { kids = (int)node.accChildCount; }
            catch (Exception) { kids = 0; }
            if (kids > 0)
                Walk(node, path, hwnd, items, depth + 1);
            else
                items.Add(new MenuSearchItem(path, "", 0, hwnd));
        }
    }
}
