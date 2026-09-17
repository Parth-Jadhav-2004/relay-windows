namespace Relay.Features.Clipboard;

public static class ClipboardPolicy
{
    public static bool IsIgnored(string? sourceId, IReadOnlyList<string> ignoredApps)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || ignoredApps.Count == 0)
            return false;
        string name;
        try
        {
            name = Path.GetFileNameWithoutExtension(sourceId);
            if (string.IsNullOrWhiteSpace(name))
                name = sourceId;
        }
        catch (Exception)
        {
            name = sourceId;
        }

        return ignoredApps.Any(ignored =>
            !string.IsNullOrWhiteSpace(ignored)
            && (name.Equals(ignored.Trim(), StringComparison.OrdinalIgnoreCase)
                || sourceId.Contains(ignored.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    public static DateTime? RetentionCutoff(int days, DateTime utcNow)
    {
        if (days <= 0)
            return null;
        return utcNow.AddDays(-Math.Clamp(days, 1, 3650));
    }
}
