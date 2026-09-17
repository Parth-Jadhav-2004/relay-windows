using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Tinycast.DesignSystem;
using Tinycast.Platform;
using Windows.Graphics;

namespace Tinycast;

public sealed partial class MessageHudWindow : Window
{
    public MessageHudWindow()
    {
        InitializeComponent();
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        var hwnd = WindowChrome.Hwnd(this);
        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt64();
        ex |= NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow;
        ex &= ~NativeMethods.WsExAppWindow;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GwlExStyle, (IntPtr)ex);
        AppWindow.Show(false);
        WindowClip.Round(Root, 18);
    }

    public void Present(string message, DialogTone tone, AppAppearance appearance = AppAppearance.Dark)
    {
        MessageBlock.Text = message;
        Glyph.Glyph = tone switch
        {
            DialogTone.Success => "\uE73E",
            DialogTone.Danger => "\uE783",
            _ => "\uE946",
        };
        Glyph.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(ThemeBrushes.ToneColor(tone));
        var dark = ThemeBrushes.IsDark(appearance, Application.Current.RequestedTheme);
        Scrim.Background = ThemeBrushes.Scrim(dark, 0);
        MessageBlock.Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextPrimary.For(dark), dark);
        WindowChrome.ResizeDips(this, Math.Min(Theme.Size.HudMaxWidth, 320), 52);
        var display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var work = display.WorkArea;
        var size = AppWindow.Size;
        AppWindow.Move(new PointInt32(
            work.X + (work.Width - size.Width) / 2,
            work.Y + work.Height - size.Height - (int)(Theme.Size.HudEdgeOffset * WindowChrome.Scale(this))));
    }
}
