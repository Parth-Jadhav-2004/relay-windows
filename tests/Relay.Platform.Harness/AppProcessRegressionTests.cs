using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Relay.Features.Launcher;

namespace Relay.Platform.Harness;

internal static class AppProcessRegressionTests
{
    public static int? TryRunChild(string[] args)
    {
        if (args.Length != 2 || args[0] != "--app-process-fixture")
            return null;
        var mode = args[1];
        WindowProc callback = (window, message, w, l) =>
        {
            if (message == 0x0010)
            {
                Console.WriteLine("close");
                if (mode == "ignore")
                    return IntPtr.Zero;
                if (mode == "delay")
                    Thread.Sleep(1200);
                DestroyWindow(window);
                return IntPtr.Zero;
            }
            if (message == 0x8001)
            {
                DestroyWindow(window);
                return IntPtr.Zero;
            }
            if (message == 0x0002)
            {
                PostQuitMessage(0);
                return IntPtr.Zero;
            }
            return DefWindowProc(window, message, w, l);
        };
        var definition = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            Procedure = Marshal.GetFunctionPointerForDelegate(callback),
            Instance = GetModuleHandle(null),
            ClassName = "RelayAppProcessFixture",
        };
        if (RegisterClassEx(ref definition) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        var hwnd = CreateWindowEx(0x08000080, definition.ClassName, "Test-owned window", 0x10000000,
            -32000, -32000, 1, 1, IntPtr.Zero, IntPtr.Zero, definition.Instance, IntPtr.Zero);
        if (hwnd == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (mode == "disabled")
            EnableWindow(hwnd, false);
        _ = Task.Run(() =>
        {
            Console.ReadLine();
            PostMessage(hwnd, 0x8001, IntPtr.Zero, IntPtr.Zero);
        });
        Console.WriteLine("ready");
        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }
        GC.KeepAlive(callback);
        return 0;
    }

    public static async Task<int> RunAsync()
    {
        var failures = 0;
        await Run("resolved full paths distinguish same-named executables", async () =>
        {
            using var first = new Fixture();
            using var other = new Fixture(first.Name);
            var a = await first.StartAsync("delay");
            var b = await other.StartAsync("ignore");
            var launches = 0;
            using var service = new AppProcess(_ =>
            {
                Require(a.HasExited && !b.HasExited, "Replacement preceded exit or matched another path.");
                launches++;
            });
            Require(await service.RestartAsync(first.Entry) == AppProcessResult.Restarted, "Restart failed.");
            Require(launches == 1 && !b.HasExited, "Wrong process was affected.");
            await StopAsync(b);
        });
        await Run("all instances exit before one replacement and aliases cannot queue restarts", async () =>
        {
            using var fixture = new Fixture();
            var first = await fixture.StartAsync("delay");
            var second = await fixture.StartAsync("delay");
            var launches = 0;
            using var service = new AppProcess(_ =>
            {
                Require(first.HasExited && second.HasExited, "Replacement preceded actual exits.");
                launches++;
            });
            var pending = service.RestartAsync(fixture.Entry);
            Require(await service.RestartAsync(fixture.Entry with { Id = "alias", Title = "Another title" }) == AppProcessResult.Busy,
                "Duplicate restart was queued.");
            Require(await pending == AppProcessResult.Restarted && launches == 1, "Expected exactly one replacement.");
        });
        foreach (var mode in new[] { "ignore", "delay", "disabled" })
        {
            await Run($"{mode} close times out without kill or late replacement", async () =>
            {
                using var fixture = new Fixture();
                var child = await fixture.StartAsync(mode);
                var launches = 0;
                using var service = new AppProcess(_ => launches++);
                Require(await service.RestartAsync(fixture.Entry, timeout: TimeSpan.FromMilliseconds(150)) == AppProcessResult.TimedOut,
                    "Expected timeout.");
                Require(!child.HasExited && launches == 0, "Timeout killed or relaunched the process.");
                await StopAsync(child);
                Require(launches == 0, "Late exit launched a replacement.");
            });
        }
        foreach (var cancelMode in new[] { "token", "dispose", "quit" })
        {
            await Run($"{cancelMode} cancels pending replacement", async () =>
            {
                using var fixture = new Fixture();
                var child = await fixture.StartAsync("ignore");
                using var cancellation = new CancellationTokenSource();
                var launches = 0;
                using var service = new AppProcess(_ => launches++);
                var pending = service.RestartAsync(fixture.Entry, cancellation.Token);
                if (cancelMode == "token")
                    cancellation.Cancel();
                else if (cancelMode == "dispose")
                    service.Dispose();
                else
                    await service.QuitAsync(fixture.Entry);
                Require(await pending == AppProcessResult.Cancelled, "Pending restart was not cancelled.");
                Require(!child.HasExited, "Cancellation killed the process.");
                await StopAsync(child);
                Require(launches == 0, "Cancellation launched a replacement.");
            });
        }
        await Run("quit is graceful and never launches", async () =>
        {
            using var fixture = new Fixture();
            var child = await fixture.StartAsync("delay");
            using var service = new AppProcess(_ => throw new InvalidOperationException("Quit launched."));
            Require(await service.QuitAsync(fixture.Entry) == AppProcessResult.Closed && child.HasExited, "Graceful quit failed.");
        });
        await Run("missing, packaged and unsupported identities never guess or launch", async () =>
        {
            using var fixture = new Fixture();
            using var service = new AppProcess(_ => throw new InvalidOperationException("Unexpected launch."));
            foreach (var path in new string?[] { null, "", "Example.Package_123!App", "https://example.invalid", "test.url", "relative.exe" })
                Require(await service.RestartAsync(fixture.Entry with { Path = path, Title = "explorer" }) == AppProcessResult.UnsupportedIdentity,
                    "Unsupported identity was guessed.");
            Require(await service.RestartAsync(fixture.Entry) == AppProcessResult.NotRunning, "Nonrunning app was launched.");
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Require(await service.RestartAsync(fixture.Entry, cancelled.Token) == AppProcessResult.Cancelled, "Pre-cancellation ignored.");
        });
        await Run("shortcut uses target executable rather than shortcut filename", async () =>
        {
            using var fixture = new Fixture();
            var shortcutPath = Path.Combine(fixture.DirectoryPath, "Completely unrelated display name.lnk");
            var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            object? shortcut = null;
            try
            {
                dynamic automation = shell;
                shortcut = automation.CreateShortcut(shortcutPath);
                dynamic link = shortcut;
                link.TargetPath = fixture.Executable;
                link.Save();
            }
            finally
            {
                if (shortcut is not null)
                    Marshal.FinalReleaseComObject(shortcut);
                Marshal.FinalReleaseComObject(shell);
            }
            var entry = fixture.Entry with { Path = shortcutPath };
            Require(AppProcess.ResolveExecutable(entry) == fixture.Executable, "Shortcut did not resolve its executable.");
            var child = await fixture.StartAsync("delay");
            using var service = new AppProcess(_ => { });
            Require(await service.QuitAsync(entry) == AppProcessResult.Closed && child.HasExited, "Shortcut did not match its running target.");
        });
        return failures;

        async Task Run(string name, Func<Task> test)
        {
            try
            {
                await test();
                Console.WriteLine("  ok   app process " + name);
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine(" FAIL  app process " + name + ": " + ex);
            }
        }
    }

    static void Require(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
    }

    static async Task StopAsync(Process child)
    {
        if (!child.HasExited)
            await child.StandardInput.WriteLineAsync("exit");
        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    sealed class Fixture : IDisposable
    {
        readonly List<Process> _children = [];
        public string Name { get; }
        public string DirectoryPath { get; }
        public string Executable { get; }
        public AppEntry Entry => new("fixture:" + DirectoryPath, "Not the executable name", AppEntryKind.Application,
            null, Executable, null, new SearchFields(SearchAlias.Name("fixture")));

        public Fixture(string? name = null)
        {
            Name = name ?? "AppProcessTest_" + Guid.NewGuid().ToString("N");
            DirectoryPath = Path.Combine(Path.GetTempPath(), "Relay-AppProcess-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(DirectoryPath, Path.GetRelativePath(AppContext.BaseDirectory, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
            var apphost = Path.Combine(AppContext.BaseDirectory,
                Path.GetFileNameWithoutExtension(typeof(AppProcessRegressionTests).Assembly.Location) + ".exe");
            Executable = Path.Combine(DirectoryPath, Name + ".exe");
            File.Copy(apphost, Executable);
        }

        public async Task<Process> StartAsync(string mode)
        {
            var info = new ProcessStartInfo(Executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };
            info.ArgumentList.Add("--app-process-fixture");
            info.ArgumentList.Add(mode);
            var child = Process.Start(info)!;
            _children.Add(child);
            Require(await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)) == "ready", "Fixture did not start.");
            return child;
        }

        public void Dispose()
        {
            foreach (var child in _children)
            {
                try
                {
                    if (!child.HasExited)
                    {
                        child.Kill();
                        child.WaitForExit(5000);
                    }
                }
                catch (InvalidOperationException) { }
                finally { child.Dispose(); }
            }
            Directory.Delete(DirectoryPath, true);
        }
    }

    delegate IntPtr WindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WindowClass
    {
        public uint Size;
        public uint Style;
        public IntPtr Procedure;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Message
    {
        public IntPtr Window;
        public uint Id;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X;
        public int Y;
        public uint Private;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    static extern ushort RegisterClassEx(ref WindowClass definition);
    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string title, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")]
    static extern bool EnableWindow(IntPtr window, bool enabled);
    [DllImport("user32.dll")]
    static extern void PostQuitMessage(int code);
    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "GetMessageW")]
    static extern int GetMessage(out Message message, IntPtr window, uint minimum, uint maximum);
    [DllImport("user32.dll")]
    static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    static extern IntPtr DispatchMessage(ref Message message);
}
