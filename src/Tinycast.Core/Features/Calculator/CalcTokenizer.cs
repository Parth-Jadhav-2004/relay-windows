namespace Tinycast.Features.Calculator;

public enum CalcTokenKind
{
    Number,
    Compact,
    Radix,
    Ident,
    Operator,
    LParen,
    RParen,
    Comma,
}

public sealed record CalcToken(CalcTokenKind Kind, string Text, double Number = 0, int Radix = 10)
{
    public bool IsIdent => Kind == CalcTokenKind.Ident;
    public bool IsNumber => Kind is CalcTokenKind.Number or CalcTokenKind.Compact or CalcTokenKind.Radix;
}

public static class CalcTokenizer
{
    public static IReadOnlyList<CalcToken>? Tokenize(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 256)
            return null;
        var tokens = new List<CalcToken>();
        var i = 0;
        while (i < query.Length)
        {
            var c = query[i];
            if (char.IsWhiteSpace(c) || c is '\u00A0' or '\u2009' or '\u202F')
            {
                i++;
                continue;
            }

            if (c is '$' or '€' or '£' or '¥' or '₹')
            {
                tokens.Add(new CalcToken(CalcTokenKind.Ident, c.ToString()));
                i++;
                continue;
            }

            if (TryNumber(query, ref i, tokens, out var invalidNumber))
                continue;
            if (invalidNumber)
                return null;
            if (TryOperator(query, ref i, tokens))
                continue;
            if (c == '(')
            {
                tokens.Add(new CalcToken(CalcTokenKind.LParen, "("));
                i++;
                continue;
            }

            if (c == ')')
            {
                tokens.Add(new CalcToken(CalcTokenKind.RParen, ")"));
                i++;
                continue;
            }

            if (c == ',')
            {
                tokens.Add(new CalcToken(CalcTokenKind.Comma, ","));
                i++;
                continue;
            }

            if (TryIdent(query, ref i, tokens))
                continue;
            return null;
        }

        if (HasTopLevelComma(tokens))
            return null;
        FoldLoneX(tokens);
        return tokens.Count == 0 ? null : tokens;
    }

    static bool HasTopLevelComma(List<CalcToken> tokens)
    {
        var depth = 0;
        foreach (var token in tokens)
        {
            if (token.Kind == CalcTokenKind.LParen)
                depth++;
            else if (token.Kind == CalcTokenKind.RParen)
                depth--;
            else if (token.Kind == CalcTokenKind.Comma && depth <= 0)
                return true;
        }

        return false;
    }

    static void FoldLoneX(List<CalcToken> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Kind != CalcTokenKind.Ident || !tokens[i].Text.Equals("x", StringComparison.OrdinalIgnoreCase))
                continue;
            if (i > 0 && i < tokens.Count - 1 && IsOperandEnd(tokens[i - 1]) && IsOperandStart(tokens[i + 1]))
                tokens[i] = new CalcToken(CalcTokenKind.Operator, "*");
        }
    }

    static bool IsOperandEnd(CalcToken token) =>
        token.IsNumber || token.Kind is CalcTokenKind.RParen || token.Kind == CalcTokenKind.Ident;

    static bool IsOperandStart(CalcToken token) =>
        token.IsNumber || token.Kind is CalcTokenKind.LParen || token.Kind == CalcTokenKind.Ident
        || (token.Kind == CalcTokenKind.Operator && token.Text is "+" or "-");

    static bool TryNumber(string query, ref int i, List<CalcToken> tokens, out bool invalid)
    {
        invalid = false;
        if (query[i] is '0' && i + 1 < query.Length && query[i + 1] is 'x' or 'X' or 'b' or 'B' or 'o' or 'O')
        {
            var prefix = char.ToLowerInvariant(query[i + 1]);
            var radix = prefix switch { 'x' => 16, 'b' => 2, _ => 8 };
            var start = i + 2;
            var j = start;
            while (j < query.Length && IsRadixDigit(query[j], radix))
                j++;
            if (j == start)
                return false;
            try
            {
                var n = Convert.ToInt64(query[start..j], radix);
                tokens.Add(new CalcToken(CalcTokenKind.Radix, query[i..j], n, radix));
                i = j;
                return true;
            }
            catch (OverflowException)
            {
                invalid = true;
                return false;
            }
            catch (FormatException)
            {
                invalid = true;
                return false;
            }
        }

        if (!char.IsDigit(query[i]) && query[i] != '.')
            return false;

        var k = i;
        var buf = "";
        var digits = 0;
        while (k < query.Length)
        {
            var c = query[k];
            if (char.IsDigit(c))
            {
                buf += c;
                digits++;
                k++;
                continue;
            }

            if (c == ',' && k + 1 < query.Length && char.IsDigit(query[k + 1]))
            {
                var group = 0;
                var p = k + 1;
                while (p < query.Length && char.IsDigit(query[p]) && group < 3)
                {
                    group++;
                    p++;
                }

                if (group != 3)
                    break;
                k++;
                continue;
            }

            if (c == '.' && (buf.Length == 0 || !buf.Contains('.')))
            {
                buf += '.';
                k++;
                continue;
            }

            break;
        }

        if (digits == 0)
            return false;

        var kind = CalcTokenKind.Number;
        if (k < query.Length && query[k] is 'e' or 'E')
        {
            var e = k;
            var exp = e + 1;
            if (exp < query.Length && query[exp] is '+' or '-')
                exp++;
            var expStart = exp;
            while (exp < query.Length && char.IsDigit(query[exp]))
                exp++;
            if (exp > expStart)
            {
                buf += query[e..exp];
                k = exp;
                kind = CalcTokenKind.Compact;
            }
        }

        if (!double.TryParse(buf, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value))
            return false;

        if (k < query.Length && query[k] is 'k' or 'K' && (k + 1 >= query.Length || !char.IsLetter(query[k + 1])))
        {
            value *= 1000;
            k++;
            kind = CalcTokenKind.Compact;
        }

        if (!double.IsFinite(value))
            return false;
        tokens.Add(new CalcToken(kind, query[i..k], value));
        i = k;
        return true;
    }

    static bool IsRadixDigit(char c, int radix) => radix switch
    {
        2 => c is '0' or '1',
        8 => c is >= '0' and <= '7',
        16 => Uri.IsHexDigit(c),
        _ => false,
    };

    static bool TryOperator(string query, ref int i, List<CalcToken> tokens)
    {
        ReadOnlySpan<string> ops =
        [
            "**", "<<", ">>", "==", "!=", "<=", ">=", "->", "≠", "≤", "≥", "⊻",
            "+", "-", "*", "/", "^", "%", "&", "|", "~", "!", "<", ">", "=",
        ];
        foreach (var op in ops)
        {
            if (query.AsSpan(i).StartsWith(op, StringComparison.Ordinal))
            {
                var text = op switch { "**" => "^", "≠" => "!=", "≤" => "<=", "≥" => ">=", "⊻" => "xor", "=" => "==", _ => op };
                tokens.Add(new CalcToken(CalcTokenKind.Operator, text));
                i += op.Length;
                return true;
            }
        }

        return false;
    }

    static bool TryIdent(string query, ref int i, List<CalcToken> tokens)
    {
        if (!IsIdentStart(query[i]))
            return false;
        if (tokens.Count > 0 && tokens[^1].IsNumber && query[i] is 'x' or 'X')
        {
            tokens.Add(new CalcToken(CalcTokenKind.Ident, query[i].ToString()));
            i++;
            return true;
        }

        var start = i;
        i++;
        while (i < query.Length && IsIdentPart(query[i]))
            i++;
        if (i < query.Length && query[i] == '/' && i + 1 < query.Length && IsIdentStart(query[i + 1]))
        {
            var save = i;
            i++;
            while (i < query.Length && IsIdentPart(query[i]))
                i++;
            var compound = query[start..i];
            if (CalcUnits.Find(compound) is null && CalcCurrency.Find(compound) is null)
                i = save;
        }

        var ident = query[start..i];
        if (ident.Equals("mod", StringComparison.OrdinalIgnoreCase)
            || ident.Equals("xor", StringComparison.OrdinalIgnoreCase)
            || ident.Equals("to", StringComparison.OrdinalIgnoreCase)
            || ident.Equals("and", StringComparison.OrdinalIgnoreCase))
        {
            tokens.Add(new CalcToken(CalcTokenKind.Operator, ident.ToLowerInvariant()));
            return true;
        }

        tokens.Add(new CalcToken(CalcTokenKind.Ident, ident));
        return true;
    }

    static bool IsIdentStart(char c) =>
        char.IsLetter(c) || c is '_' or '°' or 'µ' or 'μ' or 'Ω' or 'π' or 'τ' or 'ω';

    static bool IsIdentPart(char c) =>
        IsIdentStart(c) || char.IsDigit(c) || c is '²' or '³' or '%';
}
