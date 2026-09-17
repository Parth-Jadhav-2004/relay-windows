namespace Relay.Features.Calculator;

public sealed record CurrencyDef(string Code, string Name);

public static class CalcCurrency
{
    public static readonly (string Code, string Name, string[] Aliases)[] Crypto =
    [
        ("ADA", "Cardano", ["cardano"]),
        ("AVAX", "Avalanche", ["avalanche"]),
        ("BCH", "Bitcoin Cash", []),
        ("BNB", "BNB", ["binance"]),
        ("BSV", "Bitcoin SV", []),
        ("BTC", "Bitcoin", ["bitcoin"]),
        ("DASH", "Dash", []),
        ("DOGE", "Dogecoin", ["dogecoin"]),
        ("DOT", "Polkadot", ["polkadot"]),
        ("EOS", "EOS", []),
        ("ETC", "Ethereum Classic", []),
        ("ETH", "Ethereum", ["ethereum", "ether"]),
        ("LTC", "Litecoin", ["litecoin"]),
        ("LUNA", "Terra", ["terra"]),
        ("NEO", "Neo", []),
        ("POL", "Polygon", ["polygon"]),
        ("SHIB", "Shiba Inu", ["shiba"]),
        ("SOL", "Solana", ["solana"]),
        ("TRX", "TRON", ["tron"]),
        ("USDT", "Tether", ["tether"]),
        ("XLM", "Stellar", ["stellar"]),
        ("XMR", "Monero", ["monero"]),
        ("XRP", "XRP", ["ripple"]),
    ];

    public static IReadOnlyList<string> CryptoCodes { get; } = Crypto.Select(c => c.Code).ToList();

    public static CurrencyDef? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var key = Fold(name);
        return ByName.TryGetValue(key, out var def) ? def : null;
    }

    public static bool LooksLikeCode(string name) =>
        name.Length == 3 && name.All(char.IsLetter);

    static Dictionary<string, CurrencyDef> Build()
    {
        var defs = new Dictionary<string, CurrencyDef>(StringComparer.OrdinalIgnoreCase);
        var table = new Dictionary<string, CurrencyDef>(StringComparer.Ordinal);
        foreach (var (code, name, aliases) in Fiat)
        {
            var def = new CurrencyDef(code, name);
            defs[code] = def;
            table[Fold(code)] = def;
            table[Fold(name)] = def;
            foreach (var alias in aliases)
                table[Fold(alias)] = def;
        }

        foreach (var (code, words) in Contested)
        {
            if (!defs.TryGetValue(code, out var def))
                continue;
            foreach (var word in words)
                table[Fold(word)] = def;
        }

        foreach (var (code, words) in IsoNames)
        {
            if (!defs.TryGetValue(code, out var def))
                continue;
            foreach (var word in words)
                table[Fold(word)] = def;
        }

        foreach (var (code, words) in SignCodes)
        {
            if (!defs.TryGetValue(code, out var def))
                continue;
            foreach (var word in words)
                table[Fold(word)] = def;
        }

        foreach (var (code, name, aliases) in Crypto)
        {
            var def = new CurrencyDef(code, name);
            defs[code] = def;
            table[Fold(code)] = def;
            foreach (var alias in aliases)
                table[Fold(alias)] = def;
        }

        table["$"] = defs["USD"];
        table["€"] = defs["EUR"];
        table["£"] = defs["GBP"];
        table["¥"] = defs["JPY"];
        table["₹"] = defs["INR"];
        return table;
    }

    public static string Fold(string value)
    {
        var form = value.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var chars = form.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
            != System.Globalization.UnicodeCategory.NonSpacingMark);
        return string.Concat(chars).Replace(" ", "");
    }

    static readonly (string Code, string Name, string[] Aliases)[] Fiat =
    [
        ("USD", "US Dollar", ["usdollar", "usdolar"]),
        ("EUR", "Euro", ["euro", "euros"]),
        ("GBP", "British Pound", ["sterling", "poundsterling"]),
        ("JPY", "Japanese Yen", ["yen"]),
        ("INR", "Indian Rupee", ["rupee", "rupees"]),
        ("CAD", "Canadian Dollar", []),
        ("AUD", "Australian Dollar", []),
        ("CHF", "Swiss Franc", []),
        ("CNY", "Chinese Yuan", ["yuan"]),
        ("KRW", "South Korean Won", ["won"]),
        ("BDT", "Bangladeshi Taka", ["taka"]),
        ("BRL", "Brazilian Real", ["real", "reais"]),
        ("MXN", "Mexican Peso", []),
        ("SGD", "Singapore Dollar", ["sgd"]),
        ("HKD", "Hong Kong Dollar", []),
        ("NZD", "New Zealand Dollar", []),
        ("SEK", "Swedish Krona", []),
        ("NOK", "Norwegian Krone", []),
        ("DKK", "Danish Krone", []),
        ("PLN", "Polish Zloty", ["zloty"]),
        ("CZK", "Czech Koruna", ["koruna"]),
        ("HUF", "Hungarian Forint", ["forint"]),
        ("TRY", "Turkish Lira", ["lira"]),
        ("ZAR", "South African Rand", ["rand"]),
        ("AED", "UAE Dirham", ["dirham"]),
        ("SAR", "Saudi Riyal", ["riyal"]),
        ("THB", "Thai Baht", ["baht"]),
        ("IDR", "Indonesian Rupiah", ["rupiah"]),
        ("MYR", "Malaysian Ringgit", ["ringgit"]),
        ("PHP", "Philippine Peso", []),
        ("VND", "Vietnamese Dong", ["dong"]),
        ("PKR", "Pakistani Rupee", []),
        ("LKR", "Sri Lankan Rupee", []),
        ("NPR", "Nepalese Rupee", []),
        ("RUB", "Russian Ruble", ["ruble", "rouble"]),
        ("UAH", "Ukrainian Hryvnia", []),
        ("ILS", "Israeli Shekel", ["shekel"]),
        ("EGP", "Egyptian Pound", []),
        ("NGN", "Nigerian Naira", ["naira"]),
        ("KES", "Kenyan Shilling", ["shilling"]),
        ("CLP", "Chilean Peso", []),
        ("ARS", "Argentine Peso", []),
        ("COP", "Colombian Peso", []),
        ("PEN", "Peruvian Sol", ["soles"]),
        ("TWD", "New Taiwan Dollar", []),
        ("CNH", "Chinese Yuan Offshore", []),
        ("XDR", "Special Drawing Rights", []),
        ("CRC", "Costa Rican Colon", ["colon"]),
    ];

    static readonly Dictionary<string, string[]> Contested = new()
    {
        ["USD"] = ["dollar", "dollars"],
        ["EUR"] = ["euro", "euros"],
        ["GBP"] = ["pound", "pounds"],
        ["CHF"] = ["franc", "francs"],
        ["MXN"] = ["peso", "pesos"],
        ["INR"] = ["rupee", "rupees"],
    };

    static readonly Dictionary<string, string[]> IsoNames = new()
    {
        ["CNY"] = ["rmb", "renminbi", "yuanrenminbi"],
    };

    static readonly Dictionary<string, string[]> SignCodes = new()
    {
        ["TWD"] = ["ntd"],
    };

    static readonly Dictionary<string, CurrencyDef> ByName = Build();
}
