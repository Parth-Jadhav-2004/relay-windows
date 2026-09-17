using System.Globalization;
using System.Text;

namespace Relay.Features.Calculator;

public static class CalcFormatter
{
    public const double MaxExactInteger = 9_007_199_254_740_992.0;

    public static string Expression(string query)
    {
        var compact = string.Join(" ", query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return compact.Replace("*", "×").Replace("/", "÷").Replace("->", "→");
    }

    /// <summary>Left-column echo: unit symbols, ×/÷, `amount code` money.</summary>
    public static string FromTokens(IReadOnlyList<CalcToken> tokens)
    {
        var parts = new List<string>();
        var attach = true;

        void Add(string piece, bool glued = false)
        {
            if ((attach || glued) && parts.Count > 0)
                parts[^1] += piece;
            else
                parts.Add(piece);
            attach = false;
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            switch (token.Kind)
            {
                case CalcTokenKind.Number or CalcTokenKind.Compact or CalcTokenKind.Radix:
                    Add(CopyText(token.Number));
                    break;
                case CalcTokenKind.Ident:
                    Add(CalcUnits.Find(token.Text)?.Symbol
                        ?? CalcCurrency.Find(token.Text)?.Code
                        ?? token.Text);
                    attach = i + 1 < tokens.Count && tokens[i + 1].Kind == CalcTokenKind.LParen;
                    break;
                case CalcTokenKind.LParen:
                    Add("(");
                    attach = true;
                    break;
                case CalcTokenKind.RParen:
                    Add(")", glued: true);
                    break;
                case CalcTokenKind.Operator when token.Text is "%":
                    Add("%", glued: true);
                    break;
                case CalcTokenKind.Operator when token.Text is "!":
                    Add("!", glued: true);
                    break;
                case CalcTokenKind.Operator when token.Text is "*":
                    Add("×");
                    break;
                case CalcTokenKind.Operator when token.Text is "/":
                    Add("÷");
                    break;
                case CalcTokenKind.Operator when token.Text is "->" or "to":
                    Add("→");
                    break;
                case CalcTokenKind.Operator:
                    Add(token.Text);
                    if (token.Text is "+" or "-" && IsSign(tokens, i))
                        attach = true;
                    break;
                case CalcTokenKind.Comma:
                    Add(",", glued: true);
                    break;
            }
        }

        return parts.Count == 0 ? "" : string.Join(" ", parts);
    }

    static bool IsSign(IReadOnlyList<CalcToken> tokens, int index)
    {
        if (index <= 0)
            return true;
        var previous = tokens[index - 1];
        if (previous.Kind == CalcTokenKind.RParen)
            return false;
        if (previous.Kind == CalcTokenKind.Operator)
            return previous.Text is not "%" and not "!";
        if (previous.Kind == CalcTokenKind.Ident)
            return CalcUnits.Find(previous.Text) is null
                && CalcCurrency.Find(previous.Text) is null
                && !CalcMath.Constants.ContainsKey(previous.Text);
        return false;
    }

    public static string Display(double value) => Grouped(CopyText(value));

    public static string CopyText(double value)
    {
        var v = value == 0 ? 0 : value;
        if (double.IsNaN(v) || double.IsInfinity(v))
            return v.ToString(CultureInfo.InvariantCulture);
        if (Math.Abs(v - Math.Round(v)) < 1e-12 && Math.Abs(v) <= MaxExactInteger)
            return ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture);
        var text = v.ToString("G10", CultureInfo.InvariantCulture);
        return text;
    }

    public static string Currency(double value)
    {
        var magnitude = Math.Abs(value);
        if (magnitude < 1e-9)
            return "0.00";
        if (magnitude >= 0.01)
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        var digits = 3 - (int)Math.Floor(Math.Log10(magnitude));
        var text = value.ToString("F" + Math.Clamp(digits, 2, 12), CultureInfo.InvariantCulture);
        while (text.Contains('.') && text.EndsWith('0'))
            text = text[..^1];
        return text;
    }

    public static string CompoundFeetInches(double feet)
    {
        var sign = feet < 0 ? "-" : "";
        var magnitude = Math.Abs(feet);
        var wholeFeet = Math.Truncate(magnitude);
        var inches = Math.Round((magnitude - wholeFeet) * 12);
        if (inches >= 12)
        {
            wholeFeet += Math.Truncate(inches / 12);
            inches %= 12;
        }
        else if (inches <= -12)
        {
            wholeFeet += Math.Truncate(inches / 12);
            inches %= 12;
        }

        var feetPart = wholeFeet == 0
            ? ""
            : sign + Display(wholeFeet) + (wholeFeet == 1 ? " foot" : " feet");
        if (inches == 0)
            return feetPart.Length == 0 ? sign + "0 inches" : feetPart;
        var inchText = Display(inches);
        var inchPart = inchText + (inchText == "1" ? " inch" : " inches");
        if (feetPart.Length == 0)
            return sign + inchPart;
        return feetPart + " " + inchPart;
    }

    public static string Timespan(double seconds)
    {
        if (!double.IsFinite(seconds))
            return Display(seconds);
        var sign = seconds < 0 ? "-" : "";
        var remainder = Math.Round(Math.Abs(seconds));
        var parts = new List<string>();
        foreach (var (step, symbol) in TimespanSteps)
        {
            if (remainder < step)
                continue;
            var count = Math.Truncate(remainder / step);
            remainder -= count * step;
            parts.Add(Grouped(count.ToString("0", CultureInfo.InvariantCulture)) + " " + symbol);
        }

        if (parts.Count == 0)
            return Display(seconds) + " s";
        return sign + string.Join(" ", parts);
    }

    static readonly (double Seconds, string Symbol)[] TimespanSteps =
    [
        (604800, "wk"), (86400, "day"), (3600, "hr"), (60, "min"), (1, "s"),
    ];

    public static string Grouped(string text)
    {
        if (text.Contains('e', StringComparison.OrdinalIgnoreCase))
            return text;
        var sign = text.StartsWith('-') ? 1 : 0;
        var dot = text.IndexOf('.');
        var integerLength = dot < 0 ? text.Length : dot;
        if (integerLength - sign <= 3)
            return text;
        var output = new StringBuilder(text.Length + integerLength / 3);
        for (var i = 0; i < text.Length; i++)
        {
            if (i > sign && i < integerLength && (integerLength - i) % 3 == 0)
                output.Append(',');
            output.Append(text[i]);
        }

        return output.ToString();
    }
}
