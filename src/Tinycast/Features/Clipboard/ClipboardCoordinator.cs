using Tinycast.Features.Clipboard;
using Tinycast.Features.Launcher;
using Tinycast.Palette;
using Tinycast.Platform;

namespace Tinycast;

public sealed class ClipboardCoordinator
{
    readonly AppCore _core;
    public ClipboardListFilter Filter { get; private set; } = ClipboardListFilter.All;

    public ClipboardCoordinator(AppCore core) => _core = core;

    public void CycleFilter()
    {
        Filter = ClipboardListFilterLogic.Next(Filter);
        _core.Palette.Notify();
    }

    public IReadOnlyList<PaletteRow> Rows(string query)
    {
        if (!_core.Settings.ClipboardEnabled)
            return [new PaletteRow("clip-off", "Clipboard history is off", "Enable it in Settings → Clipboard", "\uE16D")];

        var now = DateTime.Now;
        var searchLimit = Filter == ClipboardListFilter.All ? 80 : 2000;
        return _core.ClipboardStore.Search(query, searchLimit)
            .Where(item => ClipboardListFilterLogic.Matches(item, Filter))
            .Take(80)
            .Select(item => ToRow(item, now))
            .ToList();
    }

    public ClipboardItem? ItemFor(string id)
    {
        if (!TryParseId(id, out var clipId))
            return null;
        return _core.ClipboardStore.Get(clipId);
    }

    public bool TogglePin(string id)
    {
        if (!TryParseId(id, out var clipId))
            return false;
        _core.ClipboardStore.TogglePin(clipId);
        _core.Palette.Notify();
        return true;
    }

    public bool Delete(string id)
    {
        if (!TryParseId(id, out var clipId))
            return false;
        _core.ClipboardStore.Delete(clipId);
        _core.Palette.Notify();
        return true;
    }

    public bool Activate(string id, bool inverted = false)
    {
        var item = ItemFor(id);
        if (item is null)
            return false;

        var copy = inverted;
        if (string.Equals(_core.Settings.ClipboardDefaultAction, "copy", StringComparison.OrdinalIgnoreCase))
            copy = !inverted;
        var previous = TargetHwnd();
        if (copy)
        {
            _ = CopyActivateAsync(item);
            return true;
        }

        _ = PasteActivateAsync(item, previous);
        return true;
    }

    public bool ActivatePinned(int index)
    {
        var pinned = _core.ClipboardStore.Pinned(9);
        if (index < 0 || index >= pinned.Count)
            return false;
        return Activate("clip:" + pinned[index].Id);
    }

    public async void Copy(ClipboardItem item)
    {
        try
        {
            await CopyAsync(item);
        }
        catch (Exception ex)
        {
            Log.Write("clipboard copy: " + ex.Message);
            _core.ShowMessage(ex.Message, DialogTone.Danger);
        }
    }

    Task CopyAsync(ClipboardItem item)
    {
        if (item.Kind == ClipboardKind.Image && item.ImagePath is not null)
            return _core.Clipboard.CopyImageAsync(item.ImagePath);
        if (item.Kind == ClipboardKind.File && item.FilePath is not null)
            return _core.Clipboard.CopyFileAsync(item.FilePath);
        _core.Clipboard.CopyText(item.Text);
        return Task.CompletedTask;
    }

    public void HistoryChanged()
    {
        var queue = _core.PaletteWindow?.DispatcherQueue;
        if (queue is null)
            return;
        queue.TryEnqueue(() =>
        {
            if (_core.Palette.Mode != PaletteMode.Clipboard)
                return;
            if (string.IsNullOrWhiteSpace(_core.Palette.Query))
                _core.Palette.Selection = 0;
            _core.Palette.Notify();
        });
    }

    static PaletteRow ToRow(ClipboardItem item, DateTime now)
    {
        var thumb = ClipboardPresentation.ThumbnailPath(item);
        var fill = thumb is not null;
        if (thumb is null && item.Kind == ClipboardKind.File && item.FilePath is not null)
            thumb = ShellIcons.FromFile(item.FilePath);
        if (item.Kind == ClipboardKind.Text && ClipboardColor.TryParse(item.Text, out var color))
        {
            return new PaletteRow(
                "clip:" + item.Id,
                color.Hex,
                color.Css,
                "\uE790",
                ClipboardPresentation.Section(item, now),
                AppEntryKind.Command,
                item.Text,
                false,
                item.Text,
                PrimaryAction: "Paste",
                ShowActions: true);
        }
        return new PaletteRow(
            "clip:" + item.Id,
            ClipboardPresentation.ListTitle(item),
            null,
            ClipboardPresentation.Glyph(item),
            ClipboardPresentation.Section(item, now),
            AppEntryKind.Command,
            item.Kind == ClipboardKind.Text ? item.Text : null,
            false,
            null,
            IconPath: thumb,
            PrimaryAction: "Paste",
            ShowActions: true,
            FillIcon: fill);
    }

    static bool TryParseId(string id, out long clipId)
    {
        clipId = 0;
        return id.StartsWith("clip:", StringComparison.Ordinal) && long.TryParse(id[5..], out clipId);
    }

    async Task CopyActivateAsync(ClipboardItem item)
    {
        try
        {
            await CopyAsync(item);
            if (!_core.Settings.ClipboardKeepOpen)
                _core.PaletteCoordinator.HidePalette(restoreFocus: true);
            _core.ShowMessage("Copied");
        }
        catch (Exception ex)
        {
            Log.Write("clipboard copy: " + ex.Message);
            _core.ShowMessage(ex.Message, DialogTone.Danger);
        }
    }

    async Task PasteActivateAsync(ClipboardItem item, IntPtr previous)
    {
        try
        {
            if (!_core.Settings.ClipboardKeepOpen)
                _core.PaletteCoordinator.HidePalette(restoreFocus: true);
            await PasteAsync(item, previous);
        }
        catch (Exception ex)
        {
            Log.Write("clipboard paste: " + ex.Message);
            _core.ShowMessage(ex.Message, DialogTone.Danger);
        }
    }

    Task PasteAsync(ClipboardItem item, IntPtr previous)
    {
        if (item.Kind == ClipboardKind.Text)
        {
            Paster.PasteText(item.Text, previous);
            return Task.CompletedTask;
        }

        if (item.Kind == ClipboardKind.File && item.FilePath is not null)
        {
            if (!File.Exists(item.FilePath) && !Directory.Exists(item.FilePath))
            {
                _core.ShowMessage("That file is no longer there.", DialogTone.Danger);
                return Task.CompletedTask;
            }

            return PasteFileAsync(item.FilePath, previous);
        }

        if (item.Kind == ClipboardKind.Image && item.ImagePath is not null)
            return PasteImageAsync(item.ImagePath, previous);
        return Task.CompletedTask;
    }

    async Task PasteImageAsync(string path, IntPtr previous)
    {
        await _core.Clipboard.CopyImageAsync(path);
        Paster.SendCtrlV(previous);
    }

    async Task PasteFileAsync(string path, IntPtr previous)
    {
        await _core.Clipboard.CopyFileAsync(path);
        Paster.SendCtrlV(previous);
    }

    IntPtr TargetHwnd()
    {
        var hwnd = _core.PaletteWindow?.PreviousHwnd ?? IntPtr.Zero;
        return hwnd == IntPtr.Zero ? NativeMethods.GetForegroundWindow() : hwnd;
    }
}
