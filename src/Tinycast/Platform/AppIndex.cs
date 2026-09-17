using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Tinycast.Features.Launcher;
using Tinycast.Features.SystemActions;
using Tinycast.Features.WindowManagement;
using Tinycast.Platform;
using Windows.ApplicationModel.Core;
using Windows.Management.Deployment;

namespace Tinycast;

internal static class AppIndex
{
    const int MaxDepth = 6;

    public static List<AppEntry> Scan()
    {
        var entries = new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase);
        var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ScanStore(entries, titles);
        foreach (var root in StartMenuRoots())
            ScanScope(root, entries, titles, 0);
        return entries.Values.ToList();
    }

    static IEnumerable<string> StartMenuRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
    }

    static void ScanScope(string root, Dictionary<string, AppEntry> entries, HashSet<string> titles, int depth)
    {
        if (depth > MaxDepth || string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(root))
                Consider(file, entries, titles);
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                try { ScanScope(dir, entries, titles, depth + 1); }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    static void Consider(string file, Dictionary<string, AppEntry> entries, HashSet<string> titles)
    {
        var ext = Path.GetExtension(file);
        if (!ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
            && !ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            && !ext.Equals(".url", StringComparison.OrdinalIgnoreCase))
            return;
        var name = Path.GetFileNameWithoutExtension(file);
        if (name.Length == 0 || name.Contains("uninstall", StringComparison.OrdinalIgnoreCase))
            return;
        if (!titles.Add(name))
            return;
        var id = "app:" + file.ToLowerInvariant();
        if (entries.ContainsKey(id))
            return;
        var fields = new SearchFields(SearchAlias.Name(name), SearchAlias.Technical(file));
        entries[id] = new AppEntry(
            id, name, AppEntryKind.Application, null, file, "\uE7F4", fields, ShellIcons.FromFile(file));
    }

    static void ScanStore(Dictionary<string, AppEntry> entries, HashSet<string> titles)
    {
        try
        {
            var manager = new PackageManager();
            foreach (var package in manager.FindPackagesForUser(""))
            {
                if (package.IsFramework || package.IsResourcePackage)
                    continue;
                IReadOnlyList<AppListEntry> apps;
                try { apps = package.GetAppListEntriesAsync().AsTask().GetAwaiter().GetResult(); }
                catch (Exception) { continue; }
                if (apps is null || apps.Count == 0)
                    continue;

                foreach (var app in apps)
                {
                    try
                    {
                        var name = app.DisplayInfo.DisplayName;
                        if (string.IsNullOrWhiteSpace(name) || IsRuntimePackage(name))
                            continue;
                        var aumid = app.AppInfo?.AppUserModelId;
                        if (string.IsNullOrWhiteSpace(aumid))
                            continue;
                        if (!titles.Add(name))
                            continue;
                        var id = "store:" + aumid.ToLowerInvariant();
                        if (entries.ContainsKey(id))
                            continue;
                        var logo = ShellIcons.FromAppUserModelId(aumid)
                            ?? ShellIcons.FromLogo(
                                app.DisplayInfo.GetLogo(new Windows.Foundation.Size(256, 256)),
                                aumid);
                        var fields = new SearchFields(
                            SearchAlias.Name(name),
                            SearchAlias.Technical(package.Id.FamilyName));
                        entries[id] = new AppEntry(
                            id, name, AppEntryKind.Application, null, aumid, "\uE7F4", fields, logo);
                    }
                    catch (Exception) { }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Write("Store scan: " + ex.Message);
        }
    }

    static bool IsRuntimePackage(string name) =>
        name.Contains("App Runtime", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Visual C++", StringComparison.OrdinalIgnoreCase)
        || name.Contains("WebView2", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Microsoft.NET.", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Microsoft.WindowsAppRuntime", StringComparison.OrdinalIgnoreCase);
}

internal static class ProcessLauncher
{
    static readonly Dictionary<string, string> ProtocolFallbacks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.WindowsCalculator"] = "calculator:",
        ["Microsoft.WindowsAlarms"] = "ms-clock:",
        ["Microsoft.Windows.Photos"] = "ms-photos:",
        ["Microsoft.WindowsNotepad"] = "notepad:",
        ["Microsoft.Paint"] = "ms-paint:",
        ["Microsoft.ScreenSketch"] = "ms-screenclip:",
        ["Microsoft.WindowsTerminal"] = "ms-terminal:",
        ["Microsoft.MicrosoftStickyNotes"] = "sticky-notes:",
    };

    public static void Launch(AppEntry app)
    {
        if (!string.IsNullOrWhiteSpace(app.Path) && app.Path.Contains('!', StringComparison.Ordinal))
        {
            LaunchAppUserModelId(app.Path);
            return;
        }

        if (!string.IsNullOrWhiteSpace(app.Path))
        {
            Open(app.Path);
            return;
        }

        var colon = app.Id.IndexOf(':');
        if (colon >= 0)
            Open(app.Id[(colon + 1)..]);
    }

    public static void Open(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return;
        if (ShellExecute(target, null))
            return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Write("Open failed " + target + " " + ex.Message);
        }
    }

    public static void OpenUri(string uri) => Open(uri);

    public static void LaunchAppUserModelId(string aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid))
            return;
        if (ActivateAumid(aumid))
            return;
        if (LaunchViaAppList(aumid))
            return;
        if (ShellExecute("explorer.exe", "shell:AppsFolder\\" + aumid))
            return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "shell:AppsFolder\\" + aumid,
                UseShellExecute = true,
            });
            return;
        }
        catch (Exception ex)
        {
            Log.Write("AppsFolder " + aumid + " " + ex.Message);
        }

        var family = aumid.Split('!')[0];
        var packageName = family.Split('_')[0];
        if (ProtocolFallbacks.TryGetValue(packageName, out var protocol))
            Open(protocol);
    }

    static bool ActivateAumid(string aumid)
    {
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"));
            if (type is null)
                return false;
            var mgr = (IApplicationActivationManager)Activator.CreateInstance(type)!;
            var hr = mgr.ActivateApplication(aumid, null, 0, out _);
            if (hr == 0)
                return true;
            Log.Write("ActivateApplication 0x" + hr.ToString("X8") + " " + aumid);
            return false;
        }
        catch (Exception ex)
        {
            Log.Write("AUMID " + aumid + " " + ex.Message);
            return false;
        }
    }

    static bool LaunchViaAppList(string aumid)
    {
        try
        {
            var manager = new PackageManager();
            foreach (var package in manager.FindPackagesForUser(""))
            {
                IReadOnlyList<AppListEntry> apps;
                try { apps = package.GetAppListEntriesAsync().AsTask().GetAwaiter().GetResult(); }
                catch (Exception) { continue; }
                if (apps is null)
                    continue;
                foreach (var app in apps)
                {
                    try
                    {
                        if (!string.Equals(app.AppInfo?.AppUserModelId, aumid, StringComparison.OrdinalIgnoreCase))
                            continue;
                        return app.LaunchAsync().AsTask().GetAwaiter().GetResult();
                    }
                    catch (Exception) { }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Write("AppList launch " + aumid + " " + ex.Message);
        }

        return false;
    }

    static bool ShellExecute(string file, string? arguments)
    {
        var info = new NativeMethods.ShellExecuteInfo
        {
            CbSize = Marshal.SizeOf<NativeMethods.ShellExecuteInfo>(),
            FMask = NativeMethods.SeeMaskNoAsync | NativeMethods.SeeMaskFlagNoUi,
            LpVerb = "open",
            LpFile = file,
            LpParameters = arguments,
            NShow = NativeMethods.SwShownormal,
        };
        try
        {
            if (NativeMethods.ShellExecuteEx(ref info))
                return true;
            Log.Write("ShellExecuteEx " + file + " " + Marshal.GetLastWin32Error());
            return false;
        }
        catch (Exception ex)
        {
            Log.Write("ShellExecuteEx " + file + " " + ex.Message);
            return false;
        }
    }
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("2e68d00c-f383-44e3-a1c0-1cedc5de4aaa")]
interface IApplicationActivationManager
{
    [PreserveSig]
    int ActivateApplication(
        [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
        [MarshalAs(UnmanagedType.LPWStr)] string? arguments,
        int options,
        out uint processId);
}

internal static class Paster
{
    public const string InternalFormat = "TinycastClipboardIgnore";

    public static void FocusTarget(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;
        if (NativeMethods.IsIconic(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeMethods.SwRestore);
        var current = NativeMethods.GetCurrentThreadId();
        var target = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        var attached = false;
        if (target != 0 && target != current)
            attached = NativeMethods.AttachThreadInput(current, target, true);
        NativeMethods.AllowSetForegroundWindow(uint.MaxValue);
        NativeMethods.SetForegroundWindow(hwnd);
        if (attached)
            NativeMethods.AttachThreadInput(current, target, false);
    }

    public static void PasteText(string text, IntPtr previousHwnd)
    {
        FocusTarget(previousHwnd);
        NativeMethods.SendUnicode(text);
    }

    public static void MoveCaretLeft(int count)
    {
        for (var i = 0; i < count; i++)
            NativeMethods.SendVk(NativeMethods.VkLeft);
    }

    public static void SendCtrlV(IntPtr previousHwnd)
    {
        FocusTarget(previousHwnd);
        Chord((byte)NativeMethods.VkControl, 0x56);
    }

    public static void SendCtrlC(IntPtr previousHwnd)
    {
        FocusTarget(previousHwnd);
        Chord((byte)NativeMethods.VkControl, 0x43);
    }

    static void Chord(byte modifier, byte key)
    {
        NativeMethods.keybd_event(modifier, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(key, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(key, 0, NativeMethods.KeyeventfKeyUp, UIntPtr.Zero);
        NativeMethods.keybd_event(modifier, 0, NativeMethods.KeyeventfKeyUp, UIntPtr.Zero);
    }

    public static string? ForegroundProcessPath(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            hwnd = NativeMethods.GetForegroundWindow();
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        try
        {
            var process = Process.GetProcessById((int)pid);
            return process.MainModule?.FileName ?? process.ProcessName;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

internal static class WindowInventory
{
    public static IReadOnlyList<Tinycast.Features.FileSearch.WindowSwitchEntry> Enumerate()
    {
        var list = new List<Tinycast.Features.FileSearch.WindowSwitchEntry>();
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd))
                return true;
            var title = new StringBuilder(512);
            if (NativeMethods.GetWindowText(hwnd, title, title.Capacity) <= 0)
                return true;
            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            var name = "";
            string? path = null;
            try
            {
                var process = Process.GetProcessById((int)pid);
                name = process.ProcessName;
                path = process.MainModule?.FileName;
            }
            catch (Exception) { }
            if (name.Equals("Tinycast", StringComparison.OrdinalIgnoreCase))
                return true;
            list.Add(new(hwnd, title.ToString(), name, NativeMethods.IsIconic(hwnd), path));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public static IReadOnlyList<ScreenSpec> Screens()
    {
        var screens = new List<ScreenSpec>();
        var id = 0;
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref NativeMethods.Rect rect, IntPtr data) =>
        {
            var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() };
            if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                screens.Add(new ScreenSpec(
                    id++,
                    new RectD(info.Monitor.Left, info.Monitor.Top, info.Monitor.Width, info.Monitor.Height),
                    new RectD(info.Work.Left, info.Work.Top, info.Work.Width, info.Work.Height)));
            }

            return true;
        }, IntPtr.Zero);
        return screens;
    }

    public static void Focus(IntPtr hwnd)
    {
        if (NativeMethods.IsIconic(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeMethods.SwRestore);
        NativeMethods.SetForegroundWindow(hwnd);
    }

    public static void Place(IntPtr hwnd, RectD frame)
    {
        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            (int)Math.Round(frame.X),
            (int)Math.Round(frame.Y),
            (int)Math.Round(frame.Width),
            (int)Math.Round(frame.Height),
            NativeMethods.SwpNoZOrder | NativeMethods.SwpShowWindow);
    }

    public static RectD Frame(IntPtr hwnd)
    {
        NativeMethods.GetWindowRect(hwnd, out var rect);
        return new RectD(rect.Left, rect.Top, rect.Width, rect.Height);
    }
}
