using System.Globalization;

namespace Tinycast.Features.Clipboard;

public enum DateBucket
{
    Today,
    Yesterday,
    ThisWeek,
    ThisMonth,
    Earlier,
}

public static class DateBuckets
{
    public static DateBucket From(DateTime createdAt, DateTime now)
    {
        var local = ToLocal(createdAt);
        var current = ToLocal(now);
        if (local.Date == current.Date)
            return DateBucket.Today;
        if (local.Date == current.Date.AddDays(-1))
            return DateBucket.Yesterday;

        var culture = CultureInfo.CurrentCulture;
        var cal = culture.Calendar;
        var rule = culture.DateTimeFormat.CalendarWeekRule;
        var first = culture.DateTimeFormat.FirstDayOfWeek;
        if (local.Year == current.Year
            && cal.GetWeekOfYear(local, rule, first) == cal.GetWeekOfYear(current, rule, first))
            return DateBucket.ThisWeek;
        if (local.Year == current.Year && local.Month == current.Month)
            return DateBucket.ThisMonth;
        return DateBucket.Earlier;
    }

    public static string Title(this DateBucket bucket) => bucket switch
    {
        DateBucket.Today => "Today",
        DateBucket.Yesterday => "Yesterday",
        DateBucket.ThisWeek => "This Week",
        DateBucket.ThisMonth => "This Month",
        _ => "Earlier",
    };

    static DateTime ToLocal(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
}
