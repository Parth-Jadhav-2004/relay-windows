using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Tinycast.DesignSystem;
using Windows.Graphics;
using WinRT.Interop;

namespace Tinycast.Platform;

internal static class WindowChrome
{
    static int _borderColor = 0x00161616;
    static bool _dark = true;

    public static IntPtr Hwnd(Window window) => WindowNative.GetWindowHandle(window);

    public static double Scale(Window window)
    {
        var dpi = NativeMethods.GetDpiForWindow(Hwnd(window));
        return dpi == 0 ? 1 : dpi / 96.0;
    }

    public static void ApplyPaletteChrome(Window window, double cornerRadius = Theme.Radius.Panel, bool dark = true)
    {
        _dark = dark;
        _borderColor = dark ? 0x00161616 : 0x00ECECEC;
        var presenter = OverlappedPresenter.CreateForContextMenu();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false;
        presenter.IsMinimizable = false;
        presenter.IsMaximizable = false;
        window.AppWindow.SetPresenter(presenter);
        window.AppWindow.IsShownInSwitchers = false;
        window.AppWindow.Title = "Tinycast";
        window.ExtendsContentIntoTitleBar = true;
        window.SystemBackdrop = null;
        HideTitleBar(window);

        var hwnd = Hwnd(window);
        StripNonClient(hwnd);
        ApplyDwmFrame(hwnd);
        PaintHost(hwnd);

        window.AppWindow.Changed += (_, args) =>
        {
            if (args.DidSizeChange)
                ApplyRoundRegion(window, cornerRadius);
        };
        ApplyRoundRegion(window, cornerRadius);
    }

    public static void RefreshPaletteChrome(Window window, double cornerRadius, bool dark = true)
    {
        _dark = dark;
        _borderColor = dark ? 0x00161616 : 0x00ECECEC;
        window.SystemBackdrop = null;
        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.SetBorderAndTitleBar(false, false);
        HideTitleBar(window);
        var hwnd = Hwnd(window);
        StripNonClient(hwnd);
        ApplyDwmFrame(hwnd);
        PaintHost(hwnd);
        ApplyRoundRegion(window, cornerRadius);
    }

    public static void ApplyRoundRegion(Window window, double dipRadius)
    {
        var hwnd = Hwnd(window);
        var size = window.AppWindow.Size;
        if (size.Width >= 8 && size.Height >= 8)
        {
            var scale = Scale(window);
            var diameter = (int)Math.Max(2, Math.Round(dipRadius * 2.0 * scale));
            var region = NativeMethods.CreateRoundRectRgn(0, 0, size.Width + 1, size.Height + 1, diameter, diameter);
            if (region != IntPtr.Zero && NativeMethods.SetWindowRgn(hwnd, region, true) == 0)
                NativeMethods.DeleteObject(region);
        }
        ApplyDwmFrame(hwnd);
        PaintHost(hwnd);
    }

    public static void ResizeDips(Window window, double width, double height, bool client = false)
    {
        var scale = Scale(window);
        var size = new SizeInt32(
            (int)Math.Round(width * scale),
            (int)Math.Round(height * scale));
        if (client)
            window.AppWindow.ResizeClient(size);
        else
            window.AppWindow.Resize(size);
    }

    public static void PlacePalette(Window window, Tinycast.AppSettings? settings = null)
    {
        if (settings is { PaletteRememberPosition: true } && settings.PaletteLeft >= 0 && settings.PaletteTop >= 0)
        {
            window.AppWindow.Move(new PointInt32((int)Math.Round(settings.PaletteLeft), (int)Math.Round(settings.PaletteTop)));
            return;
        }

        var display = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Primary);
        var work = display.WorkArea;
        var size = window.AppWindow.Size;
        var x = work.X + (work.Width - size.Width) / 2;
        var y = work.Y + (int)(work.Height * Theme.Size.PaletteTopMarginFraction);
        window.AppWindow.Move(new PointInt32(x, y));
    }

    static void HideTitleBar(Window window)
    {
        var titleBar = window.AppWindow.TitleBar;
        titleBar.ExtendsContentIntoTitleBar = true;
        titleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = Colors.Transparent;
        titleBar.BackgroundColor = Colors.Transparent;
        titleBar.InactiveBackgroundColor = Colors.Transparent;
        titleBar.ForegroundColor = Colors.Transparent;
        try { titleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed; }
        catch (Exception) { }
    }

    static void ApplyDwmFrame(IntPtr hwnd)
    {
        var noneType = NativeMethods.DwmsbtNone;
        NativeMethods.DwmSetWindowAttribute(
            hwnd, NativeMethods.DwmwaSystemBackdropType, ref noneType, sizeof(int));
        var preference = NativeMethods.DwmwcpDoNotRound;
        NativeMethods.DwmSetWindowAttribute(
            hwnd, NativeMethods.DwmwaWindowCornerPreference, ref preference, sizeof(int));
        var darkMode = _dark ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(
            hwnd, NativeMethods.DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));
        var none = unchecked((int)NativeMethods.DwmwaColorNone);
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaCaptionColor, ref none, sizeof(int));
        var border = _borderColor;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaBorderColor, ref border, sizeof(int));
        var margins = new NativeMethods.Margins
        {
            CxLeftWidth = 0,
            CxRightWidth = 0,
            CyTopHeight = 0,
            CyBottomHeight = 0,
        };
        NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }

    static void StripNonClient(IntPtr hwnd)
    {
        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlStyle).ToInt64();
        style &= ~(NativeMethods.WsCaption | NativeMethods.WsSysMenu | NativeMethods.WsThickFrame
            | NativeMethods.WsBorder | NativeMethods.WsDlgFrame);
        style |= NativeMethods.WsPopup | NativeMethods.WsClipSiblings | NativeMethods.WsClipChildren;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlStyle, (IntPtr)style);

        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt64();
        ex &= ~(NativeMethods.WsExWindowEdge | NativeMethods.WsExClientEdge | NativeMethods.WsExDlgModalFrame | NativeMethods.WsExLayered);
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, (IntPtr)ex);

        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder
            | NativeMethods.SwpNoActivate | NativeMethods.SwpFrameChanged);
    }

    static void PaintHost(IntPtr hwnd)
    {
        var border = _borderColor;
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaBorderColor, ref border, sizeof(int));
        var none = unchecked((int)NativeMethods.DwmwaColorNone);
        NativeMethods.DwmSetWindowAttribute(hwnd, NativeMethods.DwmwaCaptionColor, ref none, sizeof(int));
        var noneType = NativeMethods.DwmsbtNone;
        NativeMethods.DwmSetWindowAttribute(
            hwnd, NativeMethods.DwmwaSystemBackdropType, ref noneType, sizeof(int));
        NativeMethods.InvalidateRect(hwnd, IntPtr.Zero, true);
    }
}
