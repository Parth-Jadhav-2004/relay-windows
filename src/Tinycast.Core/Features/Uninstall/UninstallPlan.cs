namespace Tinycast.Features.Uninstall;

public enum UninstallProtection
{
    Removable,
    Missing,
    SystemProtected,
    UserLocked,
    ParentNotWritable,
    NotOwned,
}

public sealed record UninstallCandidate(
    string Path,
    string Title,
    string Kind,
    UninstallProtection Protection,
    string Evidence,
    long? Bytes = null)
{
    public bool IsRemovable => Protection == UninstallProtection.Removable;
    public bool SizePending => Bytes is null;
}

public sealed class UninstallSelection
{
    readonly HashSet<string> _checked;

    public UninstallSelection(IEnumerable<string>? selected = null) =>
        _checked = new HashSet<string>(selected ?? [], StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> Checked => _checked;

    public bool Contains(string path) => _checked.Contains(path);

    public void Intersect(IEnumerable<UninstallCandidate> candidates)
    {
        var removable = new HashSet<string>(
            candidates.Where(c => c.IsRemovable).Select(c => c.Path),
            StringComparer.OrdinalIgnoreCase);
        _checked.RemoveWhere(path => !removable.Contains(path));
    }

    public bool Toggle(string path, IEnumerable<UninstallCandidate> candidates)
    {
        var candidate = candidates.FirstOrDefault(c => c.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (candidate is null || !candidate.IsRemovable)
            return false;
        if (!_checked.Add(path))
            _checked.Remove(path);
        return true;
    }

    public void SelectAll(IEnumerable<UninstallCandidate> candidates)
    {
        foreach (var candidate in candidates.Where(c => c.IsRemovable))
            _checked.Add(candidate.Path);
    }

    public void SelectNone() => _checked.Clear();
}

public static class UninstallPlanLogic
{
    public static IReadOnlyList<UninstallCandidate> Ready(
        IReadOnlyList<UninstallCandidate> discovered,
        IReadOnlyDictionary<string, long> sizes)
    {
        return discovered
            .Select(c => sizes.TryGetValue(c.Path, out var bytes) ? c with { Bytes = bytes } : c)
            .ToList();
    }

    public static long SelectedBytes(IEnumerable<UninstallCandidate> candidates, UninstallSelection selection) =>
        candidates.Where(c => selection.Contains(c.Path)).Sum(c => c.Bytes ?? 0);

    public static UninstallProtection Classify(string path, bool exists, bool parentWritable, bool systemProtected, bool userLocked)
    {
        if (userLocked)
            return UninstallProtection.UserLocked;
        if (systemProtected)
            return UninstallProtection.SystemProtected;
        if (!exists)
            return UninstallProtection.Missing;
        if (!parentWritable)
            return UninstallProtection.ParentNotWritable;
        return UninstallProtection.Removable;
    }

    public static bool IsSelf(UninstallIdentity identity, string runningId, string runningFolder)
    {
        if (identity.BundleId is not null && identity.BundleId.Equals(runningId, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.IsNullOrWhiteSpace(runningFolder))
            return false;
        return identity.DisplayName.Equals("Tinycast", StringComparison.OrdinalIgnoreCase)
               && runningFolder.Contains("Tinycast", StringComparison.OrdinalIgnoreCase);
    }
}
