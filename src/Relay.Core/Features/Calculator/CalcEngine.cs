using System.Globalization;

namespace Relay.Features.Calculator;

public sealed record CalcContext(
    DateTime Now,
    TimeZoneInfo TimeZone,
    DayOfWeek FirstDayOfWeek = DayOfWeek.Sunday,
    CurrencyRates? Rates = null,
    string? RegionCurrency = null);

public sealed record CurrencyRates(IReadOnlyDictionary<string, double> UsdPerUnit, DateTime FetchedAt, bool IncludesCrypto = false)
{
    public double? Convert(double amount, string from, string to)
    {
        from = from.ToUpperInvariant();
        to = to.ToUpperInvariant();
        if (from == to)
            return amount;
        if (!UsdPerUnit.TryGetValue(from, out var fromRate) || !UsdPerUnit.TryGetValue(to, out var toRate))
            return null;
        if (fromRate == 0 || toRate == 0)
            return null;
        return amount * (toRate / fromRate);
    }
}

public sealed record CalcResult(string Expression, string Display, string CopyText, string? SourceBadge, string? TargetBadge, bool IsError)
{
    public bool IsActionable => !IsError;
}

public static class CalcEngine
{
    public static CalcResult? Evaluate(string raw, DateTime now, CurrencyRates? rates = null, string? region = null) =>
        Evaluate(raw, new CalcContext(now, TimeZoneInfo.Local, DayOfWeek.Sunday, rates, region));

    public static CalcResult? Evaluate(string raw, CalcContext context)
    {
        try
        {
            return EvaluateCore(raw, context);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    static CalcResult? EvaluateCore(string raw, CalcContext context)
    {
        var query = raw.Trim();
        if (query.Length is 0 or > 256)
            return null;
        if (query.All(static c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z')))
            return null;
        if (CalcColor.Evaluate(query) is { } color)
            return color;

        if (CalcDateTime.Evaluate(query, context) is { } date)
            return date;
        if (CalcTimeZone.Evaluate(query, context) is { } zone)
            return zone;

        var tokens = CalcTokenizer.Tokenize(query);
        if (tokens is null || tokens.Count == 0)
            return CalcPercent.Evaluate(query);

        if (TrailingBinary(tokens) is { } prefix && prefix.Count > 0)
        {
            if (EvaluateTokens(prefix, query, context, allowLoneNumber: true) is { } partial)
                return partial;
        }

        return EvaluateTokens(tokens, query, context) ?? CalcPercent.Evaluate(query);
    }

    static CalcResult? EvaluateTokens(IReadOnlyList<CalcToken> tokens, string query, CalcContext context, bool allowLoneNumber = false)
    {
        if (tokens.Count == 1)
        {
            var only = tokens[0];
            if (only.Kind == CalcTokenKind.Radix)
            {
                var display = CalcFormatter.Grouped(CalcFormatter.CopyText(only.Number));
                return new CalcResult(query, display, ((long)only.Number).ToString(CultureInfo.InvariantCulture),
                    BaseName(only.Radix), "Decimal", false);
            }

            if (only.Kind == CalcTokenKind.Compact || (allowLoneNumber && only.IsNumber))
                return Render(CalcFormatter.Expression(query), CalcValue.Number(only.Number), "Expression", "Result");
            return null;
        }

        if (TrySimpleConversion(tokens, context.Rates, out var simple) && simple is not null)
            return simple;

        if (TryTrailingConversion(tokens, out var sourceTokens, out var targetName)
            && sourceTokens.Count > 0)
        {
            var sourceValue = CalcParser.Evaluate(sourceTokens, context.Rates, context.RegionCurrency, out var sourceError);
            if (sourceError is not null)
                return new CalcResult(query, sourceError, "", null, null, true);
            if (sourceValue is { IsFinite: true })
            {
                var converted = CalcParser.ConvertValue(sourceValue, targetName, context.Rates, context.RegionCurrency, out var convError);
                if (convError is not null)
                    return new CalcResult(query, convError, "", null, null, true);
                if (converted is { IsFinite: true })
                    return Render(
                        Echo(sourceTokens, sourceValue),
                        converted,
                        HasOperator(sourceTokens) ? "Expression" : BadgeOf(sourceValue, source: true),
                        BadgeOf(converted, source: false));
            }
        }

        var value = CalcParser.Evaluate(tokens, context.Rates, context.RegionCurrency, out var error);
        if (error is not null)
            return new CalcResult(query, error, "", null, null, true);
        if (value is { IsFinite: true })
        {
            if (!allowLoneNumber && IsBareQuantity(tokens, value) && BareConversion(value) is { } bare)
                return bare;
            if (value.Kind == CalcValueKind.Currency && tokens.All(t => t.Kind is CalcTokenKind.Number or CalcTokenKind.Ident or CalcTokenKind.Compact)
                && tokens.Count <= 3 && !tokens.Any(t => t.Kind == CalcTokenKind.Operator)
                && context.RegionCurrency is { } region
                && value.CurrencyCode is { } code
                && !string.Equals(code, region, StringComparison.OrdinalIgnoreCase))
            {
                var converted = CalcParser.ConvertMoney(value.Amount, code, region, context.Rates, out var err);
                if (converted is not null)
                    return Render(
                        CalcFormatter.Display(value.Amount) + " " + code,
                        converted, NameOfCurrency(code), NameOfCurrency(region));
                if (err is not null)
                    return new CalcResult(query, err, "", null, null, true);
            }

            return Render(
                Echo(tokens, value, query),
                value,
                HasOperator(tokens) || value.Kind is CalcValueKind.Number or CalcValueKind.Boolean
                    ? "Expression"
                    : BadgeOf(value, source: true),
                BadgeOf(value, source: false));
        }

        return null;
    }

    static string Echo(IReadOnlyList<CalcToken> tokens, CalcValue value, string? query = null)
    {
        if (IsSimpleConversionSource(tokens) && value.Kind == CalcValueKind.Quantity && value.Unit is { } unit)
            return CalcFormatter.Display(value.Amount) + " " + unit.Symbol;
        if (IsSimpleConversionSource(tokens) && value.Kind == CalcValueKind.Currency && value.CurrencyCode is { } code)
            return CalcFormatter.Display(value.Amount) + " " + code;
        var fromTokens = CalcFormatter.FromTokens(tokens);
        if (fromTokens.Length > 0)
            return fromTokens;
        return query is null ? "" : CalcFormatter.Expression(query);
    }

    static bool HasOperator(IReadOnlyList<CalcToken> tokens)
    {
        foreach (var token in tokens)
        {
            if (token.Kind is CalcTokenKind.LParen or CalcTokenKind.RParen)
                return true;
            if (token.Kind == CalcTokenKind.Operator && token.Text is not "to" and not "->" and not "in" and not "into" and not "as")
                return true;
        }

        return false;
    }

    static bool IsSimpleConversionSource(IReadOnlyList<CalcToken> tokens) =>
        tokens.Count switch
        {
            1 => tokens[0].IsIdent,
            2 => tokens[0].IsNumber && tokens[1].IsIdent || tokens[0].IsIdent && tokens[1].IsNumber,
            _ => false,
        };

    static bool TryTrailingConversion(IReadOnlyList<CalcToken> tokens, out List<CalcToken> source, out string target)
    {
        source = [];
        target = "";
        var depth = 0;
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Kind == CalcTokenKind.LParen)
                depth++;
            if (token.Kind == CalcTokenKind.RParen)
                depth--;
            if (depth != 0 || i + 1 >= tokens.Count || !IsConversionConnector(token))
                continue;

            var rest = tokens.Skip(i + 1).ToList();
            if (rest.Count == 1 && rest[0].IsIdent)
            {
                source = tokens.Take(i).ToList();
                target = rest[0].Text;
                return source.Count > 0;
            }

            var joined = string.Concat(rest.Select(t => t.Text));
            var spaced = string.Join(" ", rest.Select(t => t.Text));
            if (CalcUnits.Find(joined) is not null || CalcUnits.Find(spaced) is not null)
            {
                source = tokens.Take(i).ToList();
                target = CalcUnits.Find(joined) is not null ? joined : spaced;
                return source.Count > 0;
            }
        }

        return false;
    }

    static bool IsConversionConnector(CalcToken token) =>
        token.Kind == CalcTokenKind.Operator && token.Text is "to" or "->"
        || token.IsIdent && CalcUnits.IsConnector(token.Text);

    static bool IsBareQuantity(IReadOnlyList<CalcToken> tokens, CalcValue value) =>
        value.Kind == CalcValueKind.Quantity && tokens.Count == 2 && tokens[0].IsNumber && tokens[1].IsIdent;

    static CalcResult? BareConversion(CalcValue value)
    {
        if (value.Unit is null)
            return null;
        if (value.Unit.Category == UnitCategory.Length && value.Unit.Symbol is "m" or "cm" or "mm" or "km")
        {
            var feet = CalcUnits.Convert(value.Amount, value.Unit, CalcUnits.Find("ft")!)!.Value;
            var text = CalcFormatter.CompoundFeetInches(feet);
            return new CalcResult(
                CalcFormatter.Display(value.Amount) + " " + value.Unit.Symbol,
                text, text, value.Unit.Name, "Feet", false);
        }

        if (value.Unit.Category == UnitCategory.Time && value.Unit.Symbol is "hr" or "h")
        {
            var minutes = CalcUnits.Convert(value.Amount, value.Unit, CalcUnits.Find("min")!)!.Value;
            return Render(
                CalcFormatter.Display(value.Amount) + " " + value.Unit.Symbol,
                CalcValue.Quantity(minutes, CalcUnits.Find("min")!), value.Unit.Name, "Minutes");
        }

        if (value.Unit.Category == UnitCategory.Volume && value.Unit.Symbol is "m³" or "m3")
        {
            var liters = CalcUnits.Convert(value.Amount, value.Unit, CalcUnits.Find("L")!)!.Value;
            return Render(
                CalcFormatter.Display(value.Amount) + " " + value.Unit.Symbol,
                CalcValue.Quantity(liters, CalcUnits.Find("L")!), value.Unit.Name, "Liters");
        }

        if (value.Unit.Category == UnitCategory.Pixels && value.Unit.Symbol is "px")
        {
            var rem = CalcUnits.Convert(value.Amount, value.Unit, CalcUnits.Find("rem")!)!.Value;
            return Render(
                CalcFormatter.Display(value.Amount) + " px",
                CalcValue.Quantity(rem, CalcUnits.Find("rem")!), "Pixels", "REM");
        }

        if (value.Unit.Category == UnitCategory.Pixels && value.Unit.Symbol is "em" or "rem")
        {
            var px = CalcUnits.Convert(value.Amount, value.Unit, CalcUnits.Find("px")!)!.Value;
            return Render(
                CalcFormatter.Display(value.Amount) + " " + value.Unit.Symbol,
                CalcValue.Quantity(px, CalcUnits.Find("px")!), value.Unit.Name, "Pixels");
        }

        return null;
    }

    static bool TrySimpleConversion(IReadOnlyList<CalcToken> tokens, CurrencyRates? rates, out CalcResult? result)
    {
        result = null;
        if (tokens.Count < 3
            || !IsConversionConnector(tokens[^2])
            || !tokens[^1].IsIdent
            || !tokens[^3].IsIdent)
            return false;

        var fromName = tokens[^3].Text;
        var toName = tokens[^1].Text;
        var valueTokens = tokens.Take(tokens.Count - 3).ToList();
        double amount = 1;
        if (valueTokens.Count == 1 && valueTokens[0].IsNumber)
            amount = valueTokens[0].Number;
        else if (valueTokens.Count > 1)
        {
            var parsed = CalcParser.Evaluate(valueTokens, rates, null, out _);
            if (parsed is not { Kind: CalcValueKind.Number, IsFinite: true })
                return false;
            amount = parsed.Amount;
        }
        else if (valueTokens.Count == 1)
            return false;

        CalcValue value;
        if (CalcUnits.Find(fromName) is { } unit)
            value = CalcValue.Quantity(amount, unit);
        else if (CalcCurrency.Find(fromName) is { } currency)
            value = CalcValue.Money(amount, currency.Code);
        else
            return false;

        var converted = CalcParser.ConvertValue(value, toName, rates, null, out var error);
        if (error is not null)
        {
            result = new CalcResult(fromName + " → " + toName, error, "", null, null, true);
            return true;
        }

        if (converted is null)
            return false;
        result = Render(
            CalcFormatter.Display(amount) + " " + (value.Unit?.Symbol ?? value.CurrencyCode),
            converted,
            value.Unit?.Name ?? NameOfCurrency(value.CurrencyCode),
            converted.Unit?.Name ?? NameOfCurrency(converted.CurrencyCode));
        return true;
    }

    static IReadOnlyList<CalcToken>? TrailingBinary(IReadOnlyList<CalcToken> tokens)
    {
        if (tokens.Count < 2)
            return null;
        var last = tokens[^1];
        var binary = last.Kind == CalcTokenKind.Operator
            && last.Text is "+" or "-" or "*" or "/" or "^" or "mod" or "<<" or ">>" or "&" or "|" or "xor";
        if (!binary && last.IsIdent && last.Text.Equals("x", StringComparison.OrdinalIgnoreCase))
            binary = true;
        return binary ? tokens.Take(tokens.Count - 1).ToList() : null;
    }

    static CalcResult Render(string expression, CalcValue value, string? source, string? target)
    {
        if (value.Kind == CalcValueKind.Boolean)
        {
            var flag = value.Amount != 0 ? "true" : "false";
            return new CalcResult(expression, flag, flag, source, target, false);
        }

        if (value.CurrencyCode is "hex" or "bin" or "oct" or "decimal" or "binary" or "octal" or "hexadecimal" or "dec")
        {
            var scalar = value.Kind == CalcValueKind.Quantity && value.Unit is not null
                ? CalcUnits.ToBase(value.Amount, value.Unit)
                : value.Scalar;
            if (CalcMath.ExactInteger(scalar) is not { } n)
            {
                var badge = value.CurrencyCode[0] switch
                {
                    'h' => "Hexadecimal",
                    'b' => "Binary",
                    'o' => "Octal",
                    _ => "Decimal",
                };
                return new CalcResult(expression, "Cannot convert", "", source ?? "Decimal", badge, true);
            }

            var (display, name) = value.CurrencyCode[0] switch
            {
                'h' => ("0x" + n.ToString("X", CultureInfo.InvariantCulture), "Hexadecimal"),
                'b' => ("0b" + Convert.ToString(n, 2), "Binary"),
                'o' => ("0o" + Convert.ToString(n, 8), "Octal"),
                _ => (n.ToString(CultureInfo.InvariantCulture), "Decimal"),
            };
            return new CalcResult(expression, display, display, source ?? "Decimal", name, false);
        }

        if (value.Unit?.Symbol == "timespan")
        {
            var seconds = value.Unit.Factor == 1 && value.Unit.Name == "Timespan"
                ? value.Amount
                : CalcUnits.ToBase(value.Amount, value.Unit);
            var text = CalcFormatter.Timespan(seconds);
            return new CalcResult(expression, text, text, source, "Timespan", false);
        }

        if (value.Kind == CalcValueKind.Currency && value.CurrencyCode is { } code)
        {
            var amount = CalcFormatter.Currency(value.Amount);
        return new CalcResult(
            expression,
            CalcFormatter.Grouped(amount) + " " + code,
            amount + " " + code,
            source, target ?? code, false);
        }

        if (value.Kind == CalcValueKind.Quantity && value.Unit is { } unit)
        {
            var suffix = " " + unit.Symbol;
            return new CalcResult(
                expression,
                CalcFormatter.Display(value.Amount) + suffix,
                CalcFormatter.CopyText(value.Amount) + suffix,
                source ?? unit.Name, target ?? unit.Name, false);
        }

        if (value.Kind == CalcValueKind.Percent)
        {
            var fraction = value.Amount / 100.0;
            return new CalcResult(
                expression,
                CalcFormatter.Display(fraction),
                CalcFormatter.CopyText(fraction),
                "Percent", "Result", false);
        }

        return new CalcResult(
            expression,
            CalcFormatter.Display(value.Scalar),
            CalcFormatter.CopyText(value.Scalar),
            source ?? "Expression", target ?? "Result", false);
    }

    static string? BadgeOf(CalcValue value, bool source)
    {
        if (value.Kind == CalcValueKind.Currency)
            return NameOfCurrency(value.CurrencyCode);
        if (value.Kind == CalcValueKind.Quantity)
            return value.Unit?.Name;
        if (value.Kind == CalcValueKind.Boolean)
            return source ? "Comparison" : "Boolean";
        return source ? "Expression" : "Result";
    }

    static string? NameOfCurrency(string? code) =>
        code is null ? null : CalcCurrency.Find(code)?.Name ?? code;

    static string BaseName(int radix) => radix switch { 2 => "Binary", 8 => "Octal", 16 => "Hexadecimal", _ => "Decimal" };
}
