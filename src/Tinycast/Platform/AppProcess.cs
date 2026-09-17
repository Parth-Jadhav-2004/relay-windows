using System.Diagnostics;
using Tinycast.Features.Launcher;

namespace Tinycast.Platform;

internal static class AppProcess
{
    public static void Reveal(AppEntry app)
    {
        if (string.IsNullOrWhiteSpace(app.Path))
            return;
        if (app.Path.Contains('!', StringComparison.Ordinal))
        {
            ProcessLauncher.Open("shell:AppsFolder\\" + app.Path);
            return;
        }

        FileSearchService.Reveal(app.Path);
    }

    public static void Quit(AppEntry app)
    {
        foreach (var process in Matching(app))
        {
            try { process.CloseMainWindow(); }
            catch (Exception) { }
        }
    }

    public static void Restart(AppEntry app)
    {
        foreach (var process in Matching(app))
        {
            try
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(800))
                    process.Kill(entireProcessTree: true);
            }
            catch (Exception) { }
        }

        ProcessLauncher.Launch(app);
    }

    static IEnumerable<Process> Matching(AppEntry app)
    {
        var name = ProcessName(app);
        if (string.IsNullOrWhiteSpace(name))
            return [];
        try
        {
            return Process.GetProcessesByName(name);
        }
        catch (Exception)
        {
            return [];
        }
    }

    static string? ProcessName(AppEntry app)
    {
        if (string.IsNullOrWhiteSpace(app.Path) || app.Path.Contains('!', StringComparison.Ordinal))
            return app.Title;
        try
        {
            return Path.GetFileNameWithoutExtension(app.Path);
        }
        catch (Exception)
        {
            return app.Title;
        }
    }
}
