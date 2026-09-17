using System.Globalization;

namespace Relay.Features.Clipboard;

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

        var first = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        var delta = ((int)current.DayOfWeek - (int)first + 7) % 7;
        var weekStart = current.Date.AddDays(-delta);
        if (local.Date >= weekStart && local.Date < weekStart.AddDays(7))
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
