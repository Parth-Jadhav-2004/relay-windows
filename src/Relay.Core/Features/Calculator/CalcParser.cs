namespace Relay.Features.Calculator;

public enum CalcValueKind { Number, Percent, Quantity, Currency, Boolean }

public sealed record CalcValue(
    CalcValueKind Kind,
    double Amount,
    UnitDef? Unit = null,
    string? CurrencyCode = null,
    CalcDimension Dimension = default)
{
    public static CalcValue Number(double n) => new(CalcValueKind.Number, n);
    public static CalcValue Percent(double n) => new(CalcValueKind.Percent, n);
    public static CalcValue Bool(bool v) => new(CalcValueKind.Boolean, v ? 1 : 0);
    public static CalcValue Quantity(double amount, UnitDef unit) =>
        new(CalcValueKind.Quantity, amount, unit, null, unit.Dimension);
    public static CalcValue Money(double amount, string code) =>
        new(CalcValueKind.Currency, amount, null, code, CalcDimension.Currency);

    public bool IsFinite => double.IsFinite(Amount);
    public double Scalar => Kind == CalcValueKind.Percent ? Amount / 100.0 : Amount;
}

public static class CalcParser
{
    public static CalcValue? Evaluate(
        IReadOnlyList<CalcToken> tokens,
        CurrencyRates? rates,
        string? region,
        out string? error)
    {
        error = null;
        try
        {
            var p = new Parser(tokens, rates, region);
            var value = p.ParseExpression(0);
            if (value is null || p.HasFailed)
            {
                error = p.Error;
                return null;
            }

            if (!p.AtEnd)
            {
                if (TryConvert(p, value, out var converted, out error))
                    return converted;
                error = p.Error;
                return null;
            }

            return value;
        }
        catch (OverflowException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    static bool TryConvert(Parser p, CalcValue value, out CalcValue? converted, out string? error)
    {
        converted = null;
        error = null;
        if (!p.IsConnector)
            return false;
        p.Advance();
        var targetName = p.TakeIdent();
        if (targetName is null)
            return false;
        converted = ConvertValue(value, targetName, p.Rates, p.Region, out error);
        return converted is not null || error is not null;
    }

    public static CalcValue? ConvertValue(
        CalcValue value, string target, CurrencyRates? rates, string? region, out string? error)
    {
        error = null;
        if (target is "timespan" or "duration")
        {
            if (value.Kind == CalcValueKind.Quantity && value.Unit?.Category == UnitCategory.Time)
                return value with { Unit = new UnitDef("timespan", "Timespan", UnitCategory.Time, 1) };
            if (value.Kind == CalcValueKind.Number)
                return CalcValue.Quantity(value.Amount, new UnitDef("timespan", "Timespan", UnitCategory.Time, 1));
            return null;
        }

        if (target is "unix" or "timestamp")
            return null;

        if (target is "hex" or "bin" or "oct" or "dec" or "decimal" or "binary" or "octal" or "hexadecimal")
        {
            if (value.Kind is not CalcValueKind.Number and not CalcValueKind.Boolean
                && !(value.Kind == CalcValueKind.Quantity && value.Dimension.IsScalar))
            {
                if (value.Kind == CalcValueKind.Quantity
                    && value.Unit is not null
                    && CalcUnits.Convert(value.Amount, value.Unit, value.Unit) is not null
                    && value.Dimension.IsScalar)
                { }
                else if (value.Kind != CalcValueKind.Number)
                    return null;
            }

            return value with { Kind = CalcValueKind.Number, Unit = null, CurrencyCode = target };
        }

        var unit = CalcUnits.Find(target);
        var currency = CalcCurrency.Find(target);
        if (unit is not null && value.Kind is CalcValueKind.Quantity or CalcValueKind.Number)
        {
            if (value.Kind == CalcValueKind.Number)
                return CalcValue.Quantity(value.Amount, unit);
            if (value.Unit is null)
                return null;
            if (value.Unit.Category is UnitCategory.Temperature && unit.Category is UnitCategory.Temperature)
                return CalcValue.Quantity(CalcUnits.Convert(value.Amount, value.Unit, unit)!.Value, unit);
            if (value.Dimension != unit.Dimension)
            {
                error = "Cannot convert " + CalcUnits.CategoryName(value.Unit.Category) + " to " + CalcUnits.CategoryName(unit.Category) + ".";
                return null;
            }

            var converted = CalcUnits.Convert(value.Amount, value.Unit, unit);
            return converted is null ? null : CalcValue.Quantity(converted.Value, unit);
        }

        if (currency is not null)
        {
            if (value.Kind == CalcValueKind.Number)
            {
                if (string.IsNullOrWhiteSpace(region))
                {
                    error = "Specify a source currency.";
                    return null;
                }

                return ConvertMoney(value.Amount, region, currency.Code, rates, out error);
            }
            if (value.Kind == CalcValueKind.Currency && value.CurrencyCode is not null)
                return ConvertMoney(value.Amount, value.CurrencyCode, currency.Code, rates, out error);
            if (value.Kind == CalcValueKind.Quantity)
            {
                error = "Cannot convert " + CalcUnits.CategoryName(value.Unit!.Category) + " to Currency.";
                return null;
            }
        }

        if (unit is not null && value.Kind == CalcValueKind.Currency)
        {
            error = "Cannot convert Currency to " + CalcUnits.CategoryName(unit.Category) + ".";
            return null;
        }

        return null;
    }

    public static CalcValue? ConvertMoney(double amount, string from, string to, CurrencyRates? rates, out string? error)
    {
        error = null;
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            return CalcValue.Money(amount, to.ToUpperInvariant());
        if (rates is null)
        {
            error = "Exchange rates unavailable — check your connection.";
            return null;
        }

        var converted = rates.Convert(amount, from, to);
        if (converted is null)
        {
            var missing = rates.UsdPerUnit.ContainsKey(from.ToUpperInvariant()) ? to : from;
            error = "No exchange rate for " + missing.ToUpperInvariant() + ".";
            return null;
        }

        return CalcValue.Money(converted.Value, to.ToUpperInvariant());
    }

    sealed class Parser
    {
        readonly IReadOnlyList<CalcToken> _tokens;
        int _i;
        public CurrencyRates? Rates { get; }
        public string? Region { get; }
        public string? Error { get; private set; }
        public bool HasFailed { get; private set; }

        public Parser(IReadOnlyList<CalcToken> tokens, CurrencyRates? rates, string? region)
        {
            _tokens = tokens;
            Rates = rates;
            Region = region;
        }

        public bool AtEnd => _i >= _tokens.Count;
        public bool IsConnector =>
            !AtEnd && ((_tokens[_i].Kind == CalcTokenKind.Operator && _tokens[_i].Text is "to" or "->")
                       || (_tokens[_i].IsIdent && CalcUnits.IsConnector(_tokens[_i].Text)));

        CalcToken? Peek => AtEnd ? null : _tokens[_i];
        CalcToken? Peek2 => _i + 1 >= _tokens.Count ? null : _tokens[_i + 1];

        public void Advance() => _i++;

        public string? TakeIdent()
        {
            if (Peek is { Kind: CalcTokenKind.Ident } ident)
            {
                Advance();
                return ident.Text;
            }

            return null;
        }

        public CalcValue? ParseExpression(int minBp)
        {
            var left = ParsePrefix();
            if (left is null)
                return null;
            while (!AtEnd)
            {
                if (Peek?.Kind == CalcTokenKind.Operator && Peek.Text == "!" )
                {
                    if (Binding("!").lbp < minBp)
                        break;
                    Advance();
                    var fact = CalcMath.Factorial(left.Scalar);
                    if (fact is null || left.Kind is not CalcValueKind.Number and not CalcValueKind.Percent)
                        return Fail();
                    left = CalcValue.Number(fact.Value);
                    continue;
                }

                if (TryJuxtaposition(left, minBp, out var juxta))
                {
                    if (juxta is null)
                        return null;
                    left = juxta;
                    continue;
                }

                if (TryAdjacentQuantity(left, minBp, out var adj))
                {
                    if (adj is null)
                        return Fail();
                    left = adj;
                    continue;
                }

                if (IsConnector && minBp <= 1)
                {
                    var nextOp = OperatorAfterTarget();
                    if (nextOp is "*" or "/" or "^")
                        return Fail();
                    var saved = _i;
                    Advance();
                    var target = TakeIdent();
                    if (target is null)
                    {
                        _i = saved;
                        break;
                    }

                    if (target is "hex" or "bin" or "oct" or "dec" or "decimal" or "binary" or "octal" or "hexadecimal"
                        or "timespan" or "duration")
                    {
                        left = ConvertValue(left, target, Rates, Region, out var convErr);
                        Error = convErr;
                        if (left is null)
                            return convErr is null ? Fail() : null;
                        continue;
                    }

                    if (nextOp is "+" or "-" or "" or null || AtEnd || Peek?.Kind == CalcTokenKind.RParen)
                    {
                        var converted = ConvertValue(left, target, Rates, Region, out var err);
                        Error = err;
                        if (converted is null)
                            return err is null ? Fail() : null;
                        left = converted;
                        continue;
                    }

                    _i = saved;
                    break;
                }

                if (Peek?.Kind != CalcTokenKind.Operator)
                    break;
                var op = Peek.Text;
                var (lbp, rbp) = Binding(op);
                if (lbp < minBp)
                    break;
                if (op is "to")
                    break;
                Advance();
                if (AtEnd && IsBinary(op))
                    return left;
                var right = ParseExpression(rbp);
                if (right is null)
                    return left is not null && IsBinary(op) && AtEnd ? left : Fail();
                left = Apply(op, left, right);
                if (left is null)
                    return Error is not null ? null : Fail();
            }

            return left;
        }

        string? OperatorAfterTarget()
        {
            var j = _i + 1;
            if (j >= _tokens.Count || _tokens[j].Kind != CalcTokenKind.Ident)
                return null;
            j++;
            if (j >= _tokens.Count)
                return "";
            return _tokens[j].Kind == CalcTokenKind.Operator ? _tokens[j].Text : "";
        }

        bool TryJuxtaposition(CalcValue left, int minBp, out CalcValue? result)
        {
            result = null;
            if (Binding("*").lbp < minBp)
                return false;
            if (Peek is null)
                return false;
            var implicitMul = Peek.Kind == CalcTokenKind.LParen
                || (Peek.IsIdent && (CalcMath.IsFunction(Peek.Text) || CalcMath.Constants.ContainsKey(Peek.Text)
                    || Peek.Text.Equals("square", StringComparison.OrdinalIgnoreCase)
                    || Peek.Text.Equals("cube", StringComparison.OrdinalIgnoreCase)));
            if (!implicitMul)
                return false;
            var right = ParseExpression(Binding("*").rbp);
            result = right is null ? null : Apply("*", left, right);
            return true;
        }

        bool TryAdjacentQuantity(CalcValue left, int minBp, out CalcValue? result)
        {
            result = null;
            if (left.Kind != CalcValueKind.Quantity || Peek is null || !Peek.IsNumber)
                return false;
            if (Binding("*").lbp < minBp)
                return false;
            if (Peek2 is null || Peek2.Kind != CalcTokenKind.Ident || CalcUnits.Find(Peek2.Text) is null)
                return false;
            var right = ParsePrefix();
            if (right is null || right.Kind != CalcValueKind.Quantity || right.Unit is null || left.Unit is null)
                return true;
            if (left.Dimension != right.Dimension)
                return true;
            var converted = CalcUnits.Convert(right.Amount, right.Unit, left.Unit);
            if (converted is null)
                return true;
            result = CalcValue.Quantity(left.Amount + converted.Value, left.Unit);
            return true;
        }

        CalcValue? ParsePrefix()
        {
            if (AtEnd)
                return Fail();
            var tok = Peek!;
            if (tok.Kind == CalcTokenKind.Operator && tok.Text is "+" or "-" or "~")
            {
                Advance();
                var inner = ParseExpression(Binding(tok.Text).rbp);
                if (inner is null)
                    return null;
                return tok.Text switch
                {
                    "+" => inner,
                    "-" => Negate(inner),
                    "~" => inner.Kind == CalcValueKind.Number
                        ? CalcMath.Bitwise("~", inner.Amount) is { } b ? CalcValue.Number(b) : Fail()
                        : Fail(),
                    _ => Fail(),
                };
            }

            if (tok.Kind == CalcTokenKind.LParen)
            {
                Advance();
                var inner = ParseExpression(0);
                if (Peek?.Kind != CalcTokenKind.RParen)
                    return Fail();
                Advance();
                inner = AttachUnit(inner);
                return inner;
            }

            if (tok.IsIdent && tok.Text.Equals("square", StringComparison.OrdinalIgnoreCase)
                && Peek2?.Text.Equals("root", StringComparison.OrdinalIgnoreCase) == true)
                return SpokenRoot("sqrt");
            if (tok.IsIdent && tok.Text.Equals("cube", StringComparison.OrdinalIgnoreCase)
                && Peek2?.Text.Equals("root", StringComparison.OrdinalIgnoreCase) == true)
                return SpokenRoot("cbrt");

            if (tok.IsIdent && CalcMath.IsFunction(tok.Text))
                return ParseCall(tok.Text);

            if (tok.IsIdent && CalcMath.Constants.TryGetValue(tok.Text, out var constant))
            {
                Advance();
                return AttachUnit(CalcValue.Number(constant));
            }

            if (tok.IsIdent && CalcCurrency.Find(tok.Text) is { } sign && sign.Code.Length > 0
                && Peek2 is { } next && next.IsNumber)
            {
                Advance();
                Advance();
                var money = CalcValue.Money(next.Number, sign.Code);
                return money;
            }

            if (tok.IsNumber)
            {
                Advance();
                if (Peek?.Kind == CalcTokenKind.Operator && Peek.Text == "%" )
                {
                    Advance();
                    return CalcValue.Percent(tok.Number);
                }

                if (Peek?.IsIdent == true && Peek.Text == "%")
                {
                    Advance();
                    return CalcValue.Percent(tok.Number);
                }

                return AttachUnit(CalcValue.Number(tok.Number));
            }

            if (tok.IsIdent && CalcUnits.Find(tok.Text) is { } bareUnit)
            {
                Advance();
                return CalcValue.Quantity(1, bareUnit);
            }

            if (tok.IsIdent && CalcCurrency.Find(tok.Text) is { } currency)
            {
                Advance();
                return CalcValue.Money(1, currency.Code);
            }

            return Fail();
        }

        CalcValue? SpokenRoot(string fn)
        {
            Advance();
            if (!Peek?.Text.Equals("root", StringComparison.OrdinalIgnoreCase) == true)
                return Fail();
            Advance();
            if (Peek?.Text.Equals("of", StringComparison.OrdinalIgnoreCase) == true)
                Advance();
            var arg = ParseExpression(Binding("*").rbp);
            if (arg is null)
                return null;
            return ApplyFn(fn, [arg]);
        }

        CalcValue? ParseCall(string name)
        {
            Advance();
            if (Peek?.Kind == CalcTokenKind.LParen)
            {
                Advance();
                var args = new List<CalcValue>();
                if (Peek?.Kind != CalcTokenKind.RParen)
                {
                    while (true)
                    {
                        var arg = ParseExpression(0);
                        if (arg is null)
                            return null;
                        args.Add(arg);
                        if (Peek?.Kind == CalcTokenKind.Comma)
                        {
                            Advance();
                            continue;
                        }

                        break;
                    }
                }

                if (Peek?.Kind != CalcTokenKind.RParen)
                    return Fail();
                Advance();
                return ApplyFn(name, args);
            }

            var single = ParseExpression(Binding("*").rbp);
            return single is null ? null : ApplyFn(name, [single]);
        }

        CalcValue? ApplyFn(string name, List<CalcValue> args)
        {
            if (args.Count == 0)
                return Fail();
            if (CalcMath.MeasurementFns.Contains(name) && args[0].Kind == CalcValueKind.Quantity)
            {
                var unit = args[0].Unit!;
                var scalars = new List<double>();
                foreach (var arg in args)
                {
                    if (arg.Kind != CalcValueKind.Quantity || arg.Unit is null || arg.Dimension != unit.Dimension)
                        return Fail();
                    var converted = CalcUnits.Convert(arg.Amount, arg.Unit, unit);
                    if (converted is null)
                        return Fail();
                    scalars.Add(converted.Value);
                }

                var measured = CalcMath.Evaluate(name, scalars);
                return measured is null ? Fail() : CalcValue.Quantity(measured.Value, unit);
            }

            var numbers = args.Select(a => a.Kind is CalcValueKind.Number or CalcValueKind.Percent ? a.Scalar : a.Amount).ToList();
            var result = CalcMath.Evaluate(name, numbers);
            if (result is null)
                return Fail();
            if (args.Count == 1 && args[0].Kind == CalcValueKind.Quantity && args[0].Unit is { } u
                && name is "sqrt" or "cbrt")
            {
                var dim = name == "sqrt" ? args[0].Dimension.Scale(1) : args[0].Dimension;
                if (name == "sqrt")
                {
                    if (args[0].Dimension.L % 2 != 0 && args[0].Dimension.L != 0)
                    { }
                    var half = new CalcDimension(
                        args[0].Dimension.L / 2, args[0].Dimension.M / 2, args[0].Dimension.T / 2,
                        args[0].Dimension.D / 2, args[0].Dimension.I / 2, args[0].Dimension.Px / 2,
                        args[0].Dimension.C / 2);
                    var baseUnit = CalcUnits.BaseUnit(half) ?? u;
                    var inBase = CalcUnits.ToBase(args[0].Amount, u);
                    var rooted = Math.Sqrt(inBase);
                    return CalcValue.Quantity(CalcUnits.FromBase(rooted, baseUnit), baseUnit);
                }

                if (name == "cbrt")
                {
                    var third = new CalcDimension(
                        args[0].Dimension.L / 3, args[0].Dimension.M / 3, args[0].Dimension.T / 3,
                        args[0].Dimension.D / 3, args[0].Dimension.I / 3, args[0].Dimension.Px / 3,
                        args[0].Dimension.C / 3);
                    var baseUnit = CalcUnits.BaseUnit(third) ?? u;
                    var inBase = CalcUnits.ToBase(args[0].Amount, u);
                    var rooted = Math.Cbrt(inBase);
                    return CalcValue.Quantity(CalcUnits.FromBase(rooted, baseUnit), baseUnit);
                }
            }

            if (args.Count == 1 && args[0].Kind == CalcValueKind.Quantity && name is "round" or "abs" or "floor" or "ceil" or "trunc")
                return args[0] with { Amount = result.Value };
            return CalcValue.Number(result.Value);
        }

        CalcValue? AttachUnit(CalcValue? value)
        {
            if (value is null || Peek is null)
                return value;
            if (Peek.IsIdent && Peek.Text == "%")
            {
                Advance();
                return CalcValue.Percent(value.Scalar * 100);
            }

            if (Peek.IsIdent && CalcUnits.Find(Peek.Text) is { } unit)
            {
                Advance();
                if (value.Kind == CalcValueKind.Number)
                    return CalcValue.Quantity(value.Amount, unit);
                if (value.Kind == CalcValueKind.Quantity && value.Unit is not null
                    && value.Dimension == unit.Dimension)
                {
                    var converted = CalcUnits.Convert(value.Amount, value.Unit, unit);
                    return converted is null ? value : CalcValue.Quantity(converted.Value, unit);
                }
            }

            if (Peek.IsIdent && CalcCurrency.Find(Peek.Text) is { } currency)
            {
                Advance();
                if (value.Kind == CalcValueKind.Number)
                    return CalcValue.Money(value.Amount, currency.Code);
            }

            return value;
        }

        CalcValue? Apply(string op, CalcValue left, CalcValue right)
        {
            if (op is "==" or "!=" or "<" or "<=" or ">" or ">=")
                return Compare(op, left, right);
            if (op is "&" or "|" or "xor" or "<<" or ">>")
            {
                if (left.Kind != CalcValueKind.Number || right.Kind != CalcValueKind.Number)
                    return Fail();
                var bit = CalcMath.Bitwise(op, left.Amount, right.Amount);
                return bit is null ? Fail() : CalcValue.Number(bit.Value);
            }

            if (op == "mod")
            {
                if (right.Scalar == 0)
                    return Fail();
                return CalcValue.Number(Math.Truncate(left.Scalar) == left.Scalar
                    ? left.Scalar - right.Scalar * Math.Truncate(left.Scalar / right.Scalar)
                    : left.Scalar % right.Scalar);
            }

            if (right.Kind == CalcValueKind.Percent)
            {
                var p = right.Amount / 100.0;
                return op switch
                {
                    "+" => Scale(left, 1 + p),
                    "-" => Scale(left, 1 - p),
                    "*" => Scale(left, p),
                    "/" => p == 0 ? Fail() : Scale(left, 1 / p),
                    _ => Fail(),
                };
            }

            if (left.Kind == CalcValueKind.Currency || right.Kind == CalcValueKind.Currency)
                return ApplyCurrency(op, left, right);
            if (left.Kind == CalcValueKind.Quantity || right.Kind == CalcValueKind.Quantity)
                return ApplyQuantity(op, left, right);

            var a = left.Scalar;
            var b = right.Scalar;
            var n = op switch
            {
                "+" => a + b,
                "-" => a - b,
                "*" => a * b,
                "/" => b == 0 ? double.NaN : a / b,
                "^" => Math.Pow(a, b),
                _ => double.NaN,
            };
            return double.IsFinite(n) ? CalcValue.Number(n) : Fail();
        }

        CalcValue? Scale(CalcValue value, double factor) => value.Kind switch
        {
            CalcValueKind.Quantity when value.Unit is { } u => CalcValue.Quantity(value.Amount * factor, u),
            CalcValueKind.Currency when value.CurrencyCode is { } c => CalcValue.Money(value.Amount * factor, c),
            _ => CalcValue.Number(value.Scalar * factor),
        };

        CalcValue? ApplyQuantity(string op, CalcValue left, CalcValue right)
        {
            if (op is "+" or "-")
            {
                var unit = right.Kind == CalcValueKind.Quantity ? right.Unit : left.Unit;
                if (unit is null)
                    return Fail();
                double l = left.Kind == CalcValueKind.Number
                    ? left.Amount
                    : left.Unit is null ? double.NaN : CalcUnits.Convert(left.Amount, left.Unit, unit) ?? double.NaN;
                double r = right.Kind == CalcValueKind.Number
                    ? right.Amount
                    : right.Unit is null ? double.NaN : CalcUnits.Convert(right.Amount, right.Unit, unit) ?? double.NaN;
                if (!double.IsFinite(l) || !double.IsFinite(r))
                {
                    Error = left.Unit is not null && right.Unit is not null && left.Dimension != right.Dimension
                        ? "Cannot convert " + CalcUnits.CategoryName(left.Unit.Category) + " to " + CalcUnits.CategoryName(right.Unit.Category) + "."
                        : null;
                    return Fail();
                }

                if (unit.Category == UnitCategory.Temperature && left.Kind == CalcValueKind.Quantity && right.Kind == CalcValueKind.Quantity
                    && left.Unit!.Symbol != right.Unit!.Symbol)
                    return Fail();
                var sum = op == "+" ? l + r : l - r;
                return CalcValue.Quantity(sum, unit);
            }

            if (left.Kind != CalcValueKind.Quantity)
            {
                if (op == "*")
                    return right.Kind == CalcValueKind.Quantity
                        ? CalcValue.Quantity(left.Scalar * right.Amount, right.Unit!)
                        : Fail();
                return Fail();
            }

            if (op == "*" && right.Kind == CalcValueKind.Number)
                return CalcValue.Quantity(left.Amount * right.Scalar, left.Unit!);
            if (op == "/" && right.Kind == CalcValueKind.Number)
                return right.Scalar == 0 ? Fail() : CalcValue.Quantity(left.Amount / right.Scalar, left.Unit!);
            if (op == "^" && right.Kind == CalcValueKind.Number && right.Amount is 2 or 3)
            {
                var n = (int)right.Amount;
                var dim = left.Dimension.Scale(n);
                var unit = CalcUnits.BaseUnit(dim);
                if (unit is null || left.Unit is null)
                    return Fail();
                var @base = Math.Pow(CalcUnits.ToBase(left.Amount, left.Unit), n);
                return CalcValue.Quantity(CalcUnits.FromBase(@base, unit), unit);
            }

            if (right.Kind != CalcValueKind.Quantity || left.Unit is null || right.Unit is null)
                return Fail();
            if (op == "*")
            {
                var dim = left.Dimension.Add(right.Dimension);
                var unit = CalcUnits.ProductUnit(left.Unit, right.Unit) ?? CalcUnits.BaseUnit(dim);
                if (unit is null)
                    return Fail();
                var product = CalcUnits.ToBase(left.Amount, left.Unit) * CalcUnits.ToBase(right.Amount, right.Unit);
                return CalcValue.Quantity(CalcUnits.FromBase(product, unit), unit);
            }

            if (op == "/")
            {
                var rBase = CalcUnits.ToBase(right.Amount, right.Unit);
                if (rBase == 0)
                    return Fail();
                var dim = left.Dimension.Sub(right.Dimension);
                if (dim.IsScalar)
                    return CalcValue.Number(CalcUnits.ToBase(left.Amount, left.Unit) / rBase);
                var unit = CalcUnits.BaseUnit(dim);
                if (unit is null)
                    return Fail();
                var q = CalcUnits.ToBase(left.Amount, left.Unit) / rBase;
                return CalcValue.Quantity(CalcUnits.FromBase(q, unit), unit);
            }

            return Fail();
        }

        CalcValue? ApplyCurrency(string op, CalcValue left, CalcValue right)
        {
            string? code = right.Kind == CalcValueKind.Currency ? right.CurrencyCode : left.CurrencyCode;
            if (code is null)
                return Fail();
            double? l = MoneyAmount(left, code);
            double? r = MoneyAmount(right, code);
            if (l is null || r is null)
                return Error is not null ? null : Fail();
            if (op is "+" or "-")
                return CalcValue.Money(op == "+" ? l.Value + r.Value : l.Value - r.Value, code);
            if (op == "*" && right.Kind == CalcValueKind.Number)
                return CalcValue.Money(l.Value * right.Scalar, code);
            if (op == "*" && left.Kind == CalcValueKind.Number && right.Kind == CalcValueKind.Currency)
                return CalcValue.Money(left.Scalar * r.Value, code);
            if (op == "/" && right.Kind == CalcValueKind.Number)
                return right.Scalar == 0 ? Fail() : CalcValue.Money(l.Value / right.Scalar, code);
            if (op == "/" && right.Kind == CalcValueKind.Quantity && right.Unit is not null)
            {
                var unit = right.Unit;
                var per = new UnitDef(code + "/" + unit.Symbol, code + " per " + unit.Name, unit.Category, unit.Factor);
                return CalcValue.Quantity(l.Value / right.Amount, per);
            }

            return Fail();
        }

        double? MoneyAmount(CalcValue value, string code)
        {
            if (value.Kind == CalcValueKind.Number)
                return value.Amount;
            if (value.Kind != CalcValueKind.Currency || value.CurrencyCode is null)
                return null;
            if (string.Equals(value.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                return value.Amount;
            var converted = ConvertMoney(value.Amount, value.CurrencyCode, code, Rates, out var err);
            Error = err;
            return converted?.Amount;
        }

        CalcValue? Compare(string op, CalcValue left, CalcValue right)
        {
            double l, r;
            if (left.Kind == CalcValueKind.Quantity && right.Kind == CalcValueKind.Quantity
                && left.Unit is not null && right.Unit is not null)
            {
                if (left.Dimension != right.Dimension)
                {
                    Error = "Cannot convert " + CalcUnits.CategoryName(left.Unit.Category) + " to " + CalcUnits.CategoryName(right.Unit.Category) + ".";
                    return Fail();
                }

                l = CalcUnits.ToBase(left.Amount, left.Unit);
                r = CalcUnits.ToBase(right.Amount, right.Unit);
            }
            else if (left.Kind is CalcValueKind.Number or CalcValueKind.Percent
                && right.Kind is CalcValueKind.Number or CalcValueKind.Percent)
            {
                l = left.Scalar;
                r = right.Scalar;
            }
            else
                return Fail();

            var ok = op switch
            {
                "==" => AlmostEqual(l, r),
                "!=" => !AlmostEqual(l, r),
                "<" => l < r,
                "<=" => l <= r,
                ">" => l > r,
                ">=" => l >= r,
                _ => false,
            };
            return CalcValue.Bool(ok);
        }

        static CalcValue Negate(CalcValue value) => value.Kind switch
        {
            CalcValueKind.Quantity => value with { Amount = -value.Amount },
            CalcValueKind.Currency => value with { Amount = -value.Amount },
            CalcValueKind.Percent => value with { Amount = -value.Amount },
            _ => CalcValue.Number(-value.Scalar),
        };

        CalcValue? Fail()
        {
            HasFailed = true;
            return null;
        }

        static bool AlmostEqual(double l, double r) =>
            Math.Abs(l - r) <= 1e-9 * Math.Max(1.0, Math.Max(Math.Abs(l), Math.Abs(r)));

        static bool IsBinary(string op) => op is "+" or "-" or "*" or "/" or "^" or "mod" or "&" or "|" or "xor" or "<<" or ">>";

        static (int lbp, int rbp) Binding(string op) => op switch
        {
            "||" or "|" => (2, 2),
            "xor" => (3, 3),
            "&" => (4, 4),
            "==" or "!=" or "<" or "<=" or ">" or ">=" => (5, 5),
            "<<" or ">>" => (6, 6),
            "+" or "-" => (7, 7),
            "*" or "/" or "mod" or "%" => (8, 8),
            "^" => (9, 8),
            "~" or "!" => (10, 10),
            "to" or "->" => (1, 1),
            _ => (0, 0),
        };
    }
}
