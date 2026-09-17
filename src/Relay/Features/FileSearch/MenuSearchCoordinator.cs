using Relay.Features.FileSearch;
using Relay.Palette;
using Relay.Platform;

namespace Relay;

public sealed class MenuSearchCoordinator
{
    readonly AppCore _core;
    IntPtr _frozen;
    IReadOnlyList<MenuSearchItem> _snapshot = [];
    MenuSearchTargetKind _kind = MenuSearchTargetKind.NoApplication;
    int _revision;

    public MenuSearchCoordinator(AppCore core) => _core = core;

    public void Reset()
    {
        _frozen = IntPtr.Zero;
        _snapshot = [];
        _kind = MenuSearchTargetKind.NoApplication;
        _revision++;
    }

    public void Capture()
    {
        if (_core.PaletteWindow?.IsPaletteVisible == true)
            _frozen = _core.PaletteWindow.PreviousHwnd;
        else
        {
            var front = NativeMethods.GetForegroundWindow();
            _frozen = front;
        }
        var process = Paster.ForegroundProcessPath(_frozen);
        _kind = MenuSearchTarget.Classify(process, Environment.ProcessPath, _core.Settings.NavigationExcludedApps);
        _snapshot = [];
        if (_kind != MenuSearchTargetKind.Searchable)
            return;
        var gen = ++_revision;
        var hwnd = _frozen;
        _ = Task.Run(() =>
        {
            var items = Snapshot(hwnd);
            _core.PaletteWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                if (gen != _revision)
                    return;
                _snapshot = items;
                if (items.Count == 0)
                    _kind = MenuSearchTargetKind.MenuLess;
                _core.Palette.Notify();
            });
        });
    }

    public IReadOnlyList<PaletteRow> Rows(string query)
    {
        if (!_core.Settings.NavigationEnabled)
            return [new PaletteRow("nav-off", "Navigation is off", "Enable it in Settings → Navigation", "\uE700")];
        if (_kind is not MenuSearchTargetKind.Searchable)
            return [new PaletteRow("menu-empty", MenuSearchTarget.EmptyMessage(_kind), "Menu Search never retargets after open", "\uE700")];
        if (_snapshot.Count == 0)
            return [new PaletteRow("menu-searching", "Reading menus…", "The walk never opens a menu", "\uE700")];
        var filtered = MenuSearchQuery.Filter(_snapshot, query);
        var section = string.IsNullOrWhiteSpace(query) ? null : "Results";
        var rows = filtered
            .Select((item, index) => new PaletteRow(
                "menu:" + index,
                item.LeafTitle,
                string.IsNullOrWhiteSpace(query) ? item.ParentPath : item.SearchPath,
                "\uE700",
                section ?? item.Section,
                Accessory: string.IsNullOrWhiteSpace(item.Shortcut) ? null : item.Shortcut))
            .ToList();
        if (rows.Count == 0)
            rows.Add(new PaletteRow("menu-empty", MenuSearchTarget.EmptyMessage(MenuSearchTargetKind.Searchable), "Classic menus, accessibility, and UI Automation are searched", "\uE700"));
        return rows;
    }

    public bool Activate(string id)
    {
        if (id is "menu-empty" or "menu-searching" or "nav-off" or "menu-skip")
            return true;
        if (!id.StartsWith("menu:", StringComparison.Ordinal))
            return false;
        if (!int.TryParse(id[5..], out var index) || index < 0 || index >= _snapshot.Count)
            return false;
        var item = _snapshot[index];
        if (!item.IsEligible)
            return true;
        var hwnd = _frozen == IntPtr.Zero ? new IntPtr(item.Hwnd) : _frozen;
        _core.PaletteCoordinator.HidePalette(restoreFocus: true);
        if (item.CommandId != 0)
            NativeMethods.PostMessage(hwnd, NativeMethods.WmCommand, (IntPtr)item.CommandId, IntPtr.Zero);
        else if (!AccessibleMenu.Press(hwnd, item.Path))
            UiaMenu.Press(hwnd, item.Path);
        return true;
    }

    static IReadOnlyList<MenuSearchItem> Snapshot(IntPtr hwnd)
    {
        var items = MenuProbe.Items(hwnd);
        if (items.Count == 0)
            items = AccessibleMenu.Items(hwnd);
        if (items.Count == 0)
            items = UiaMenu.Items(hwnd);
        return items;
    }
}
