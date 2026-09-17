using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Relay.Features.Ai;

namespace Relay.Platform;

/// <summary>
/// Spawns and probes a local OpenCode server, or connects to an external one.
/// Mirrors apps/server/src/provider/opencodeRuntime.ts
/// (startOpenCodeServerProcess, connectToOpenCodeServer,
/// verifyOpenCodeServerVersion, runOpenCodeCommand --version).
/// </summary>
public sealed record OpenCodeServerHandle(
    string Url,
    string? ServerPassword,
    string Version,
    Process? Process,
    bool External);

public sealed class OpenCodeException(string operation, string detail, Exception? inner = null)
    : Exception($"{operation}: {detail}", inner)
{
    public string Operation { get; } = operation;
    public string Detail { get; } = detail;
}

internal static class OpenCodeServerManager
{
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static string ResolveBinaryPath(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();
        return OpenCodeConstants.DefaultBinaryPath;
    }

    public static int FindAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static string? FindOnPath(string binary)
    {
        try
        {
            binary = binary.Trim();
            if (binary.Length == 0 || Path.IsPathRooted(binary) || binary != Path.GetFileName(binary))
                return null;
            var extension = Path.GetExtension(binary);
            if (extension.Length != 0 && !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
                return null;
            var baseName = Path.GetFileNameWithoutExtension(binary);
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try
                {
                    var trimmed = dir.Trim().Trim('"');
                    if (string.IsNullOrEmpty(trimmed) || !Directory.Exists(trimmed))
                        continue;
                    var exe = Path.Combine(trimmed, baseName + ".exe");
                    if (File.Exists(exe))
                        return exe;
                    var cmd = Path.Combine(trimmed, baseName + ".cmd");
                    if (File.Exists(cmd) && ResolveShimTarget(cmd) is { } target)
                        return target;
                }
                catch (Exception) { }
            }

            foreach (var wellKnown in WellKnownExeLocations(baseName))
            {
                if (File.Exists(wellKnown))
                    return wellKnown;
            }
        }
        catch (Exception) { }
        return null;
    }

    static IEnumerable<string> WellKnownExeLocations(string baseName)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(userProfile, ".opencode", "bin", baseName + ".exe");
        yield return Path.Combine(localAppData, "hermes", "node", "node_modules", "opencode-ai", "bin", baseName + ".exe");
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", baseName + ".exe");
    }

    static string? ResolveShimTarget(string cmdPath)
    {
        try
        {
            if (new FileInfo(cmdPath).Length > 16 * 1024)
                return null;
            var dir = Path.GetDirectoryName(Path.GetFullPath(cmdPath))!;
            string? target = null;
            var dp0Defined = false;
            foreach (var raw in File.ReadLines(cmdPath))
            {
                var line = raw.Trim();
                if (line.StartsWith('@'))
                    line = line[1..];
                if (line.Length == 0 || line.Equals("echo off", StringComparison.OrdinalIgnoreCase)
                    || line.Equals("setlocal", StringComparison.OrdinalIgnoreCase)
                    || line.Equals("endlocal", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (target is null && (line.Equals("set \"dp0=%~dp0\"", StringComparison.OrdinalIgnoreCase)
                    || line.Equals("set dp0=%~dp0", StringComparison.OrdinalIgnoreCase)))
                {
                    dp0Defined = true;
                    continue;
                }
                if (target is not null || !line.StartsWith('"'))
                    return null;
                var end = line.IndexOf('"', 1);
                if (end < 0 || line[(end + 1)..].Trim() != "%*")
                    return null;
                var candidate = line[1..end];
                var prefix = candidate.StartsWith("%~dp0", StringComparison.OrdinalIgnoreCase) ? "%~dp0"
                    : dp0Defined && candidate.StartsWith("%dp0%", StringComparison.OrdinalIgnoreCase) ? "%dp0%" : null;
                if (prefix is null)
                    return null;
                var relative = candidate[prefix.Length..].TrimStart('\\', '/');
                if (relative.IndexOfAny(['%', '!', '^', ':', '"']) >= 0 || Path.IsPathRooted(relative))
                    return null;
                target = Path.GetFullPath(Path.Combine(dir, relative));
                if (!target.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || !Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(target))
                    return null;
            }
            return target;
        }
        catch (Exception) { }
        return null;
    }

    static ProcessStartInfo BuildSpawn(string binaryPath, IReadOnlyList<string> arguments, string? workingDirectory)
    {
        var resolved = ResolveExecutable(binaryPath)
            ?? throw new OpenCodeException("startOpenCodeServerProcess",
                $"Couldn't resolve an executable OpenCode CLI at '{binaryPath}'. "
                + "Set the full path to opencode.exe (or a resolvable opencode.cmd shim) in Settings, or install it on PATH.");
        var startInfo = new ProcessStartInfo
        {
            FileName = resolved,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? "",
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
        return startInfo;
    }

    static string? ResolveExecutable(string binaryPath)
    {
        if (Path.IsPathRooted(binaryPath) || binaryPath != Path.GetFileName(binaryPath))
        {
            if (File.Exists(binaryPath))
                return NormalizeExecutable(binaryPath);
            return null;
        }

        var fileName = Path.GetFileName(binaryPath.Trim());
        if (File.Exists(binaryPath))
            return NormalizeExecutable(binaryPath);
        return FindOnPath(fileName);
    }

    static string? NormalizeExecutable(string path)
    {
        var extension = Path.GetExtension(path);
        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(path);
        if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
            return ResolveShimTarget(path);
        return null;
    }

    public static async Task<string?> ProbeCliVersionAsync(string binaryPath, CancellationToken token = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(OpenCodeConstants.VersionProbeTimeout);
            var psi = BuildSpawn(ResolveBinaryPath(binaryPath), ["--version"], null);
            using var proc = Process.Start(psi);
            if (proc is null)
                return null;
            try
            {
                var stdout = proc.StandardOutput.ReadToEndAsync(cts.Token);
                var stderr = proc.StandardError.ReadToEndAsync(cts.Token);
                await Task.WhenAll(stdout, stderr, proc.WaitForExitAsync(cts.Token)).ConfigureAwait(false);
                return proc.ExitCode == 0 ? OpenCodeInventory.ParseGenericCliVersion(await stdout.ConfigureAwait(false)) : null;
            }
            finally
            {
                try
                {
                    if (!proc.HasExited)
                        proc.Kill(entireProcessTree: true);
                }
                catch (Exception) { }
            }
        }
        catch (Exception ex)
        {
            Log.Write("opencode version probe: " + ex.Message);
            return null;
        }
    }

    public static async Task<OpenCodeServerHandle> StartLocalAsync(
        string binaryPath,
        string directory,
        string? configuredPassword,
        IReadOnlyDictionary<string, string?>? instanceEnv = null,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        binaryPath = ResolveBinaryPath(binaryPath);
        var port = FindAvailablePort();
        var password = OpenCodeInventory.ResolveServerPassword(
            external: false, configuredPassword,
            instanceEnv, EnvSnapshot());
        var configContent = OpenCodeInventory.ResolveConfigContent(instanceEnv, EnvSnapshot());

        var psi = BuildSpawn(binaryPath,
            ["serve", $"--hostname={OpenCodeConstants.DefaultHostname}", $"--port={port}"],
            Directory.Exists(directory) ? directory : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        if (instanceEnv is not null)
        {
            foreach (var (k, v) in instanceEnv)
                if (v is not null)
                    psi.Environment[k] = v;
        }
        if (!string.IsNullOrEmpty(password))
            psi.Environment["OPENCODE_SERVER_PASSWORD"] = password;
        psi.Environment["OPENCODE_CONFIG_CONTENT"] = configContent;

        Process? proc;
        try
        {
            proc = Process.Start(psi);
        }
        catch (Exception ex)
        {
            throw new OpenCodeException("startOpenCodeServerProcess",
                "OpenCode CLI (opencode) is not installed or not on PATH. Install it, then run `opencode auth login`.", ex);
        }

        if (proc is null)
            throw new OpenCodeException("startOpenCodeServerProcess",
                "OpenCode CLI (opencode) is not installed or not on PATH. Install it, then run `opencode auth login`.");

        var transferred = false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(OpenCodeConstants.ServerStartTimeout);
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var urlFound = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var outputEnded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var errorEnded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    outputEnded.TrySetResult();
                    return;
                }
                lock (stdout)
                {
                    stdout.AppendLine(e.Data);
                    if (stdout.Length > OpenCodeConstants.ServerStartupMaxOutputChars)
                        stdout.Remove(0, stdout.Length - OpenCodeConstants.ServerStartupMaxOutputChars);
                    var url = OpenCodeInventory.ParseServerUrlFromOutput(stdout.ToString());
                    if (url is not null)
                        urlFound.TrySetResult(url);
                }
            };
            proc.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    errorEnded.TrySetResult();
                    return;
                }
                lock (stderr)
                {
                    stderr.AppendLine(e.Data);
                    if (stderr.Length > OpenCodeConstants.ServerStartupMaxOutputChars)
                        stderr.Remove(0, stderr.Length - OpenCodeConstants.ServerStartupMaxOutputChars);
                }
            };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var exitWatch = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            var exited = proc.WaitForExitAsync(exitWatch.Token);
            try
            {
                var completed = await Task.WhenAny(urlFound.Task, exited).WaitAsync(timeout.Token).ConfigureAwait(false);
                if (completed == exited)
                {
                    await exited.ConfigureAwait(false);
                    await Task.WhenAll(outputEnded.Task, errorEnded.Task).WaitAsync(timeout.Token).ConfigureAwait(false);
                    string so, se;
                    lock (stdout) so = stdout.ToString().Trim();
                    lock (stderr) se = stderr.ToString().Trim();
                    throw new OpenCodeException("startOpenCodeServerProcess",
                        $"OpenCode server exited before startup completed (code: {proc.ExitCode})."
                        + (so.Length > 0 ? $"\n\nstdout:\n{so}" : "")
                        + (se.Length > 0 ? $"\n\nstderr:\n{se}" : ""));
                }

                var url = await urlFound.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
                var version = await VerifyHealthAsync(url, password, timeout.Token).ConfigureAwait(false);
                timeout.Token.ThrowIfCancellationRequested();
                if (proc.HasExited)
                    throw new OpenCodeException("startOpenCodeServerProcess", "OpenCode server exited during its health check.");
                transferred = true;
                return new OpenCodeServerHandle(url, password, version, proc, External: false);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested && timeout.IsCancellationRequested)
            {
                throw new OpenCodeException("startOpenCodeServerProcess",
                    $"Timed out waiting for OpenCode server start after {(int)OpenCodeConstants.ServerStartTimeout.TotalMilliseconds}ms.");
            }
            finally
            {
                exitWatch.Cancel();
                try { await exited.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
        }
        finally
        {
            if (!transferred)
                Kill(proc);
        }
    }

    public static async Task<OpenCodeServerHandle> ConnectExternalAsync(
        string serverUrl,
        string? configuredPassword,
        CancellationToken token = default)
    {
        serverUrl = serverUrl.Trim().TrimEnd('/');
        var password = OpenCodeInventory.ResolveServerPassword(
            external: true, configuredPassword);
        var version = await VerifyHealthAsync(serverUrl, password, token);
        return new OpenCodeServerHandle(serverUrl, password, version, Process: null, External: true);
    }

    public static async Task<string> VerifyHealthAsync(
        string baseUrl, string? serverPassword, CancellationToken token = default)
    {
        try
        {
            using var client = BuildHttp(serverPassword, OpenCodeConstants.HealthTimeout);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(OpenCodeConstants.HealthTimeout);
            using var response = await client.GetAsync(baseUrl.TrimEnd('/') + "/global/health", cts.Token);
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                throw new OpenCodeException("global.health",
                    "OpenCode server rejected authentication. Check the server URL and password.");
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(cts.Token);
            using var doc = JsonDocument.Parse(body);
            var healthy = doc.RootElement.TryGetProperty("healthy", out var h) && h.GetBoolean();
            var version = doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() : null;
            if (!healthy || string.IsNullOrWhiteSpace(version) || OpenCodeInventory.TryParseSemver(version) is null)
                throw new OpenCodeException("global.health",
                    $"OpenCode server returned an invalid health response. Requires v{OpenCodeConstants.MinimumVersion} or newer.");
            if (!OpenCodeInventory.IsVersionSupported(version))
                throw new OpenCodeException("global.health",
                    $"OpenCode v{version} is too old. Upgrade to v{OpenCodeConstants.MinimumVersion} or newer.");
            return version;
        }
        catch (OpenCodeException)
        {
            throw;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new OpenCodeException("global.health",
                $"Couldn't reach the configured OpenCode server at {baseUrl}. Check the URL, then refresh provider status.", ex);
        }
    }

    public static HttpClient BuildHttp(string? serverPassword, TimeSpan timeout)
    {
        var client = new HttpClient(new SocketsHttpHandler { UseCookies = false }) { Timeout = timeout };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Relay/0.1");
        if (!string.IsNullOrEmpty(serverPassword))
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "Authorization", OpenCodeInventory.BasicAuthHeader(serverPassword));
        return client;
    }

    public static void Kill(Process? proc)
    {
        if (proc is null)
            return;
        try
        {
            if (!proc.HasExited)
                proc.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            Log.Write("opencode kill: " + ex.Message);
        }
        finally
        {
            proc.Dispose();
        }
    }

    static Dictionary<string, string?> EnvSnapshot()
    {
        var snapshot = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string k)
                snapshot[k] = entry.Value as string;
        }

        return snapshot;
    }
}
