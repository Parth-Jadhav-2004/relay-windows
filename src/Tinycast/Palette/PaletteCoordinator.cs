using Tinycast.Palette;

namespace Tinycast;

public sealed class PaletteCoordinator
{
    readonly AppCore _core;

    public PaletteCoordinator(AppCore core) => _core = core;

    public bool IsVisible => _core.PaletteWindow?.IsPaletteVisible == true;

    public bool IsShowing(PaletteMode mode) => IsVisible && _core.Palette.Mode == mode;

    public void TogglePalette()
    {
        if (IsVisible)
            HidePalette();
        else
            ShowPalette(PaletteMode.Launcher);
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
        _core.FileSearchCoordinator.Reset();
        _core.PaletteWindow?.HidePalette(restoreFocus);
        _core.Palette.Prepare(PaletteMode.Launcher);
    }

    public void HandleEscape()
    {
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
