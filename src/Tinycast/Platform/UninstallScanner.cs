using Microsoft.Win32;
using Tinycast.Features.Uninstall;

namespace Tinycast.Platform;

internal static class UninstallScanner
{
    public static IReadOnlyList<UninstallLeftover> Find(string query)
    {
        var identity = new UninstallIdentity(query.Trim(), AppPaths.ChannelId, []);
        var hits = new Dictionary<string, UninstallLeftover>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                 })
            AddFolders(hits, root, identity);

        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            foreach (var path in new[]
                     {
                         @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
                         @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
                     })
                AddRegistry(hits, hive, path, identity);
        }

        AddStartMenu(hits, identity);
        return hits.Values.OrderByDescending(h => h.Bytes).ToList();
    }

    static void AddFolders(Dictionary<string, UninstallLeftover> hits, string root, UninstallIdentity identity)
    {
        if (!Directory.Exists(root))
            return;
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root).Take(120))
            {
                var name = Path.GetFileName(dir);
                if (!UninstallRules.MatchesName(name, identity) || UninstallRules.IsProtected(dir))
                    continue;
                hits[dir] = new UninstallLeftover(dir, name, UninstallLeftoverLogic.FolderBytes(dir), true, "Folder");
            }
        }
        catch (Exception) { }
    }

    static void AddRegistry(Dictionary<string, UninstallLeftover> hits, RegistryKey hive, string path, UninstallIdentity identity)
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
            var uninstall = sub.GetValue("QuietUninstallString") as string
                            ?? sub.GetValue("UninstallString") as string;
            var location = sub.GetValue("InstallLocation") as string;
            if (!string.IsNullOrWhiteSpace(location) && Directory.Exists(location) && !UninstallRules.IsProtected(location))
                hits[location] = new UninstallLeftover(location, display, UninstallLeftoverLogic.FolderBytes(location), true, "Install");
            if (!string.IsNullOrWhiteSpace(uninstall))
                hits["reg:" + hive.Name + ":" + name] = new UninstallLeftover(uninstall, display, 0, true, "Uninstaller");
        }
    }

    static void AddStartMenu(Dictionary<string, UninstallLeftover> hits, UninstallIdentity identity)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        };
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
                continue;
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories).Take(200))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!UninstallRules.MatchesName(name, identity) || UninstallRules.IsProtected(file))
                        continue;
                    hits[file] = new UninstallLeftover(file, name, UninstallLeftoverLogic.FolderBytes(file), true, "Shortcut");
                }
            }
            catch (Exception) { }
        }
    }
}
