using System.Globalization;
using System.Text.RegularExpressions;

namespace Relay.Features.Calculator;

public static class CalcPercent
{
    public static CalcResult? Evaluate(string query)
    {
        query = string.Join(" ", query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var of = Regex.Match(query, @"^([+-]?\d+(?:\.\d+)?)\s*%\s*(?:of\s+)?([+-]?\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase);
        if (of.Success)
            return Number(query, Num(of, 1) / 100 * Num(of, 2), "Percent");

        var off = Regex.Match(query, @"^([+-]?\d+(?:\.\d+)?)\s*%\s*off\s+([+-]?\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase);
        if (off.Success)
            return Number(query, (1 - Num(off, 1) / 100) * Num(off, 2), "Discounted");

        var tip = Regex.Match(query, @"^([+-]?\d+(?:\.\d+)?)\s*%\s*tip\s+on\s+([+-]?\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase);
        if (tip.Success)
            return Number(query, Num(tip, 1) / 100 * Num(tip, 2), "Tip");

        var asPct = Regex.Match(query, @"^([+-]?\d+(?:\.\d+)?)\s+as\s*%\s*of\s+([+-]?\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase);
        if (asPct.Success)
            return PercentCard(query, Num(asPct, 2) == 0 ? double.NaN : 100 * Num(asPct, 1) / Num(asPct, 2), "Percentage");

        var whatPct = Regex.Match(query, @"^([+-]?\d+(?:\.\d+)?)\s+is\s+what\s*%\s*of\s+([+-]?\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase);
        if (whatPct.Success)
            return PercentCard(query, Num(whatPct, 2) == 0 ? double.NaN : 100 * Num(whatPct, 1) / Num(whatPct, 2), "Percentage");

        var ofWhat = Regex.Match(query, @"^([+-]?\d+(?:\.\d+)?)\s+is\s+([+-]?\d+(?:\.\d+)?)\s*%\s*of\s+what$", RegexOptions.IgnoreCase);
        if (ofWhat.Success)
            return Number(query, Num(ofWhat, 2) == 0 ? double.NaN : Num(ofWhat, 1) / (Num(ofWhat, 2) / 100), "Total");

        var ratio = Regex.Match(query, @"^ratio\s+of\s+(-?\d+)\s+to\s+(-?\d+)$", RegexOptions.IgnoreCase);
        if (ratio.Success)
        {
            if (!long.TryParse(ratio.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var a)
                || !long.TryParse(ratio.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var b)
                || a == long.MinValue || b == long.MinValue)
                return new CalcResult(query, "Cannot evaluate", "", "Ratio", "Result", true);
            if (a == 0 && b == 0)
                return null;
            var g = Gcd(Math.Abs(a), Math.Abs(b));
            if (g == 0)
                return null;
            var text = a / g + " : " + b / g;
            return new CalcResult(query, text, text, "Ratio", "Result", false);
        }

        var list = Regex.Match(query, @"^(average|avg|mean|sum|min|max)\s+of\s+(.+)$", RegexOptions.IgnoreCase);
        if (list.Success)
        {
            var segments = Regex.Split(list.Groups[2].Value, @"\s*(?:,|and)\s*");
            var nums = new List<double>(segments.Length);
            foreach (var segment in segments)
            {
                if (!double.TryParse(segment, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
                    || !double.IsFinite(n))
                    return new CalcResult(query, "Cannot evaluate", "", TitleOf(list.Groups[1].Value), "Result", true);
                nums.Add(n);
            }

            if (nums.Count == 0)
                return null;
            var name = list.Groups[1].Value.ToLowerInvariant();
            var value = name switch
            {
                "sum" => nums.Sum(),
                "min" => nums.Min(),
                "max" => nums.Max(),
                _ => nums.Average(),
            };
            return Number(query, value, TitleOf(name));
        }

        var round = Regex.Match(query, @"^round\s+([+-]?\d+(?:\.\d+)?)\s+to\s+nearest\s+([+-]?\d+(?:\.\d+)?)$", RegexOptions.IgnoreCase);
        if (round.Success)
        {
            var step = Num(round, 2);
            if (step == 0)
                return null;
            return Number(query, Math.Round(Num(round, 1) / step) * step, "Rounded");
        }

        return null;
    }

    static double Num(Match match, int group) =>
        double.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);

    static long Gcd(long a, long b)
    {
        while (b != 0)
            (a, b) = (b, a % b);
        return a;
    }

    static string TitleOf(string name) => name.ToLowerInvariant() switch
    {
        "sum" => "Sum",
        "min" => "Minimum",
        "max" => "Maximum",
        _ => "Average",
    };

    static CalcResult Number(string query, double value, string badge)
    {
        if (!double.IsFinite(value))
            return new CalcResult(query, "Cannot evaluate", "", badge, "Result", true);
        return new CalcResult(query, CalcFormatter.Display(value), CalcFormatter.CopyText(value), badge, "Result", false);
    }

    static CalcResult PercentCard(string query, double value, string badge)
    {
        if (!double.IsFinite(value))
            return new CalcResult(query, "Cannot evaluate", "", badge, "Result", true);
        return new CalcResult(query, CalcFormatter.Display(value) + "%", CalcFormatter.CopyText(value) + "%", badge, "Result", false);
    }
}
