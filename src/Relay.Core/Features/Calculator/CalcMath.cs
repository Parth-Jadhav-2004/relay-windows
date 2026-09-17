namespace Relay.Features.Calculator;

public static class CalcMath
{
    public static readonly IReadOnlyDictionary<string, Func<double, double>> Functions =
        new Dictionary<string, Func<double, double>>(StringComparer.OrdinalIgnoreCase)
        {
            ["sqrt"] = Math.Sqrt,
            ["log"] = Math.Log10,
            ["ln"] = Math.Log,
            ["sin"] = Math.Sin,
            ["cos"] = Math.Cos,
            ["tan"] = Math.Tan,
            ["abs"] = Math.Abs,
            ["floor"] = Math.Floor,
            ["ceil"] = Math.Ceiling,
            ["round"] = Math.Round,
            ["cot"] = x => 1 / Math.Tan(x),
            ["sec"] = x => 1 / Math.Cos(x),
            ["csc"] = x => 1 / Math.Sin(x),
            ["asin"] = Math.Asin,
            ["acos"] = Math.Acos,
            ["atan"] = Math.Atan,
            ["arcsin"] = Math.Asin,
            ["arccos"] = Math.Acos,
            ["arctan"] = Math.Atan,
            ["sinh"] = Math.Sinh,
            ["cosh"] = Math.Cosh,
            ["tanh"] = Math.Tanh,
            ["asinh"] = Math.Asinh,
            ["acosh"] = Math.Acosh,
            ["atanh"] = Math.Atanh,
            ["cbrt"] = Math.Cbrt,
            ["exp"] = Math.Exp,
            ["log2"] = Math.Log2,
            ["sign"] = x => x > 0 ? 1 : x < 0 ? -1 : 0,
            ["trunc"] = Math.Truncate,
        };

    public static readonly IReadOnlyDictionary<string, double> Constants =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["pi"] = Math.PI,
            ["π"] = Math.PI,
            ["e"] = Math.E,
            ["tau"] = Math.Tau,
            ["τ"] = Math.Tau,
            ["phi"] = (1 + Math.Sqrt(5)) / 2,
        };

    public static readonly HashSet<string> MultipleArguments = new(StringComparer.OrdinalIgnoreCase)
    {
        "hypot", "round", "log", "gcd", "lcm", "atan2", "pow", "root", "fmod",
        "min", "max", "sum", "avg", "mean", "average",
    };

    public static readonly HashSet<string> MeasurementFns = new(StringComparer.OrdinalIgnoreCase)
    {
        "hypot", "round", "min", "max", "sum", "avg", "mean", "average",
    };

    public static bool IsFunction(string name) =>
        Functions.ContainsKey(name) || MultipleArguments.Contains(name);

    public static double? Factorial(double value)
    {
        if (value < 0 || value != Math.Round(value) || value > 170)
            return null;
        var result = 1.0;
        for (var n = 2; n <= value; n++)
            result *= n;
        return result;
    }

    public static double? Evaluate(string name, IReadOnlyList<double> values)
    {
        if (values.Count == 0 || values.Any(v => !double.IsFinite(v)))
            return null;
        var first = values[0];
        double result;
        switch (name.ToLowerInvariant())
        {
            case "min":
                result = values.Min();
                break;
            case "max":
                result = values.Max();
                break;
            case "sum":
                result = values.Sum();
                break;
            case "avg":
            case "mean":
            case "average":
                result = values.Average();
                break;
            case "hypot":
                result = Math.Sqrt(values.Sum(v => v * v));
                break;
            case "gcd":
            case "lcm":
                return IntegerReduce(name, values);
            default:
                if (values.Count == 1 && Functions.TryGetValue(name, out var fn))
                {
                    result = fn(first);
                    break;
                }

                if (values.Count != 2)
                    return null;
                var second = values[1];
                switch (name.ToLowerInvariant())
                {
                    case "round":
                        if (second != Math.Truncate(second) || second is < -308 or > 308)
                            return null;
                        var factor = Math.Pow(10, second);
                        var scaled = first * factor;
                        result = double.IsFinite(scaled) ? Math.Round(scaled) / factor : first;
                        break;
                    case "log":
                        if (first <= 0 || second <= 0 || second == 1)
                            return null;
                        result = Math.Log(first) / Math.Log(second);
                        break;
                    case "atan2":
                        result = Math.Atan2(first, second);
                        break;
                    case "pow":
                        result = Math.Pow(first, second);
                        break;
                    case "root":
                        if (second == 0)
                            return null;
                        result = first < 0 && second % 2 != 0 && second == Math.Round(second)
                            ? -Math.Pow(-first, 1 / second)
                            : Math.Pow(first, 1 / second);
                        break;
                    case "fmod":
                        result = first - second * Math.Truncate(first / second);
                        break;
                    default:
                        return null;
                }

                break;
        }

        return double.IsFinite(result) ? result : null;
    }

    static double? IntegerReduce(string name, IReadOnlyList<double> values)
    {
        long acc = name.Equals("gcd", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        foreach (var value in values)
        {
            if (ExactInteger(value) is not { } integer)
                return null;
            var positive = Math.Abs(integer);
            var a = acc;
            var b = positive;
            while (b != 0)
                (a, b) = (b, a % b);
            if (name.Equals("gcd", StringComparison.OrdinalIgnoreCase))
                acc = a;
            else if (a == 0)
                acc = 0;
            else
            {
                try { acc = checked(acc / a * positive); }
                catch (OverflowException) { return null; }
            }
        }

        var result = (double)Math.Abs(acc);
        return result < CalcFormatter.MaxExactInteger ? result : null;
    }

    public static long? ExactInteger(double value)
    {
        if (Math.Abs(value) >= CalcFormatter.MaxExactInteger || value != Math.Round(value))
            return null;
        return (long)value;
    }

    public static double? Bitwise(string op, double left, double right = 0)
    {
        if (ExactInteger(left) is not { } lhs)
            return null;
        long result;
        switch (op)
        {
            case "~":
                result = ~lhs;
                break;
            default:
                if (ExactInteger(right) is not { } rhs)
                    return null;
                if (op is "<<" or ">>")
                {
                    if (rhs is < 0 or > 63)
                        return null;
                    result = op == "<<" ? lhs << (int)rhs : lhs >> (int)rhs;
                    if (op is "<<" && result >> (int)rhs != lhs)
                        return null;
                    break;
                }

                result = op switch
                {
                    "&" => lhs & rhs,
                    "|" => lhs | rhs,
                    "xor" or "⊻" => lhs ^ rhs,
                    _ => 0,
                };
                break;
        }

        var value = (double)result;
        return Math.Abs(value) < CalcFormatter.MaxExactInteger ? value : null;
    }
}
