using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Relay.DesignSystem;
using Windows.UI;

namespace Relay.DesignSystem;

internal static class ThemeBrushes
{
    public static bool IsDark(AppAppearance appearance, ApplicationTheme systemTheme)
    {
        return appearance switch
        {
            AppAppearance.Light => false,
            AppAppearance.Dark => true,
            _ => systemTheme == ApplicationTheme.Dark,
        };
    }

    public static Color Ink(double alpha, bool dark)
    {
        var a = ToByte(alpha);
        return dark ? Color.FromArgb(a, 255, 255, 255) : Color.FromArgb(a, 0, 0, 0);
    }

    public static SolidColorBrush InkBrush(double alpha, bool dark) =>
        new(Ink(alpha, dark));

    public static SolidColorBrush Scrim(bool dark, int transparency)
    {
        var baseAlpha = Theme.Colors.PanelScrim.For(dark);
        var alpha = Math.Clamp(baseAlpha - transparency / 100.0 * 0.25, 0.08, 0.88);
        var a = ToByte(alpha);
        var color = dark ? Color.FromArgb(a, 0, 0, 0) : Color.FromArgb(a, 255, 255, 255);
        return new SolidColorBrush(color);
    }

    public static Color PanelFill(bool dark) =>
        dark ? Color.FromArgb(255, 22, 22, 22) : Color.FromArgb(255, 236, 236, 236);

    public static AcrylicBrush PanelAcrylic(bool dark) => new()
    {
        TintColor = dark ? Color.FromArgb(255, 18, 18, 18) : Color.FromArgb(255, 242, 242, 242),
        TintOpacity = dark ? 0.52 : 0.62,
        TintLuminosityOpacity = dark ? 0.88 : 0.92,
        FallbackColor = PanelFill(dark),
    };

    public static Color ToneColor(DialogTone tone) => tone switch
    {
        DialogTone.Success => Color.FromArgb(255, 52, 199, 89),
        DialogTone.Danger => Color.FromArgb(255, 255, 69, 58),
        _ => Colors.White,
    };

    static byte ToByte(double alpha) => (byte)Math.Clamp(Math.Round(alpha * 255), 0, 255);
}
