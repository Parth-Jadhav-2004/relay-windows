namespace Tinycast.DesignSystem;

/// <summary>Numeric tokens from macOS Theme.swift. Colors are ramps; WinUI maps them.</summary>
public static class Theme
{
    public static class Spacing
    {
        public const double Xxs = 2;
        public const double Xs = 4;
        public const double Sm = 6;
        public const double Md = 8;
        public const double Lg = 10;
        public const double Xl = 12;
        public const double Xxl = 20;
        public const double Xxxl = 28;
        public const double SectionHeaderBottom = 4;
        public const double SectionSpacing = 12;
    }

    public static class Radius
    {
        public const double Panel = 26;
        public const double Row = 10;
        public const double Card = 10;
        public const double Dialog = 20;
        public const double MenuPanel = 16;
        public const double MenuRow = 10;
        public const double BarControl = 8;
        public const double KeyCap = 6;
        public const double Thumbnail = 6;
    }

    public static class Size
    {
        public const double PanelWidth = 750;
        public const double PanelHeight = 475;
        public const double CompactPanelWidth = 620;
        public const double CompactPanelHeight = 360;

        public static double InterfaceScale(string size) => size switch
        {
            "compact" => 0.9,
            "large" => 1.12,
            _ => 1,
        };

        public static (double Width, double Height) PalettePanel(string interfaceSize, bool compact)
        {
            var scale = InterfaceScale(interfaceSize);
            var width = (compact ? CompactPanelWidth : PanelWidth) * scale;
            var height = (compact ? CompactPanelHeight : PanelHeight) * scale;
            return (width, height);
        }
        public const double HeaderHeight = 44;
        public const double HeaderPadding = 10;
        public const double BottomBarHeight = 52;
        public const double BarButtonHeight = 28;
        public const double RowIcon = 32;
        public const double KeyCap = 18;
        public const double MenuButton = 36;
        public const double SettingsWindowWidth = 900;
        public const double SettingsWindowHeight = 700;
        public const double SettingsSidebar = 215;
        public const double SettingsSidebarScrollGutter = 16;
        public const double DialogWidth = 420;
        public const double DialogIcon = 32;
        public const double HudMaxWidth = 420;
        public const double HudEdgeOffset = 48;
        public const double PaletteTopMarginFraction = 0.18;
        public const double SearchFieldSize = 20;
        public const double CalcResult = 20;
        public const double ClipboardListWidth = 290;
        public const double ClipboardMediaHeight = 260;
        public const double ClipboardPreviewPixel = 900;
    }

    public static class Duration
    {
        public const double MessageHudSeconds = 2.4;
        public const double VolumeHudSeconds = 1.6;
        public const double EnterSeconds = 0.18;
        public const double ExitSeconds = 0.12;
        public const double HoverSeconds = 0.12;
    }

    public readonly record struct Ramp(double Dark, double Light)
    {
        public double For(bool dark) => dark ? Dark : Light;
    }

    public static class Colors
    {
        public static readonly Ramp PanelScrim = new(0.40, 0.55);
        public static readonly Ramp Selection = new(0.10, 0.09);
        public static readonly Ramp RowHover = new(0.05, 0.045);
        public static readonly Ramp Separator = new(0.10, 0.12);
        public static readonly Ramp ControlSurface = new(0.10, 0.08);
        public static readonly Ramp Border = new(0.20, 0.18);
        public static readonly Ramp TextPrimary = new(1.00, 1.00);
        public static readonly Ramp TextSecondary = new(0.60, 0.60);
        public static readonly Ramp TextTertiary = new(0.40, 0.42);
        public static readonly Ramp CardFill = new(0.05, 0.04);
        public static readonly Ramp CardStroke = new(0.10, 0.10);
        public static readonly Ramp GlassFrost = new(0.05, 0.25);
    }
}
