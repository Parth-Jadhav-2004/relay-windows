using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Tinycast.Platform;

namespace Tinycast.Platform.Harness;

public static class OpenCodeRegressionTests
{
    const string ModeFile = "OpenCodeRegression.mode";

    public static async Task<int?> TryRunChildAsync(string[] args)
    {
        var modePath = Path.Combine(AppContext.BaseDirectory, ModeFile);
        if (!File.Exists(modePath))
            return null;
        var mode = File.ReadAllText(modePath);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "child.pid"), Environment.ProcessId.ToString());
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "arguments.json"), JsonSerializer.Serialize(args));
        if (args.SequenceEqual(new[] { "--version" }))
        {
            Console.Error.Write(new string('e', 1024 * 1024));
            Console.WriteLine("1.15.13");
            return mode == "version-failure" ? 1 : 0;
        }
        if (mode == "exit")
        {
            Console.Error.WriteLine("fixture startup failure");
            return 23;
        }
        if (mode == "silent")
        {
            await Task.Delay(Timeout.Infinite);
            return 0;
        }
        var port = int.Parse(args.Single(a => a.StartsWith("--port=", StringComparison.Ordinal))[7..]);
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        Console.WriteLine($"opencode server listening on http://127.0.0.1:{port}");
        while (true)
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var reader = new StreamReader(client.GetStream(), leaveOpen: true);
            while (await reader.ReadLineAsync() is { Length: > 0 }) { }
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "health.request"), "received");
            if (mode == "health-stall")
            {
                await Task.Delay(Timeout.Infinite);
                return 0;
            }
            var body = mode == "health-invalid" ? "invalid" : "{\"healthy\":true,\"version\":\"1.15.13\"}";
            var status = mode == "health-failure" ? "503 Service Unavailable" : "200 OK";
            var bytes = Encoding.UTF8.GetBytes($"HTTP/1.1 {status}\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}");
            await client.GetStream().WriteAsync(bytes);
        }
    }

    public static async Task<int> RunAsync()
    {
        var failures = 0;
        await Run("owner shares server and closes only after last disposed-owner borrower releases", async () =>
        {
            using var fixture = new Fixture("healthy");
            using var owner = new OpenCodeServerOwner(fixture.Executable, fixture.DirectoryPath, () => null);
            var firstEntered = new TaskCompletionSource<OpenCodeServerHandle>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondEntered = new TaskCompletionSource<OpenCodeServerHandle>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseSecond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = owner.WithServerAsync(async (server, _) =>
            {
                firstEntered.SetResult(server);
                await releaseFirst.Task;
                return 1;
            });
            Task<int>? second = null;
            try
            {
                Require(File.Exists(fixture.Executable), $"Fixture executable missing: {fixture.Executable}");
                if (await Task.WhenAny(firstEntered.Task, first) == first)
                    await first;
                var shared = await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                second = owner.WithServerAsync(async (server, _) =>
                {
                    secondEntered.SetResult(server);
                    await releaseSecond.Task;
                    return 2;
                });
                Require(ReferenceEquals(shared, await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(10))),
                    "Concurrent borrowers did not share the same server.");
                releaseFirst.SetResult();
                Require(await first == 1 && !shared.Process!.HasExited, "First release closed a borrowed server.");
                await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(owner.Dispose)));
                Require(!shared.Process!.HasExited, "Dispose killed the remaining borrower's server.");
                var rejected = await CaptureAsync(() => owner.WithServerAsync((_, _) => Task.FromResult(3)));
                Require(rejected is ObjectDisposedException, $"Disposed owner accepted a borrow or failed incorrectly: {rejected}");
                releaseSecond.SetResult();
                Require(await second == 2, "Final release changed the consumer result.");
                await fixture.RequireExitedAsync();
            }
            finally
            {
                releaseFirst.TrySetResult();
                releaseSecond.TrySetResult();
                await CaptureAsync(() => first);
                if (second is not null)
                    await CaptureAsync(() => second);
            }
        });
        await Run("version probe drains stderr", async () =>
        {
            using var fixture = new Fixture("version");
            var version = await OpenCodeServerManager.ProbeCliVersionAsync(fixture.Executable);
            Require(version == "1.15.13", $"Expected version after full stderr pipe, got {version ?? "null"}.");
            await fixture.RequireExitedAsync();
        });
        await Run("unsupported shim fails without shell", async () =>
        {
            using var fixture = new Fixture("healthy");
            var shim = Path.Combine(fixture.DirectoryPath, "unsupported.cmd");
            File.WriteAllText(shim, "@exit /b 0\r\n");
            var error = await CaptureAsync(() => OpenCodeServerManager.StartLocalAsync(shim, fixture.DirectoryPath, null));
            Require(error is OpenCodeException && error.Message.Contains("executable", StringComparison.OrdinalIgnoreCase),
                $"Expected clear executable resolution failure, got {error}.");
            Require(!File.Exists(Path.Combine(fixture.DirectoryPath, "child.pid")), "Unsupported shim spawned a process.");
        });
        await Run("explicit missing path never falls back to PATH", async () =>
        {
            using var fixture = new Fixture("version");
            var previous = Environment.GetEnvironmentVariable("PATH");
            try
            {
                Environment.SetEnvironmentVariable("PATH", fixture.DirectoryPath);
                var missing = Path.Combine(fixture.DirectoryPath, "missing", Path.GetFileName(fixture.Executable));
                var version = await OpenCodeServerManager.ProbeCliVersionAsync(missing);
                Require(version is null, "Missing explicit path resolved to an unrelated PATH executable.");
            }
            finally { Environment.SetEnvironmentVariable("PATH", previous); }
        });
        foreach (var mode in new[] { "health-failure", "health-invalid", "health-stall" })
        {
            await Run($"{mode} cleans up child", async () =>
            {
                using var fixture = new Fixture(mode);
                var error = await CaptureAsync(() => OpenCodeServerManager.StartLocalAsync(fixture.Executable, fixture.DirectoryPath, null));
                Require(error is not null, "Startup unexpectedly succeeded.");
                await fixture.RequireExitedAsync();
            });
        }
        foreach (var mode in new[] { "silent", "health-stall" })
        {
            await Run($"cancellation during {mode} cleans up child", async () =>
            {
                using var fixture = new Fixture(mode);
                using var cancel = new CancellationTokenSource();
                var pending = OpenCodeServerManager.StartLocalAsync(fixture.Executable, fixture.DirectoryPath, null, token: cancel.Token);
                await fixture.WaitForFileAsync(mode == "silent" ? "child.pid" : "health.request");
                cancel.Cancel();
                var error = await CaptureAsync(() => pending);
                Require(error is OperationCanceledException, $"Caller cancellation was replaced by {error}.");
                await fixture.RequireExitedAsync();
            });
        }
        return failures;

        async Task Run(string name, Func<Task> test)
        {
            try
            {
                await test().WaitAsync(TimeSpan.FromSeconds(45));
                Console.WriteLine($"PASS OpenCode: {name}");
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"FAIL OpenCode: {name}: {ex}");
            }
        }
    }

    static async Task<Exception?> CaptureAsync(Func<Task> action)
    {
        try { await action(); return null; }
        catch (Exception ex) { return ex; }
    }

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    sealed class Fixture : IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "Tinycast OpenCode " + Guid.NewGuid().ToString("N"));
        public string Executable { get; }

        public Fixture(string mode)
        {
            Directory.CreateDirectory(DirectoryPath);
            foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory))
                File.Copy(file, Path.Combine(DirectoryPath, Path.GetFileName(file)));
            Executable = Path.Combine(DirectoryPath, Path.GetFileName(Environment.ProcessPath!));
            Require(Executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && !Path.GetFileName(Executable).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase),
                "Run the harness apphost executable, not dotnet harness.dll.");
            File.WriteAllText(Path.Combine(DirectoryPath, ModeFile), mode);
        }

        public async Task WaitForFileAsync(string name)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!File.Exists(Path.Combine(DirectoryPath, name)))
                await Task.Delay(20, timeout.Token);
        }

        public async Task RequireExitedAsync()
        {
            await WaitForFileAsync("child.pid");
            var pid = int.Parse(await File.ReadAllTextAsync(Path.Combine(DirectoryPath, "child.pid")));
            try
            {
                using var child = Process.GetProcessById(pid);
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch (ArgumentException) { }
        }

        public void Dispose()
        {
            var pidFile = Path.Combine(DirectoryPath, "child.pid");
            if (File.Exists(pidFile))
            {
                try
                {
                    using var child = Process.GetProcessById(int.Parse(File.ReadAllText(pidFile)));
                    if (!child.HasExited)
                        child.Kill(entireProcessTree: true);
                    child.WaitForExit(3000);
                }
                catch (ArgumentException) { }
            }
            for (var attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    Directory.Delete(DirectoryPath, recursive: true);
                    return;
                }
                catch (IOException) when (attempt < 19) { Thread.Sleep(50); }
                catch (UnauthorizedAccessException) when (attempt < 19) { Thread.Sleep(50); }
            }
        }
    }
}
