namespace Relay.Features.Calculator;

public enum UnitCategory
{
    Length, Pixels, PixelArea, PixelDensity, Weight, Temperature, Time, Area, Volume, VolumeFlow,
    DigitalStorage, Angle, Speed, Pressure, DataRate, Acceleration, Force, Energy, Power,
    ElectricCurrent, Voltage, Resistance, ElectricCharge, Frequency,
}

public sealed record UnitDef(string Symbol, string Name, UnitCategory Category, double Factor, double Offset = 0)
{
    public CalcDimension Dimension => CalcDimension.For(Category);
}

public readonly record struct CalcDimension(int L, int M, int T, int D, int I, int Px, int C)
{
    public static CalcDimension None => default;
    public static CalcDimension Length => new(1, 0, 0, 0, 0, 0, 0);
    public static CalcDimension Mass => new(0, 1, 0, 0, 0, 0, 0);
    public static CalcDimension Time => new(0, 0, 1, 0, 0, 0, 0);
    public static CalcDimension Data => new(0, 0, 0, 1, 0, 0, 0);
    public static CalcDimension Current => new(0, 0, 0, 0, 1, 0, 0);
    public static CalcDimension Pixel => new(0, 0, 0, 0, 0, 1, 0);
    public static CalcDimension Currency => new(0, 0, 0, 0, 0, 0, 1);

    public bool IsScalar => L == 0 && M == 0 && T == 0 && D == 0 && I == 0 && Px == 0 && C == 0;

    public CalcDimension Add(CalcDimension other) =>
        new(L + other.L, M + other.M, T + other.T, D + other.D, I + other.I, Px + other.Px, C + other.C);

    public CalcDimension Sub(CalcDimension other) =>
        new(L - other.L, M - other.M, T - other.T, D - other.D, I - other.I, Px - other.Px, C - other.C);

    public CalcDimension Scale(int n) => new(L * n, M * n, T * n, D * n, I * n, Px * n, C * n);

    public static CalcDimension For(UnitCategory category) => category switch
    {
        UnitCategory.Length => Length,
        UnitCategory.Weight => Mass,
        UnitCategory.Time => Time,
        UnitCategory.DigitalStorage => Data,
        UnitCategory.ElectricCurrent => Current,
        UnitCategory.Pixels => Pixel,
        UnitCategory.Area => new(2, 0, 0, 0, 0, 0, 0),
        UnitCategory.Volume => new(3, 0, 0, 0, 0, 0, 0),
        UnitCategory.Speed => new(1, 0, -1, 0, 0, 0, 0),
        UnitCategory.Acceleration => new(1, 0, -2, 0, 0, 0, 0),
        UnitCategory.Force => new(1, 1, -2, 0, 0, 0, 0),
        UnitCategory.Pressure => new(-1, 1, -2, 0, 0, 0, 0),
        UnitCategory.Energy => new(2, 1, -2, 0, 0, 0, 0),
        UnitCategory.Power => new(2, 1, -3, 0, 0, 0, 0),
        UnitCategory.Voltage => new(2, 1, -3, 0, -1, 0, 0),
        UnitCategory.Resistance => new(2, 1, -3, 0, -2, 0, 0),
        UnitCategory.ElectricCharge => new(0, 0, 1, 0, 1, 0, 0),
        UnitCategory.Frequency => new(0, 0, -1, 0, 0, 0, 0),
        UnitCategory.DataRate => new(0, 0, -1, 1, 0, 0, 0),
        UnitCategory.VolumeFlow => new(3, 0, -1, 0, 0, 0, 0),
        UnitCategory.PixelDensity => new(-1, 0, 0, 0, 0, 1, 0),
        UnitCategory.PixelArea => new(0, 0, 0, 0, 0, 2, 0),
        _ => None,
    };
}

public static class CalcUnits
{
    static readonly Lazy<(Dictionary<string, UnitDef> Exact, Dictionary<string, UnitDef> Folded)> Index =
        new(Build);

    public static UnitDef? Find(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        var (exact, folded) = Index.Value;
        if (exact.TryGetValue(name, out var unit))
            return unit;
        return folded.TryGetValue(Fold(name), out unit) ? unit : null;
    }

    public static double ToBase(double amount, UnitDef unit) =>
        unit.Category == UnitCategory.Temperature
            ? amount * unit.Factor + unit.Offset
            : amount * unit.Factor;

    public static double FromBase(double amount, UnitDef unit) =>
        unit.Category == UnitCategory.Temperature
            ? (amount - unit.Offset) / unit.Factor
            : amount / unit.Factor;

    public static double? Convert(double amount, UnitDef from, UnitDef to)
    {
        if (from.Dimension != to.Dimension)
            return null;
        if (from.Category == UnitCategory.Temperature && from.Symbol != to.Symbol
            && from.Category != to.Category && to.Category != UnitCategory.Temperature)
            return null;
        return FromBase(ToBase(amount, from), to);
    }

    public static string CategoryName(UnitCategory category) => category switch
    {
        UnitCategory.Length => "Length",
        UnitCategory.Weight => "Weight",
        UnitCategory.Temperature => "Temperature",
        UnitCategory.Time => "Time",
        UnitCategory.Area => "Area",
        UnitCategory.Volume => "Volume",
        UnitCategory.DigitalStorage => "Data",
        UnitCategory.Angle => "Angle",
        UnitCategory.Speed => "Speed",
        _ => category.ToString(),
    };

    public static UnitDef? BaseUnit(CalcDimension dim)
    {
        UnitDef? best = null;
        foreach (var unit in Index.Value.Exact.Values)
        {
            if (unit.Dimension != dim)
                continue;
            if (best is null || BetterBase(unit, best))
                best = unit;
        }

        return best;
    }

    static bool BetterBase(UnitDef candidate, UnitDef current)
    {
        var candBase = Math.Abs(candidate.Factor - 1) < 1e-12 && candidate.Offset == 0;
        var currBase = Math.Abs(current.Factor - 1) < 1e-12 && current.Offset == 0;
        if (candBase != currBase)
            return candBase;
        if (candidate.Name.Length != current.Name.Length)
            return candidate.Name.Length < current.Name.Length;
        return string.CompareOrdinal(candidate.Name, current.Name) < 0;
    }

    public static UnitDef? ProductUnit(UnitDef left, UnitDef right)
    {
        if (left.Category is UnitCategory.Power && right.Category == UnitCategory.Time && right.Factor >= 60)
            return Find("Wh");
        if (left.Category is UnitCategory.ElectricCurrent && right.Category == UnitCategory.Time)
            return right.Factor >= 3600 ? Find("Ah") : Find("mAh") ?? Find("Ah");
        return BaseUnit(left.Dimension.Add(right.Dimension));
    }

    public static bool IsConnector(string word) =>
        word is "to" or "in" or "into" or "->" or "as";

    static string Fold(string name)
    {
        var folded = name.Trim().ToLowerInvariant().Replace(" ", "").Replace("²", "2").Replace("³", "3");
        folded = folded.Replace("°", "").Replace("µ", "u").Replace("μ", "u").Replace("ω", "ohm").Replace("Ω", "ohm");
        return folded;
    }

    static (Dictionary<string, UnitDef>, Dictionary<string, UnitDef>) Build()
    {
        var exact = new Dictionary<string, UnitDef>(StringComparer.Ordinal);
        var folded = new Dictionary<string, UnitDef>(StringComparer.Ordinal);
        var category = UnitCategory.Length;
        foreach (var rawLine in Records.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;
            var fields = line.Split('|');
            if (fields.Length == 1)
            {
                category = Enum.Parse<UnitCategory>(line, true);
                continue;
            }

            var factor = double.Parse(fields[2], System.Globalization.CultureInfo.InvariantCulture);
            var offset = fields.Length >= 5
                ? double.Parse(fields[4], System.Globalization.CultureInfo.InvariantCulture)
                : 0;
            var def = new UnitDef(fields[0], fields[1], category, factor, offset);
            foreach (var alias in fields[3].Split(','))
                Add(exact, folded, alias, def);
            Add(exact, folded, fields[0], def);
        }

        foreach (var (prefix, name, factor) in new (string, string, double)[]
        {
            ("p", "Pico", 1e-12), ("n", "Nano", 1e-9), ("µ", "Micro", 1e-6), ("m", "Milli", 1e-3),
            ("c", "Centi", 1e-2), ("d", "Deci", 1e-1), ("k", "Kilo", 1e3), ("M", "Mega", 1e6),
            ("G", "Giga", 1e9), ("T", "Tera", 1e12), ("P", "Peta", 1e15),
        })
        {
            foreach (var key in new[] { "m", "g", "s", "hz", "n", "j", "w", "pa" })
            {
                if (!folded.TryGetValue(key, out var baseUnit) && !exact.TryGetValue(key, out baseUnit))
                    continue;
                var symbol = prefix + baseUnit.Symbol;
                var label = name + baseUnit.Name.ToLowerInvariant();
                var unit = new UnitDef(symbol, label, baseUnit.Category, baseUnit.Factor * factor);
                var noun = label.ToLowerInvariant();
                var aliases = new List<string> { symbol, noun };
                if (noun.EndsWith('s'))
                    aliases.Add(noun[..^1]);
                if (prefix == "µ")
                {
                    aliases.Add("u" + baseUnit.Symbol);
                    aliases.Add("μ" + baseUnit.Symbol);
                }

                foreach (var alias in aliases)
                    Add(exact, folded, alias, unit, overwrite: false);
            }
        }

        foreach (var (prefix, label, factor) in new (string, string, double)[]
        {
            ("", "", 1), ("k", "Kilo", 1e3), ("M", "Mega", 1e6), ("G", "Giga", 1e9), ("T", "Tera", 1e12),
            ("Ki", "Kibi", 1024), ("Mi", "Mebi", 1048576), ("Gi", "Gibi", 1073741824),
            ("Ti", "Tebi", 1099511627776),
        })
        {
            var bytes = prefix + "B/s";
            var byteUnit = new UnitDef(bytes, (label.Length == 0 ? "Bytes" : label + "bytes") + " per Second", UnitCategory.DataRate, factor);
            Add(exact, folded, bytes, byteUnit);
            if (prefix.Length > 0)
            {
                var bits = prefix + "bit";
                var bitUnit = new UnitDef(bits, label + "bits", UnitCategory.DigitalStorage, factor / 8);
                Add(exact, folded, bits, bitUnit);
                var rate = bits + "/s";
                Add(exact, folded, rate, new UnitDef(rate, label + "bits per Second", UnitCategory.DataRate, factor / 8));
            }
        }

        return (exact, folded);
    }

    static void Add(
        Dictionary<string, UnitDef> exact,
        Dictionary<string, UnitDef> folded,
        string alias,
        UnitDef def,
        bool overwrite = true)
    {
        alias = alias.Trim();
        if (alias.Length == 0)
            return;
        if (overwrite || !exact.ContainsKey(alias))
            exact[alias] = def;
        var key = Fold(alias);
        if (overwrite || !folded.ContainsKey(key))
            folded[key] = def;
    }

    const string Records = """
        Length
        mm|Millimeters|0.001|mm,millimeter,millimeters,millimetre,millimetres
        cm|Centimeters|0.01|cm,centimeter,centimeters,centimetre,centimetres
        dm|Decimeters|0.1|dm,decimeter,decimeters,decimetre,decimetres
        m|Meters|1.0|m,meter,meters,metre,metres
        km|Kilometers|1000.0|km,kilometer,kilometers,kilometre,kilometres
        in|Inches|0.0254|in,inch,inches
        ft|Feet|0.3048|ft,foot,feet
        yd|Yards|0.9144|yd,yard,yards
        mi|Miles|1609.344|mi,mile,miles
        nmi|Nautical Miles|1852.0|nmi,nauticalmile,nauticalmiles
        Pixels
        px|Pixels|1.0|px,pixel,pixels
        rem|REM|16.0|rem,rems
        em|EM|16.0|em,ems
        PixelArea
        px²|Square Pixels|1.0|px2
        PixelDensity
        ppi|Pixels per Inch|39.37007874015748|ppi,px/in,px/inch,px/inches,pixels/inch
        px/cm|Pixels per Centimeter|100.0|px/cm
        px/mm|Pixels per Millimeter|1000.0|px/mm
        px/m|Pixels per Meter|1.0|px/m
        Weight
        mg|Milligrams|1e-06|mg,milligram,milligrams
        g|Grams|0.001|g,gram,grams
        kg|Kilograms|1.0|kg,kilogram,kilograms,kilo,kilos
        oz|Ounces|0.028349523125|oz,ounce,ounces
        lb|Pounds|0.45359237|lb,lbs,pound,pounds
        t|Tonnes|1000.0|t,ton,tons,tonne,tonnes
        st|Stone|6.35029318|st,stone
        short ton|US Tons|907.18474|shortton,uston
        long ton|UK Tons|1016.0469088|longton,ukton
        Temperature
        °C|Celsius|1.0|c,°c,celsius,centigrade|273.15
        °F|Fahrenheit|0.5555555555555556|f,°f,fahrenheit|255.3722222222222
        K|Kelvin|1.0|k,kelvin,kelvins
        Time
        ms|Milliseconds|0.001|ms,millisecond,milliseconds
        s|Seconds|1.0|s,sec,secs,second,seconds
        min|Minutes|60.0|min,mins,minute,minutes
        hr|Hours|3600.0|h,hr,hrs,hour,hours
        day|Days|86400.0|d,day,days
        week|Weeks|604800.0|wk,week,weeks
        workdays|Workdays|28800.0|workday,workdays,businessday,businessdays
        Area
        mm²|Square Millimeters|1e-06|mm2,sqmm
        cm²|Square Centimeters|0.0001|cm2,sqcm
        dm²|Square Decimeters|0.01|dm2,sqdm
        m²|Square Meters|1.0|m2,sqm
        km²|Square Kilometers|1000000.0|km2,sqkm
        in²|Square Inches|0.00064516|in2,sqin
        ft²|Square Feet|0.09290304|ft2,sqft
        yd²|Square Yards|0.83612736|yd2,sqyd
        mi²|Square Miles|2589988.110336|mi2,sqmi
        acre|Acres|4046.8564224|acre,acres
        ha|Hectares|10000.0|ha,hectare,hectares
        Volume
        mL|Milliliters|1e-06|ml,milliliter,milliliters,millilitre,millilitres
        cL|Centiliters|1e-05|cl,centiliter,centiliters,centilitre,centilitres
        dL|Deciliters|0.0001|dl,deciliter,deciliters,decilitre,decilitres
        L|Liters|0.001|l,liter,liters,litre,litres
        cup|Cups|0.0002365882365|cup,cups
        tbsp|Tablespoons|1.478676478125e-05|tbsp,tablespoon,tablespoons
        tsp|Teaspoons|4.92892159375e-06|tsp,teaspoon,teaspoons
        gal|Gallons|0.003785411784|gal,gallon,gallons
        qt|Quarts|0.000946352946|qt,quart,quarts
        pt|Pints|0.000473176473|pt,pint,pints
        fl oz|Fluid Ounces|2.95735295625e-05|floz,floz,fl oz
        mm³|Cubic Millimeters|1e-09|mm3
        cm³|Cubic Centimeters|1e-06|cm3,cc
        dm³|Cubic Decimeters|0.001|dm3
        m³|Cubic Meters|1.0|m3
        in³|Cubic Inches|1.6387064e-05|in3
        ft³|Cubic Feet|0.028316846592|ft3
        yd³|Cubic Yards|0.764554857984|yd3
        VolumeFlow
        L/s|Liters per Second|0.001|l/s,l/sec
        L/min|Liters per Minute|1.6666666666666667e-05|l/min,lpm
        L/h|Liters per Hour|2.7777777777777776e-07|l/h,l/hr,lph
        m³/s|Cubic Meters per Second|1.0|m3/s,m3/sec
        m³/h|Cubic Meters per Hour|0.0002777777777777778|m3/h,m3/hr
        gal/min|Gallons per Minute|6.30901964e-05|gal/min,gpm
        DigitalStorage
        bit|Bits|0.125|bit,bits
        B|Bytes|1.0|b,byte,bytes
        kB|Kilobytes|1000.0|kb,kilobyte,kilobytes
        MB|Megabytes|1000000.0|mb,megabyte,megabytes
        GB|Gigabytes|1000000000.0|gb,gigabyte,gigabytes
        TB|Terabytes|1000000000000.0|tb,terabyte,terabytes
        PB|Petabytes|1000000000000000.0|pb,petabyte,petabytes
        KiB|Kibibytes|1024.0|kib,kibibyte,kibibytes
        MiB|Mebibytes|1048576.0|mib,mebibyte,mebibytes
        GiB|Gibibytes|1073741824.0|gib,gibibyte,gibibytes
        TiB|Tebibytes|1099511627776.0|tib,tebibyte,tebibytes
        Angle
        rad|Radians|1.0|rad,radian,radians
        deg|Degrees|0.017453292519943295|deg,degree,degrees
        grad|Gradians|0.015707963267948967|grad,grads,gradian,gradians,gon
        arcmin|Arcminutes|0.0002908882086657216|arcmin,arcminute,arcminutes
        arcsec|Arcseconds|4.84813681109536e-06|arcsec,arcsecond,arcseconds
        turn|Turns|6.283185307179586|turn,turns,rev,revolution,revolutions
        Speed
        m/s|Meters per Second|1.0|mps,m/s,m/sec
        km/h|Kilometers per Hour|0.2777777777777778|kmh,kph,km/h,km/hr,kmph
        mph|Miles per Hour|0.44704|mph,mi/h,mi/hr
        ft/s|Feet per Second|0.3048|fps,ft/s,ft/sec
        kn|Knots|0.5144444444444445|kn,knot,knots
        km/s|Kilometers per Second|1000.0|km/s,km/sec
        Pressure
        Pa|Pascals|1.0|pa,pascal,pascals
        hPa|Hectopascals|100.0|hpa
        kPa|Kilopascals|1000.0|kpa
        bar|Bar|100000.0|bar,bars
        mbar|Millibar|100.0|mbar,millibar,millibars
        psi|PSI|6894.757293168|psi
        atm|Atmospheres|101325.0|atm,atmosphere,atmospheres
        mmHg|Millimeters of Mercury|133.322387415|mmhg
        Torr|Torr|133.32236842105263|torr
        DataRate
        bps|Bits per Second|0.125|bps
        Kbps|Kilobits per Second|125.0|kbps,kbit/s
        Mbps|Megabits per Second|125000.0|mbps,mbit/s
        Gbps|Gigabits per Second|125000000.0|gbps,gbit/s
        Tbps|Terabits per Second|125000000000.0|tbps,tbit/s
        Acceleration
        m/s²|Meters per Second Squared|1.0|m/s2,mps2
        Force
        N|Newtons|1.0|n,newton,newtons
        Energy
        J|Joules|1.0|j,joule,joules
        kJ|Kilojoules|1000.0|kj,kilojoule,kilojoules
        Wh|Watt Hours|3600.0|wh
        mWh|Milliwatt Hours|3.6|mwh
        kWh|Kilowatt Hours|3600000.0|kwh
        MWh|Megawatt Hours|3600000000.0|MWh,megawatthour,megawatthours
        cal|Calories|4.184|cal,calorie,calories
        kcal|Kilocalories|4184.0|kcal,kilocalorie,kilocalories
        Power
        W|Watts|1.0|w,watt,watts
        mW|Milliwatts|0.001|mw,milliwatt,milliwatts
        kW|Kilowatts|1000.0|kw,kilowatt,kilowatts
        MW|Megawatts|1000000.0|MW,megawatt,megawatts
        ElectricCurrent
        A|Amperes|1.0|a,amp,amps,ampere,amperes
        mA|Milliamperes|0.001|ma,milliamp,milliamps,milliampere,milliamperes
        MA|Megaamperes|1000000.0|MA,megaamp,megaamps
        µA|Microamperes|1e-06|ua,µa,μa,microamp,microamps
        Voltage
        V|Volts|1.0|v,volt,volts
        mV|Millivolts|0.001|mv,millivolt,millivolts
        kV|Kilovolts|1000.0|kv,kilovolt,kilovolts
        MV|Megavolts|1000000.0|MV,megavolt,megavolts
        Resistance
        Ω|Ohms|1.0|ohm,ohms
        mΩ|Milliohms|0.001|mohm,milliohm,milliohms
        kΩ|Kilohms|1000.0|kohm,kohms,kilohm,kilohms
        MΩ|Megohms|1000000.0|MΩ,megohm,megohms
        ElectricCharge
        As|Coulombs|1.0|as,coulomb,coulombs
        Ah|Ampere Hours|3600.0|ah,amphour,amphours
        mAh|Milliampere Hours|3.6|mah,milliamphour,milliamphours
        MAh|Megaampere Hours|3600000000.0|MAh
        Frequency
        Hz|Hertz|1.0|hz,hertz
        kHz|Kilohertz|1000.0|khz,kilohertz
        MHz|Megahertz|1000000.0|mhz,megahertz
        Volume
        UK gal|UK Gallons|0.00454609|ukgal,ukgallon,ukgallons
        UK qt|UK Quarts|0.0011365225|ukqt,ukquart,ukquarts
        UK pt|UK Pints|0.00056826125|ukpt,ukpint,ukpints
        UK fl oz|UK Fluid Ounces|2.84130625e-05|ukfloz
        Power
        hp|Horsepower|745.6998715822702|hp,horsepower
        Energy
        BTU|British Thermal Units|1055.05585262|btu
        Frequency
        rpm|Revolutions per Minute|0.016666666666666666|rpm
        Force
        lbf|Pounds Force|4.4482216152605|lbf,poundforce
        """;
}
