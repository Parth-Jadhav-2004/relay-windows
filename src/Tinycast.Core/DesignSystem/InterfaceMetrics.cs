namespace Tinycast.DesignSystem;

/// <summary>Scale applied to palette-adjacent surfaces. Settings, Onboarding, Support never scale.</summary>
public sealed class InterfaceMetrics
{
    public double Scale { get; }

    public static InterfaceMetrics Standard { get; } = new(1);
    public static InterfaceMetrics Large { get; } = new(1.12);

    InterfaceMetrics(double scale) => Scale = scale;

    public static InterfaceMetrics FromSettings(string interfaceSize) =>
        string.Equals(interfaceSize, "large", StringComparison.OrdinalIgnoreCase) ? Large : Standard;

    public double Space(double token) => Math.Round(token * Scale);
}
