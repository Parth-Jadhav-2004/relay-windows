namespace Tinycast.Features.Calendar;

public static class MeetingJoinCard
{
    public static MeetingEvent? NextJoinable(IReadOnlyList<MeetingEvent> meetings, DateTime now)
    {
        return meetings
            .Where(m => m.Link is not null && !m.IsAllDay)
            .Where(m => m.Start >= now.AddMinutes(-5) && m.Start <= now.AddHours(2))
            .OrderBy(m => m.Start)
            .FirstOrDefault();
    }
}
