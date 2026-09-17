using System.Globalization;
using System.Text.RegularExpressions;

namespace Tinycast.Features.Calculator;

public static class CalcTimeZone
{
    static readonly HashSet<string> Connectors = new(StringComparer.OrdinalIgnoreCase) { "in", "to", "at" };

    static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["utc"] = "UTC", ["gmt"] = "GMT", ["z"] = "UTC",
        ["pst"] = "Pacific Standard Time", ["pdt"] = "Pacific Standard Time",
        ["mst"] = "Mountain Standard Time", ["mdt"] = "Mountain Standard Time",
        ["cst"] = "Central Standard Time", ["cdt"] = "Central Standard Time",
        ["est"] = "Eastern Standard Time", ["edt"] = "Eastern Standard Time",
        ["cet"] = "Central Europe Standard Time", ["cest"] = "Central Europe Standard Time",
        ["jst"] = "Tokyo Standard Time", ["ist"] = "India Standard Time",
        ["aest"] = "AUS Eastern Standard Time", ["bst"] = "GMT Standard Time",
        ["tokyo"] = "Tokyo Standard Time", ["japan"] = "Tokyo Standard Time",
        ["london"] = "GMT Standard Time", ["ldn"] = "GMT Standard Time", ["lon"] = "GMT Standard Time", ["uk"] = "GMT Standard Time",
        ["paris"] = "Romance Standard Time", ["france"] = "Romance Standard Time",
        ["berlin"] = "W. Europe Standard Time", ["germany"] = "W. Europe Standard Time",
        ["nyc"] = "Eastern Standard Time", ["ny"] = "Eastern Standard Time", ["newyork"] = "Eastern Standard Time",
        ["sf"] = "Pacific Standard Time", ["sfo"] = "Pacific Standard Time",
        ["la"] = "Pacific Standard Time", ["lax"] = "Pacific Standard Time", ["losangeles"] = "Pacific Standard Time",
        ["chicago"] = "Central Standard Time",
        ["sydney"] = "AUS Eastern Standard Time", ["melbourne"] = "AUS Eastern Standard Time", ["australia"] = "AUS Eastern Standard Time",
        ["kolkata"] = "India Standard Time", ["mumbai"] = "India Standard Time", ["delhi"] = "India Standard Time", ["india"] = "India Standard Time",
        ["dubai"] = "Arabian Standard Time", ["uae"] = "Arabian Standard Time",
        ["singapore"] = "Singapore Standard Time",
        ["hongkong"] = "China Standard Time", ["hkg"] = "China Standard Time", ["beijing"] = "China Standard Time", ["china"] = "China Standard Time",
        ["seoul"] = "Korea Standard Time", ["korea"] = "Korea Standard Time",
        ["saopaulo"] = "E. South America Standard Time",
        ["zurich"] = "W. Europe Standard Time", ["zürich"] = "W. Europe Standard Time",
        ["vienna"] = "W. Europe Standard Time", ["vie"] = "W. Europe Standard Time",
        ["lhr"] = "GMT Standard Time", ["nrt"] = "Tokyo Standard Time", ["jfk"] = "Eastern Standard Time",
        ["usa"] = "Eastern Standard Time", ["us"] = "Eastern Standard Time",
        ["asia/tokyo"] = "Tokyo Standard Time", ["europe/london"] = "GMT Standard Time",
        ["america/new_york"] = "Eastern Standard Time", ["america/los_angeles"] = "Pacific Standard Time",
    };

    static readonly Lazy<Dictionary<string, TimeZoneInfo>> Cities = new(BuildCities);

    public static CalcResult? Evaluate(string raw, CalcContext context)
    {
        if (raw.Length > 128 || !raw.Any(char.IsWhiteSpace))
            return null;
        var words = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || !words.Any(w => Connectors.Contains(w)))
            return null;
        var last = CalcCurrency.Fold(words[^1]);
        if (Zone(last) is null && !LooksLikeDuration(last) && !words[^1].All(char.IsDigit))
            return null;

        var query = string.Join(" ", words).ToLowerInvariant();
        if (ParseDiff(query, context) is { } diff)
            return diff;

        var (zoneQuery, offset) = SplitOffset(query);
        var parts = zoneQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;
        var connector = Array.FindLastIndex(parts, w => Connectors.Contains(w));
        if (connector <= 0 || connector == parts.Length - 1)
            return null;
        var targetWords = parts[(connector + 1)..];
        TimeZoneInfo target;
        TimeSpan? ahead = null;
        if (Resolve(string.Join("", targetWords), context) is { } named)
            target = named;
        else if (ParseDuration(string.Join(" ", targetWords)) is { } duration)
        {
            target = context.TimeZone;
            ahead = duration;
        }
        else
            return null;

        var leading = parts[..connector];
        if (!SourceMoment(leading, context, out var source, out var sourceZone))
            return null;

        DateTimeOffset sourceDto;
        try
        {
            var wall = DateTime.SpecifyKind(source, DateTimeKind.Unspecified);
            if (sourceZone.IsInvalidTime(wall))
                return null;
            sourceDto = new DateTimeOffset(wall, sourceZone.GetUtcOffset(wall));
            if (ahead is { } a)
                sourceDto = sourceDto.Add(a);
            if (offset is { } o)
                sourceDto = sourceDto.Add(o);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }

        DateTime dest;
        DateTime adjustedSource;
        try
        {
            dest = TimeZoneInfo.ConvertTime(sourceDto, target).DateTime;
            adjustedSource = TimeZoneInfo.ConvertTime(sourceDto, sourceZone).DateTime;
        }
        catch (ArgumentException)
        {
            return null;
        }

        var note = DayNote(adjustedSource.Date, dest.Date);
        var time = dest.ToString("h:mm tt", CultureInfo.InvariantCulture).ToLowerInvariant();
        var display = time + note;
        return new CalcResult(
            adjustedSource.ToString("h:mm tt", CultureInfo.InvariantCulture).ToLowerInvariant(),
            display, time, Label(sourceZone), Label(target), false);
    }

    static CalcResult? ParseDiff(string query, CalcContext context)
    {
        var match = Regex.Match(query, @"^diff(?:erence)?(?:\s+(?:with|from|to|in))?\s+(.+)$");
        if (!match.Success)
            return null;
        if (Resolve(CalcCurrency.Fold(match.Groups[1].Value), context) is not { } zone)
            return null;
        var home = context.TimeZone.GetUtcOffset(context.Now);
        var there = zone.GetUtcOffset(context.Now);
        var hours = (there - home).TotalHours;
        var display = CalcFormatter.Display(hours) + " hr";
        return new CalcResult(query, display, CalcFormatter.CopyText(hours) + " hr", Label(context.TimeZone), Label(zone), false);
    }

    static (string Query, TimeSpan? Offset) SplitOffset(string query)
    {
        foreach (var sep in new[] { " + ", " - " })
        {
            var index = query.LastIndexOf(sep, StringComparison.Ordinal);
            if (index < 0)
                continue;
            var tail = query[(index + sep.Length)..];
            if (ParseDuration(tail, implyHours: true) is not { } duration)
                continue;
            return (query[..index], sep == " - " ? -duration : duration);
        }

        return (query, null);
    }

    static bool SourceMoment(string[] leading, CalcContext context, out DateTime source, out TimeZoneInfo zone)
    {
        source = context.Now;
        zone = context.TimeZone;
        var text = string.Join(" ", leading);
        if (text is "time" or "what time is it" or "now")
            return true;
        var inIndex = Array.FindIndex(leading, w => w is "in");
        string[] clockWords = leading;
        if (inIndex > 0)
        {
            clockWords = leading[..inIndex];
            var srcName = CalcCurrency.Fold(string.Join("", leading[(inIndex + 1)..]));
            if (Resolve(srcName, context) is { } src)
                zone = src;
        }

        var clock = string.Join(" ", clockWords);
        if (clock is "time" or "now" or "")
            return true;
        var ampm = Regex.Match(clock, @"^(\d{1,2})(?::(\d{2}))?\s*(am|pm)?$");
        if (!ampm.Success)
            return false;
        var hour = int.Parse(ampm.Groups[1].Value);
        var minute = ampm.Groups[2].Success ? int.Parse(ampm.Groups[2].Value) : 0;
        if (ampm.Groups[3].Value is "pm" && hour < 12) hour += 12;
        if (ampm.Groups[3].Value is "am" && hour == 12) hour = 0;
        if (hour is < 0 or > 23 || minute is < 0 or > 59)
            return false;
        source = context.Now.Date.AddHours(hour).AddMinutes(minute);
        return true;
    }

    static TimeSpan? ParseDuration(string text, bool implyHours = false)
    {
        var match = Regex.Match(text.Trim(), @"^(\d+(?:\.\d+)?)\s*(hours?|hrs?|h|minutes?|mins?|min|m|seconds?|secs?|s)?$");
        if (!match.Success)
            return null;
        if (!double.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n))
            return null;
        var unit = match.Groups[2].Value;
        try
        {
            if (unit.Length == 0)
                return implyHours ? TimeSpan.FromHours(n) : null;
            return unit[0] switch
            {
                'h' => TimeSpan.FromHours(n),
                'm' => TimeSpan.FromMinutes(n),
                's' => TimeSpan.FromSeconds(n),
                _ => TimeSpan.FromHours(n),
            };
        }
        catch (OverflowException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    static bool LooksLikeDuration(string word) =>
        word is "hours" or "hour" or "hrs" or "hr" or "h" or "minutes" or "minute" or "mins" or "min";

    static TimeZoneInfo? Resolve(string folded, CalcContext context)
    {
        if (folded.Length == 0)
            return null;
        if (Aliases.TryGetValue(folded, out var id) && TryFind(id) is { } aliased)
            return aliased;
        if (Cities.Value.TryGetValue(folded, out var city))
            return city;
        return TryFind(folded);
    }

    static TimeZoneInfo? Zone(string folded) =>
        Aliases.ContainsKey(folded) || Cities.Value.ContainsKey(folded) ? Resolve(folded, new CalcContext(DateTime.UtcNow, TimeZoneInfo.Utc)) : null;

    static TimeZoneInfo? TryFind(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception) { return null; }
    }

    static Dictionary<string, TimeZoneInfo> BuildCities()
    {
        var map = new Dictionary<string, TimeZoneInfo>(StringComparer.Ordinal);
        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
        {
            foreach (var part in zone.Id.Split('/', ' '))
            {
                var key = CalcCurrency.Fold(part);
                if (key.Length >= 3 && !map.ContainsKey(key))
                    map[key] = zone;
            }

            var display = zone.DisplayName;
            var start = display.LastIndexOf(')');
            if (start >= 0 && start + 1 < display.Length)
            {
                var city = CalcCurrency.Fold(display[(start + 1)..]);
                if (city.Length >= 3 && !map.ContainsKey(city))
                    map[city] = zone;
            }
        }

        return map;
    }

    static string Label(TimeZoneInfo zone)
    {
        var id = zone.Id;
        var slash = id.LastIndexOf('/');
        if (slash >= 0 && slash + 1 < id.Length)
            return id[(slash + 1)..].Replace("_", " ");
        return zone.StandardName.Replace(" Standard Time", "", StringComparison.OrdinalIgnoreCase);
    }

    static string DayNote(DateTime from, DateTime to)
    {
        var days = (to.Date - from.Date).Days;
        return days switch
        {
            0 => "",
            1 => " (tomorrow)",
            -1 => " (yesterday)",
            > 1 => " (in " + days + " days)",
            _ => " (" + -days + " days ago)",
        };
    }
}
