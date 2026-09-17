using Tinycast.Features.FileSearch;

namespace Tinycast.Platform;

internal static class UiaMenu
{
    static readonly Guid CuiAutomation = new("FF48DBA4-60EF-4201-AA87-54103EEF594E");
    const int UiaControlTypePropertyId = 30003;
    const int UiaNamePropertyId = 30005;
    const int UiaIsEnabledPropertyId = 30010;
    const int UiaControlTypeMenuItem = 50011;
    const int TreeScopeDescendants = 4;

    public static IReadOnlyList<MenuSearchItem> Items(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return [];
        try
        {
            var type = Type.GetTypeFromCLSID(CuiAutomation);
            if (type is null)
                return [];
            dynamic uia = Activator.CreateInstance(type)!;
            dynamic root = uia.ElementFromHandle(hwnd);
            if (root is null)
                return [];
            dynamic condition = uia.CreatePropertyCondition(UiaControlTypePropertyId, UiaControlTypeMenuItem);
            dynamic found = root.FindAll(TreeScopeDescendants, condition);
            var count = (int)found.Length;
            var items = new List<MenuSearchItem>();
            for (var i = 0; i < count && items.Count < MenuSnapshotPolicy.ItemLimit; i++)
            {
                dynamic element = found.GetElement(i);
                string name;
                try { name = ((string)element.CurrentName ?? "").Replace("&", "", StringComparison.Ordinal).Trim(); }
                catch (Exception) { name = ""; }
                if (name.Length == 0 || name == "-")
                    continue;
                var enabled = true;
                try { enabled = (bool)element.CurrentIsEnabled; }
                catch (Exception) { }
                var parent = ParentName(element);
                items.Add(new MenuSearchItem(
                    string.IsNullOrEmpty(parent) ? name : parent + " → " + name,
                    "",
                    0,
                    hwnd,
                    name,
                    parent,
                    enabled));
            }

            return MenuSnapshotPolicy.Flatten(items, dropFirstTopLevel: false);
        }
        catch (Exception)
        {
            return [];
        }
    }

    public static bool Press(IntPtr hwnd, string path)
    {
        foreach (var _ in Items(hwnd))
        {
        }

        try
        {
            var type = Type.GetTypeFromCLSID(CuiAutomation);
            if (type is null)
                return false;
            dynamic uia = Activator.CreateInstance(type)!;
            dynamic root = uia.ElementFromHandle(hwnd);
            dynamic condition = uia.CreatePropertyCondition(UiaControlTypePropertyId, UiaControlTypeMenuItem);
            dynamic found = root.FindAll(TreeScopeDescendants, condition);
            var count = (int)found.Length;
            for (var i = 0; i < count; i++)
            {
                dynamic element = found.GetElement(i);
                string name;
                try { name = ((string)element.CurrentName ?? "").Replace("&", "", StringComparison.Ordinal).Trim(); }
                catch (Exception) { continue; }
                var parent = ParentName(element);
                var joined = string.IsNullOrEmpty(parent) ? name : parent + " → " + name;
                if (joined != path)
                    continue;
                var patternId = 10000; // Invoke
                try
                {
                    dynamic invoke = element.GetCurrentPattern(patternId);
                    invoke.Invoke();
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }

    static string ParentName(dynamic element)
    {
        try
        {
            dynamic walker = ((dynamic)element).Current; // unused; keep parent via tree walker
            _ = walker;
        }
        catch (Exception) { }

        try
        {
            var type = Type.GetTypeFromCLSID(CuiAutomation);
            if (type is null)
                return "";
            dynamic uia = Activator.CreateInstance(type)!;
            dynamic walker = uia.ControlViewWalker;
            dynamic parent = walker.GetParentElement(element);
            var names = new List<string>();
            while (parent is not null && names.Count < 8)
            {
                string name;
                try { name = ((string)parent.CurrentName ?? "").Replace("&", "", StringComparison.Ordinal).Trim(); }
                catch (Exception) { name = ""; }
                int typeId;
                try { typeId = (int)parent.CurrentControlType; }
                catch (Exception) { typeId = 0; }
                if (name.Length > 0 && typeId is 50010 or 50009 or 50011)
                    names.Add(name);
                if (typeId is 50032 or 50033)
                    break;
                parent = walker.GetParentElement(parent);
            }

            names.Reverse();
            if (names.Count > 0 && names[^1].Equals(((string)element.CurrentName ?? "").Replace("&", "", StringComparison.Ordinal).Trim(), StringComparison.Ordinal))
                names.RemoveAt(names.Count - 1);
            return MenuSearchItem.Join(names);
        }
        catch (Exception)
        {
            return "";
        }
    }
}
