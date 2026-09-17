using Tinycast.Features.Clipboard;
using Tinycast.Features.Launcher;
using Tinycast.Palette;
using Tinycast.Platform;

namespace Tinycast;

public sealed class ClipboardCoordinator
{
    readonly AppCore _core;

    public ClipboardCoordinator(AppCore core) => _core = core;

    public IReadOnlyList<PaletteRow> Rows(string query)
    {
        if (!_core.Settings.ClipboardEnabled)
            return [new PaletteRow("clip-off", "Clipboard history is off", "Enable it in Settings → Features", "\uE16D")];

        var now = DateTime.Now;
        return _core.ClipboardStore.Search(query).Select(item => ToRow(item, now)).ToList();
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
        var previous = TargetHwnd();
        if (copy)
        {
            Copy(item);
            _core.PaletteCoordinator.HidePalette(restoreFocus: true);
            _core.ShowMessage("Copied");
            return true;
        }

        _core.PaletteCoordinator.HidePalette(restoreFocus: true);
        Paste(item, previous);
        return true;
    }

    public void Copy(ClipboardItem item)
    {
        if (item.Kind == ClipboardKind.Image && item.ImagePath is not null)
        {
            _ = _core.Clipboard.CopyImageAsync(item.ImagePath);
            return;
        }

        if (item.Kind == ClipboardKind.File && item.FilePath is not null)
        {
            _ = _core.Clipboard.CopyFileAsync(item.FilePath);
            return;
        }

        _core.Clipboard.CopyText(item.Text);
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

    void Paste(ClipboardItem item, IntPtr previous)
    {
        if (item.Kind == ClipboardKind.Text)
        {
            Paster.PasteText(item.Text, previous);
            return;
        }

        if (item.Kind == ClipboardKind.File && item.FilePath is not null)
        {
            if (!File.Exists(item.FilePath))
            {
                _core.ShowMessage("That file is no longer there.", DialogTone.Danger);
                return;
            }

            _ = PasteFileAsync(item.FilePath, previous);
            return;
        }

        if (item.Kind == ClipboardKind.Image && item.ImagePath is not null)
            _ = PasteImageAsync(item.ImagePath, previous);
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
