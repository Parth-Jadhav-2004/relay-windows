using System.Globalization;
using System.Text.RegularExpressions;

namespace Tinycast.Features.Calculator;

public static class CalcDateTime
{
    static readonly Dictionary<string, int> Months = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jan"] = 1, ["january"] = 1, ["feb"] = 2, ["february"] = 2, ["mar"] = 3, ["march"] = 3,
        ["apr"] = 4, ["april"] = 4, ["may"] = 5, ["jun"] = 6, ["june"] = 6, ["jul"] = 7, ["july"] = 7,
        ["aug"] = 8, ["august"] = 8, ["sep"] = 9, ["sept"] = 9, ["september"] = 9,
        ["oct"] = 10, ["october"] = 10, ["nov"] = 11, ["november"] = 11, ["dec"] = 12, ["december"] = 12,
    };

    static readonly Dictionary<string, DayOfWeek> Weekdays = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sunday"] = DayOfWeek.Sunday, ["sun"] = DayOfWeek.Sunday,
        ["monday"] = DayOfWeek.Monday, ["mon"] = DayOfWeek.Monday,
        ["tuesday"] = DayOfWeek.Tuesday, ["tue"] = DayOfWeek.Tuesday, ["tues"] = DayOfWeek.Tuesday,
        ["wednesday"] = DayOfWeek.Wednesday, ["wed"] = DayOfWeek.Wednesday,
        ["thursday"] = DayOfWeek.Thursday, ["thu"] = DayOfWeek.Thursday, ["thur"] = DayOfWeek.Thursday, ["thurs"] = DayOfWeek.Thursday,
        ["friday"] = DayOfWeek.Friday, ["fri"] = DayOfWeek.Friday,
        ["saturday"] = DayOfWeek.Saturday, ["sat"] = DayOfWeek.Saturday,
    };

    public static CalcResult? Evaluate(string raw, CalcContext context)
    {
        var echo = raw.Trim();
        var lowered = Collapse(echo.ToLowerInvariant());
        if (lowered.Length == 0)
            return null;
        var signals = Signals(lowered);
        if (!signals.Any)
            return null;

        if (signals.Timestamp && ParseTimestamp(lowered, echo, context) is { } ts)
            return ts;
        if (signals.Until && ParseUntil(lowered, echo, context) is { } until)
            return until;
        if (signals.Since && ParseSince(lowered, echo, context) is { } since)
            return since;
        if (signals.Arith && ParseArithmetic(lowered, echo, context) is { } arith)
            return arith;
        if (signals.FromAgo && ParseOffset(lowered, echo, context) is { } offset)
            return offset;
        if (signals.InWord && ParseWeekdayIn(lowered, echo, context) is { } week)
            return week;
        if (signals.BareMoment && ParseBare(lowered, echo, context) is { } bare)
            return bare;
        return null;
    }

    static string Collapse(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    readonly record struct Sig(bool Digit, bool Until, bool Since, bool Arith, bool FromAgo, bool InWord, bool At, bool NextLast, bool Timestamp, bool Moment, bool DayName)
    {
        public bool Any => Until || Since || Arith || FromAgo || InWord || (At || NextLast || (Digit && DayName) || Timestamp);
        public bool BareMoment => At || NextLast || (Digit && DayName) || Timestamp;
    }

    static Sig Signals(string query)
    {
        var words = query.Split(' ');
        var s = new Sig();
        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];
            var first = i == 0;
            var last = i == words.Length - 1;
            switch (word)
            {
                case "till" or "until" or "til":
                    if (!first && !last) s = s with { Until = true };
                    break;
                case "since":
                    if (!first && !last) s = s with { Since = true };
                    break;
                case "+" or "-":
                    if (!first && !last) s = s with { Arith = true };
                    break;
                case "from":
                    if (!first && !last) s = s with { FromAgo = true };
                    break;
                case "ago":
                    if (!first) s = s with { FromAgo = true };
                    break;
                case "in":
                    if (!first && !last) s = s with { InWord = true };
                    break;
                case "at":
                    if (!first && !last) s = s with { At = true };
                    break;
                case "next" or "last":
                    if (first) s = s with { NextLast = true };
                    break;
                case "unix" or "timestamp":
                    s = s with { Timestamp = true, Moment = true };
                    break;
            }

            var dots = 0;
            var dashes = 0;
            foreach (var c in word)
            {
                if (char.IsDigit(c)) s = s with { Digit = true };
                if (c == '.') dots++;
                if (c == '-') dashes++;
                if (c is ':' or '/') s = s with { Moment = true };
            }

            if (dots >= 2) s = s with { DayName = true, Moment = true };
            if (dashes >= 2) s = s with { Moment = true };
            var letters = Regex.Replace(word, "[^a-z]", "");
            if (Months.ContainsKey(letters)) s = s with { DayName = true, Moment = true };
            if (Weekdays.ContainsKey(letters)) s = s with { Moment = true };
            if (letters is "now" or "today" or "tomorrow" or "yesterday" or "noon" or "midnight" or "am" or "pm")
                s = s with { Moment = true };
        }

        if (s.Arith && !s.Moment)
            s = s with { Arith = false };
        return s;
    }

    static CalcResult? ParseUntil(string query, string echo, CalcContext ctx)
    {
        var split = Regex.Split(query, @"\s+(till|until|til)\s+");
        if (split.Length < 3)
            return null;
        var unit = DurationUnit(split[0].Trim()) ?? "hr";
        if (!TryMoment(split[^1], ctx, Bias.Future, out var moment, out var hasTime))
            return null;
        return DurationCard(echo, ctx.Now, moment, unit, hasTime, "Until");
    }

    static CalcResult? ParseSince(string query, string echo, CalcContext ctx)
    {
        var split = Regex.Split(query, @"\s+since\s+");
        if (split.Length < 2)
            return null;
        var unit = DurationUnit(split[0].Trim()) ?? "day";
        if (!TryMoment(split[^1], ctx, Bias.Past, out var moment, out var hasTime))
            return null;
        return DurationCard(echo, moment, ctx.Now, unit, hasTime, "Since");
    }

    static CalcResult? ParseArithmetic(string query, string echo, CalcContext ctx)
    {
        var parts = Regex.Split(query, @"\s+([+-])\s+");
        if (parts.Length < 3)
            return null;
        if (!TryMoment(parts[0], ctx, Bias.Nearest, out var moment, out var hasTime))
            return null;
        for (var i = 1; i + 1 < parts.Length; i += 2)
        {
            var sign = parts[i] == "-" ? -1 : 1;
            if (!TryDuration(parts[i + 1], ctx, moment, hasTime, out var next, out hasTime))
                return null;
            var delta = next - moment;
            moment = sign > 0 ? next : moment - delta;
        }

        return MomentCard(echo, moment, hasTime, ctx);
    }

    static CalcResult? ParseOffset(string query, string echo, CalcContext ctx)
    {
        var ago = Regex.Match(query, @"^(.+?)\s+ago$");
        if (ago.Success)
        {
            if (!TryDuration(ago.Groups[1].Value + " from now", ctx, ctx.Now, true, out var past, out var hasTime))
                return null;
            var delta = past - ctx.Now;
            return MomentCard(echo, ctx.Now - delta, hasTime, ctx);
        }

        var from = Regex.Match(query, @"^(.+?)\s+from\s+(.+)$");
        if (!from.Success)
            return null;
        if (!TryMoment(from.Groups[2].Value, ctx, Bias.Nearest, out var anchor, out var hasT))
            return null;
        if (!TryDuration(from.Groups[1].Value, ctx, anchor, hasT, out var result, out hasT))
            return null;
        return MomentCard(echo, result, hasT, ctx);
    }

    static CalcResult? ParseWeekdayIn(string query, string echo, CalcContext ctx)
    {
        var match = Regex.Match(query, @"^([a-z]+)\s+in\s+(\d+)\s+weeks?$");
        if (!match.Success || !Weekdays.TryGetValue(match.Groups[1].Value, out var day))
            return null;
        if (!int.TryParse(match.Groups[2].Value, out var weeks))
            return null;
        var landing = ctx.Now.Date.AddDays(7 * weeks);
        var start = StartOfWeek(landing, ctx.FirstDayOfWeek);
        var offset = ((int)day - (int)ctx.FirstDayOfWeek + 7) % 7;
        return MomentCard(echo, start.AddDays(offset), false, ctx);
    }

    static CalcResult? ParseBare(string query, string echo, CalcContext ctx)
    {
        if (!TryMoment(query, ctx, Bias.Nearest, out var moment, out var hasTime))
            return null;
        return MomentCard(echo, moment, hasTime, ctx);
    }

    static CalcResult? ParseTimestamp(string query, string echo, CalcContext ctx)
    {
        if (Regex.IsMatch(query, @"^(now|today)\s+to\s+unix(\s+ms)?$"))
        {
            var ms = query.Contains("ms");
            var value = new DateTimeOffset(DateTime.SpecifyKind(ctx.Now, DateTimeKind.Unspecified), ctx.TimeZone.GetUtcOffset(ctx.Now)).ToUnixTimeMilliseconds();
            if (!ms)
                value /= 1000;
            var text = value.ToString(CultureInfo.InvariantCulture);
            return Ok(echo, text, text, "Now", ms ? "Unix ms" : "Unix");
        }

        var unix = Regex.Match(query, @"^unix\s+(-?\d+)(\s+ms)?(?:\s+to\s+date)?$");
        if (unix.Success)
        {
            var n = long.Parse(unix.Groups[1].Value, CultureInfo.InvariantCulture);
            var dto = unix.Groups[2].Success
                ? DateTimeOffset.FromUnixTimeMilliseconds(n)
                : DateTimeOffset.FromUnixTimeSeconds(n);
            var local = TimeZoneInfo.ConvertTime(dto, ctx.TimeZone).DateTime;
            var text = local.ToString("d MMMM yyyy HH:mm", CultureInfo.InvariantCulture);
            return Ok(echo, text, text, "Unix", WeekdayName(local));
        }

        if (DateTimeOffset.TryParse(query.Replace(" to date", "", StringComparison.OrdinalIgnoreCase),
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            var local = TimeZoneInfo.ConvertTime(parsed, ctx.TimeZone).DateTime;
            var text = local.ToString("d MMMM yyyy HH:mm", CultureInfo.InvariantCulture);
            return Ok(echo, text, text, "Timestamp", WeekdayName(local));
        }

        return null;
    }

    enum Bias { Future, Past, Nearest }

    static bool TryMoment(string text, CalcContext ctx, Bias bias, out DateTime moment, out bool hasTime)
    {
        moment = default;
        hasTime = false;
        text = text.Trim().TrimEnd('.');
        var at = Regex.Match(text, @"^(.*?)\s+at\s+(.+)$");
        var head = at.Success ? at.Groups[1].Value.Trim() : text;
        var clock = at.Success ? at.Groups[2].Value.Trim() : null;

        if (head is "now")
        {
            moment = ctx.Now;
            hasTime = true;
        }
        else if (head is "today")
            moment = ctx.Now.Date;
        else if (head is "tomorrow")
            moment = ctx.Now.Date.AddDays(1);
        else if (head is "yesterday")
            moment = ctx.Now.Date.AddDays(-1);
        else if (head is "noon")
        {
            moment = ctx.Now.Date.AddHours(12);
            hasTime = true;
        }
        else if (head is "midnight")
        {
            moment = ctx.Now.Date;
            hasTime = true;
        }
        else if (head.StartsWith("next ") && Weekdays.TryGetValue(head[5..], out var nextDay))
            moment = NextWeekday(ctx.Now.Date, nextDay, 1);
        else if (head.StartsWith("last ") && Weekdays.TryGetValue(head[5..], out var lastDay))
            moment = NextWeekday(ctx.Now.Date, lastDay, -1);
        else if (Weekdays.TryGetValue(head, out var namedDay))
            moment = SnapWeekday(ctx.Now.Date, namedDay, bias);
        else if (TryParseDate(head, ctx.Now, bias, out moment))
        { }
        else if (clock is null && TryParseClock(head, ctx.Now, bias, out moment))
        {
            hasTime = true;
            return true;
        }
        else
            return false;

        if (clock is not null)
        {
            if (!TryParseClock(clock, moment, Bias.Nearest, out var timed))
                return false;
            moment = moment.Date + timed.TimeOfDay;
            hasTime = true;
        }

        return true;
    }

    static bool TryParseDate(string text, DateTime now, Bias bias, out DateTime date)
    {
        date = default;
        text = text.Trim().TrimEnd('.');
        var dotted = Regex.Match(text, @"^(\d{1,2})\.(\d{1,2})\.(\d{2}|\d{4})$");
        if (dotted.Success)
        {
            var day = int.Parse(dotted.Groups[1].Value);
            var month = int.Parse(dotted.Groups[2].Value);
            var year = ExpandYear(int.Parse(dotted.Groups[3].Value));
            if (!ValidDate(year, month, day))
                return false;
            date = new DateTime(year, month, day);
            return true;
        }

        var ordinal = Regex.Match(text, @"^(\d{1,2})\.?\s*([a-z]+)$");
        if (ordinal.Success && Months.TryGetValue(ordinal.Groups[2].Value, out var m1)
            && int.TryParse(ordinal.Groups[1].Value, out var d1))
        {
            date = NearestDate(now, m1, d1, bias);
            return date != default;
        }

        var monthDay = Regex.Match(text, @"^([a-z]+)\s+(\d{1,2})(?:st|nd|rd|th)?(?:\s+(\d{2}|\d{4}))?$");
        if (monthDay.Success && Months.TryGetValue(monthDay.Groups[1].Value, out var m2)
            && int.TryParse(monthDay.Groups[2].Value, out var d2))
        {
            var year = monthDay.Groups[3].Success ? ExpandYear(int.Parse(monthDay.Groups[3].Value)) : 0;
            date = year == 0 ? NearestDate(now, m2, d2, bias) : new DateTime(year, m2, d2);
            return date != default && ValidDate(date.Year, date.Month, date.Day);
        }

        return false;
    }

    static bool TryParseClock(string text, DateTime day, Bias bias, out DateTime moment)
    {
        moment = default;
        text = text.Trim();
        var ampm = Regex.Match(text, @"^(\d{1,2})(?::(\d{2}))?(?::(\d{2}))?\s*(am|pm)?$");
        if (!ampm.Success)
            return false;
        var hour = int.Parse(ampm.Groups[1].Value);
        var minute = ampm.Groups[2].Success ? int.Parse(ampm.Groups[2].Value) : 0;
        var second = ampm.Groups[3].Success ? int.Parse(ampm.Groups[3].Value) : 0;
        var mer = ampm.Groups[4].Value;
        if (mer is "pm" && hour < 12)
            hour += 12;
        if (mer is "am" && hour == 12)
            hour = 0;
        if (hour is < 0 or > 23 || minute is < 0 or > 59)
            return false;
        moment = day.Date.AddHours(hour).AddMinutes(minute).AddSeconds(second);
        if (bias == Bias.Future && moment <= day)
            moment = moment.AddDays(1);
        if (bias == Bias.Past && moment >= day)
            moment = moment.AddDays(-1);
        return true;
    }

    static bool TryDuration(string text, CalcContext ctx, DateTime from, bool hasTime, out DateTime result, out bool outTime)
    {
        result = from;
        outTime = hasTime;
        text = text.Trim();
        if (Regex.IsMatch(text, @"^\d+$") && hasTime)
        {
            result = from.AddHours(int.Parse(text, CultureInfo.InvariantCulture));
            return true;
        }

        if (Regex.IsMatch(text, @"^\d+$") && !hasTime)
        {
            result = from.AddDays(int.Parse(text, CultureInfo.InvariantCulture));
            return true;
        }

        var match = Regex.Match(text, @"^(\d+(?:\.\d+)?)\s*(work\s*days?|business\s*days?|weekdays?|working\s*days?|months?|years?|weeks?|days?|hours?|hrs?|h|minutes?|mins?|min|seconds?|secs?|s)$");
        if (!match.Success)
            return false;
        var n = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var unit = match.Groups[2].Value.Replace(" ", "");
        if (unit.StartsWith("work") || unit.StartsWith("business") || unit.StartsWith("weekday") || unit.StartsWith("working"))
        {
            if (n != Math.Truncate(n))
                return false;
            result = AddBusinessDays(from, (int)n);
            outTime = false;
            return true;
        }

        result = unit[0] switch
        {
            'y' => from.AddYears((int)n),
            'm' when unit.StartsWith("month") => from.AddMonths((int)n),
            'w' => from.AddDays(7 * n),
            'd' => from.AddDays(n),
            'h' => from.AddHours(n),
            's' => from.AddSeconds(n),
            _ => unit.StartsWith("min") ? from.AddMinutes(n) : from,
        };
        outTime = unit[0] is 'h' or 's' || unit.StartsWith("min") || hasTime;
        if (unit.StartsWith("month") || unit[0] == 'y')
            outTime = hasTime;
        return true;
    }

    static DateTime AddBusinessDays(DateTime start, int days)
    {
        var date = start.Date;
        var sign = Math.Sign(days);
        var remaining = Math.Abs(days);
        if (date.DayOfWeek is DayOfWeek.Saturday)
            date = date.AddDays(sign > 0 ? 2 : -1);
        if (date.DayOfWeek is DayOfWeek.Sunday)
            date = date.AddDays(sign > 0 ? 1 : -2);
        var weeks = remaining / 5;
        date = date.AddDays(weeks * 7 * sign);
        remaining %= 5;
        while (remaining > 0)
        {
            date = date.AddDays(sign);
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                remaining--;
        }

        return date;
    }

    static DateTime NextWeekday(DateTime from, DayOfWeek day, int dir)
    {
        var date = from;
        do { date = date.AddDays(dir); }
        while (date.DayOfWeek != day);
        return date;
    }

    static DateTime SnapWeekday(DateTime from, DayOfWeek day, Bias bias)
    {
        if (from.DayOfWeek == day)
            return from;
        return bias == Bias.Past ? NextWeekday(from, day, -1) : NextWeekday(from, day, 1);
    }

    static DateTime NearestDate(DateTime now, int month, int day, Bias bias)
    {
        if (!ValidDate(now.Year, month, day))
            return default;
        var thisYear = new DateTime(now.Year, month, day);
        var next = thisYear.AddYears(1);
        var prev = thisYear.AddYears(-1);
        if (bias == Bias.Future)
            return thisYear >= now.Date ? thisYear : next;
        if (bias == Bias.Past)
            return thisYear <= now.Date ? thisYear : prev;
        var candidates = new[] { prev, thisYear, next };
        return candidates.MinBy(d => Math.Abs((d - now.Date).TotalDays));
    }

    static DateTime StartOfWeek(DateTime date, DayOfWeek first)
    {
        var diff = ((int)date.DayOfWeek - (int)first + 7) % 7;
        return date.AddDays(-diff);
    }

    static int ExpandYear(int year) => year < 100 ? year <= 68 ? 2000 + year : 1900 + year : year;

    static bool ValidDate(int y, int m, int d)
    {
        if (m is < 1 or > 12) return false;
        return d >= 1 && d <= DateTime.DaysInMonth(y, m);
    }

    static string? DurationUnit(string text) => text switch
    {
        "hrs" or "hr" or "hours" or "hour" or "h" => "hr",
        "days" or "day" or "d" => "day",
        "mins" or "min" or "minutes" or "minute" => "min",
        "weeks" or "week" => "wk",
        "sec" or "secs" or "seconds" or "s" => "s",
        _ => null,
    };

    static CalcResult DurationCard(string echo, DateTime from, DateTime to, string unit, bool hasTime, string badge)
    {
        var seconds = (to - from).TotalSeconds;
        var amount = unit switch
        {
            "hr" => seconds / 3600,
            "min" => seconds / 60,
            "day" => (to.Date - from.Date).TotalDays,
            "wk" => seconds / 604800,
            _ => seconds,
        };
        var display = CalcFormatter.Display(amount) + " " + unit;
        return Ok(echo, display, CalcFormatter.CopyText(amount) + " " + unit, badge, unit == "day" ? "Days" : "Duration");
    }

    static CalcResult MomentCard(string echo, DateTime moment, bool hasTime, CalcContext ctx)
    {
        var display = hasTime
            ? moment.ToString("d MMMM yyyy HH:mm", CultureInfo.InvariantCulture)
            : moment.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
        var copy = hasTime
            ? moment.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
            : moment.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return Ok(echo, display, copy, WeekdayName(ctx.Now), WeekdayName(moment));
    }

    static string WeekdayName(DateTime date) => date.ToString("dddd", CultureInfo.InvariantCulture);

    static CalcResult Ok(string expr, string display, string copy, string? src, string? dst) =>
        new(expr, display, copy, src, dst, false);
}
