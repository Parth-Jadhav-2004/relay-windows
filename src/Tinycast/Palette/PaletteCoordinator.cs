using Tinycast.Palette;

namespace Tinycast;

public sealed class PaletteCoordinator
{
    readonly AppCore _core;
    DateTime _hiddenAt = DateTime.MinValue;
    PaletteMode _hiddenMode = PaletteMode.Launcher;

    public PaletteCoordinator(AppCore core) => _core = core;

    public bool IsVisible => _core.PaletteWindow?.IsPaletteVisible == true;

    public bool IsShowing(PaletteMode mode) => IsVisible && _core.Palette.Mode == mode;

    public void TogglePalette()
    {
        if (IsVisible)
        {
            HidePalette();
            return;
        }

        var seconds = _core.Settings.PalettePopToRootSeconds;
        var keep = seconds > 0 && DateTime.UtcNow - _hiddenAt < TimeSpan.FromSeconds(seconds);
        ShowPalette(keep ? _hiddenMode : PaletteMode.Launcher, restoreAnyMode: keep);
    }

    public void TogglePalette(PaletteMode mode)
    {
        if (IsShowing(mode))
            HidePalette();
        else
            ShowPalette(mode);
    }

    public void Navigate(PaletteMode mode)
    {
        if (mode == PaletteMode.FileSearch && _core.Palette.Mode != PaletteMode.FileSearch)
            _core.FileSearchCoordinator.Reset();
        if (IsVisible && _core.Palette.Mode != mode && mode != PaletteMode.Launcher)
            _core.Palette.Push(mode);
        else
            _core.Palette.Prepare(mode);
    }

    public void ShowPalette(PaletteMode mode, bool restoreAnyMode = false, string? seeding = null)
    {
        if (seeding is not null || !restoreAnyMode || _core.Palette.Mode != mode)
            Navigate(mode);
        if (seeding is not null)
        {
            _core.Palette.Query = seeding;
            _core.Palette.Notify();
        }

        _core.PaletteWindow?.ShowPalette();
    }

    public void HidePalette(bool restoreFocus = true)
    {
        _hiddenAt = DateTime.UtcNow;
        _hiddenMode = _core.Palette.Mode;
        _core.FileSearchCoordinator.Reset();
        _core.PaletteWindow?.HidePalette(restoreFocus);
        if (_core.Settings.PalettePopToRootSeconds <= 0)
            _core.Palette.Prepare(PaletteMode.Launcher);
    }

    public void HandleEscape()
    {
        if (PaletteEscape.ClearsQueryFirst(_core.Palette.Query, _core.Settings.PaletteEscapeClearsQuery))
        {
            _core.Palette.Query = "";
            _core.Palette.Selection = 0;
            _core.Palette.Notify();
            return;
        }

        if (_core.Palette.Mode == PaletteMode.FileSearch && _core.FileSearchCoordinator.HandleEscape())
            return;
        if (_core.Palette.Pop())
            return;
        HidePalette();
    }

    public void RingTab(bool backwards)
    {
        var next = PaletteTabRing.Next(_core.Palette.Mode, _core.Settings.ClipboardEnabled, _core.Settings.AiEnabled, backwards);
        if (next == _core.Palette.Mode)
            return;
        if (_core.Palette.Mode == PaletteMode.FileSearch)
            _core.FileSearchCoordinator.Reset();
        _core.Palette.PushCarryingQuery(next);
    }
}
