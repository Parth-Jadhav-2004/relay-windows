using System.Text.RegularExpressions;

namespace Tinycast.Features.Calendar;

public enum MeetingProvider
{
    Zoom,
    GoogleMeet,
    Teams,
    Webex,
    Jitsi,
    Whereby,
    Chime,
    GotoMeeting,
    BlueJeans,
    Skype,
    Generic,
}

public sealed record MeetingLink(MeetingProvider Provider, Uri Url, string? Account)
{
    public string Title => Provider switch
    {
        MeetingProvider.Zoom => "Zoom",
        MeetingProvider.GoogleMeet => "Google Meet",
        MeetingProvider.Teams => "Microsoft Teams",
        MeetingProvider.Webex => "Webex",
        MeetingProvider.Jitsi => "Jitsi",
        MeetingProvider.Whereby => "Whereby",
        MeetingProvider.Chime => "Amazon Chime",
        MeetingProvider.GotoMeeting => "GoTo Meeting",
        MeetingProvider.BlueJeans => "BlueJeans",
        MeetingProvider.Skype => "Skype",
        _ => "Meeting Link",
    };

    public static MeetingLink? Detect(string? text) => Detect([text]);

    public static MeetingLink? Detect(IEnumerable<string?> fields, string? account = null)
    {
        MeetingLink? fallback = null;
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field))
                continue;
            foreach (var url in WebUrls(field))
            {
                var provider = Classify(url);
                if (provider is null)
                    continue;
                var link = new MeetingLink(provider.Value, url, account);
                if (provider != MeetingProvider.Generic)
                    return link;
                fallback ??= link;
            }
        }

        return fallback;
    }

    static MeetingProvider? Classify(Uri url)
    {
        if (url.Scheme is not ("http" or "https"))
            return null;
        var host = url.Host.ToLowerInvariant();
        var provider = FromHost(host);
        if (provider is null)
            return MeetingProvider.Generic;
        return Admits(provider.Value, url.AbsolutePath) ? provider : null;
    }

    static readonly Dictionary<MeetingProvider, string[]> HostSuffixes = new()
    {
        [MeetingProvider.Zoom] = ["zoom.us", "zoom.com", "zoomgov.com"],
        [MeetingProvider.GoogleMeet] = ["meet.google.com"],
        [MeetingProvider.Teams] = ["teams.microsoft.com", "teams.microsoft.us", "teams.live.com"],
        [MeetingProvider.Webex] = ["webex.com", "webex.com.cn"],
        [MeetingProvider.Jitsi] = ["meet.jit.si", "8x8.vc"],
        [MeetingProvider.Whereby] = ["whereby.com"],
        [MeetingProvider.Chime] = ["chime.aws"],
        [MeetingProvider.GotoMeeting] = ["gotomeeting.com", "gotomeet.me", "app.goto.com"],
        [MeetingProvider.BlueJeans] = ["bluejeans.com"],
        [MeetingProvider.Skype] = ["join.skype.com"],
    };

    static MeetingProvider? FromHost(string host)
    {
        foreach (var (provider, suffixes) in HostSuffixes)
        {
            if (suffixes.Any(s => host == s || host.EndsWith("." + s, StringComparison.Ordinal)))
                return provider;
        }

        return null;
    }

    static bool Admits(MeetingProvider provider, string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(s => s.ToLowerInvariant()).ToArray();
        return provider switch
        {
            MeetingProvider.Zoom => segments.Any(s => s is "j" or "w" or "s" or "my"),
            MeetingProvider.GoogleMeet => segments.Length == 1 && segments[0] != "tel",
            MeetingProvider.Teams => segments.Contains("meetup-join") || segments.Contains("meet"),
            _ => segments.Length > 0,
        };
    }

    static readonly HashSet<char> Terminators = [' ', '\t', '\n', '\r', '"', '\'', '<', '>', '\u00A0'];
    static readonly HashSet<char> Trailing = ['.', ',', ';', ':', ')', ']', '}', '!', '?'];

    static IEnumerable<Uri> WebUrls(string text)
    {
        var found = new List<Uri>();
        var cursor = 0;
        while (cursor < text.Length)
        {
            var start = text.IndexOf("http", cursor, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                break;
            var end = start;
            while (end < text.Length && !Terminators.Contains(text[end]))
                end++;
            var candidate = text[start..end];
            while (candidate.Length > 0 && Trailing.Contains(candidate[^1]))
                candidate = candidate[..^1];
            if ((candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                 || candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                && Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            {
                found.Add(uri);
            }

            cursor = end > start ? end : start + 1;
        }

        return found;
    }
}

public sealed record MeetingEvent(string Id, string Title, DateTime Start, DateTime End, MeetingLink? Link, bool IsAllDay, string? CalendarId = null);

public static class MeetingAutoJoin
{
    public static bool IsDue(MeetingEvent meeting, DateTime now, DateTime armedAt, ISet<string> joined)
    {
        if (meeting.IsAllDay || meeting.Link is null)
            return false;
        if (meeting.Link.Provider == MeetingProvider.Generic)
            return false;
        if (meeting.Start < armedAt)
            return false;
        if (meeting.Start > now.AddMinutes(1) || meeting.Start < now.AddMinutes(-1))
            return false;
        return !joined.Contains(meeting.Id);
    }
}
