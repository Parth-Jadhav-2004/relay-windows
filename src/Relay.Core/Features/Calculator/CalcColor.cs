using Relay.Features.Clipboard;

namespace Relay.Features.Calculator;

public static class CalcColor
{
    public static CalcResult? Evaluate(string query)
    {
        if (!ClipboardColor.TryParse(query, out var color))
            return null;
        return new CalcResult(query, color.Hex + "  " + color.Css, color.Hex, "Color", color.Css, false);
    }
}
