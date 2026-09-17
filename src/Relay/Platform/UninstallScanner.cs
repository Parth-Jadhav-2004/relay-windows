using Microsoft.Win32;
using Relay.Features.Uninstall;

namespace Relay.Platform;

internal static class UninstallScanner
{
    public static IReadOnlyList<UninstallCandidate> Discover(string query)
    {
        var identity = new UninstallIdentity(query.Trim(), AppPaths.ChannelId, []);
        if (UninstallPlanLogic.IsSelf(identity, AppPaths.ChannelId, AppPaths.Root))
            return [];

        var hits = new Dictionary<string, UninstallCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in LibraryRoots())
            AddFolders(hits, root, identity);

        AddStartMenu(hits, identity);
        AddDesktop(hits, identity);
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            foreach (var path in new[]
                     {
                         @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
                         @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
                     })
                AddRegistry(hits, hive, path, identity);
        }

        return hits.Values
            .OrderBy(c => c.IsRemovable ? 0 : 1)
            .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static Dictionary<string, long> Measure(IEnumerable<UninstallCandidate> candidates)
    {
        var sizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            if (!candidate.IsRemovable)
                continue;
            sizes[candidate.Path] = UninstallLeftoverLogic.FolderBytes(candidate.Path);
        }

        return sizes;
    }

    static IReadOnlyList<string> LibraryRoots() =>
    [
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages"),
    ];

    static void AddFolders(Dictionary<string, UninstallCandidate> hits, string root, UninstallIdentity identity)
    {
        if (!Directory.Exists(root))
            return;
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root).Take(400))
            {
                var name = Path.GetFileName(dir);
                if (!UninstallRules.MatchesName(name, identity))
                    continue;
                Add(hits, dir, name, "Folder");
            }
        }
        catch (Exception) { }
    }

    static void AddRegistry(Dictionary<string, UninstallCandidate> hits, RegistryKey hive, string path, UninstallIdentity identity)
    {
        using var key = hive.OpenSubKey(path);
        if (key is null)
            return;
        foreach (var name in key.GetSubKeyNames())
        {
            using var sub = key.OpenSubKey(name);
            if (sub is null)
                continue;
            var display = sub.GetValue("DisplayName") as string ?? "";
            if (!UninstallRules.MatchesName(display, identity) && !UninstallRules.MatchesName(name, identity))
                continue;
            var location = sub.GetValue("InstallLocation") as string;
            if (!string.IsNullOrWhiteSpace(location) && Directory.Exists(location))
                Add(hits, location, display.Length == 0 ? name : display, "Install");
            var uninstall = sub.GetValue("QuietUninstallString") as string
                            ?? sub.GetValue("UninstallString") as string;
            if (!string.IsNullOrWhiteSpace(uninstall))
            {
                var id = "reg:" + hive.Name + ":" + name;
                hits[id] = new UninstallCandidate(
                    id,
                    string.IsNullOrWhiteSpace(display) ? name : display,
                    "Uninstaller",
                    UninstallProtection.UserLocked,
                    uninstall);
            }
        }
    }

    static void AddStartMenu(Dictionary<string, UninstallCandidate> hits, UninstallIdentity identity)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        };
        foreach (var root in roots)
            AddFiles(hits, root, "*.lnk", identity, "Shortcut");
    }

    static void AddDesktop(Dictionary<string, UninstallCandidate> hits, UninstallIdentity identity)
    {
        AddFiles(hits, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "*.lnk", identity, "Shortcut");
    }

    static void AddFiles(
        Dictionary<string, UninstallCandidate> hits, string root, string pattern, UninstallIdentity identity, string kind)
    {
        if (!Directory.Exists(root))
            return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories).Take(400))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (!UninstallRules.MatchesName(name, identity))
                    continue;
                Add(hits, file, name, kind);
            }
        }
        catch (Exception) { }
    }

    static void Add(Dictionary<string, UninstallCandidate> hits, string path, string title, string kind)
    {
        var exists = File.Exists(path) || Directory.Exists(path);
        var protection = UninstallPlanLogic.Classify(
            path,
            exists,
            ParentWritable(path),
            UninstallRules.IsProtected(path),
            userLocked: false);
        hits[path] = new UninstallCandidate(path, title, kind, protection, path);
    }

    static bool ParentWritable(string path)
    {
        try
        {
            var parent = Path.GetDirectoryName(path.TrimEnd('\\', '/'));
            if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
                return false;
            var probe = Path.Combine(parent, ".relay-write-" + Guid.NewGuid().ToString("n"));
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
