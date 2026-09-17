using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Tinycast.Features.Ai;

namespace Tinycast.Platform;

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
            var baseName = Path.GetFileNameWithoutExtension(binary.Trim());
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = "opencode";
            // Prefer a real Win32 .exe: direct spawn with UseShellExecute=false
            // cannot run .cmd shims or extensionless sh scripts (hermes/node
            // installs put those on PATH). T3's resolveSpawnCommand uses a
            // shell on Windows for the same reason.
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

    /// <summary>
    /// Follows a .cmd shim to its real exe (hermes pattern:
    /// `"%dp0%\node_modules\opencode-ai\bin\opencode.exe" %*`).
    /// </summary>
    static string? ResolveShimTarget(string cmdPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(cmdPath);
            if (dir is null)
                return null;
            foreach (var line in File.ReadLines(cmdPath))
            {
                var quoteStart = line.IndexOf('"');
                while (quoteStart >= 0)
                {
                    var quoteEnd = line.IndexOf('"', quoteStart + 1);
                    if (quoteEnd <= quoteStart)
                        break;
                    var candidate = line[(quoteStart + 1)..quoteEnd];
                    candidate = candidate.Replace("%~dp0", dir + Path.DirectorySeparatorChar)
                        .Replace("%dp0%", dir + Path.DirectorySeparatorChar)
                        .Replace("%DP0%", dir + Path.DirectorySeparatorChar);
                    candidate = Environment.ExpandEnvironmentVariables(candidate).Trim();
                    if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        && File.Exists(candidate))
                        return candidate;
                    quoteStart = line.IndexOf('"', quoteEnd + 1);
                }
            }

            var siblingExe = Path.Combine(dir, Path.GetFileNameWithoutExtension(cmdPath) + ".exe");
            if (File.Exists(siblingExe))
                return siblingExe;
        }
        catch (Exception) { }
        return null;
    }

    /// <summary>
    /// Builds a spawn that works for real exes and for .cmd/sh shims alike.
    /// Shims go via cmd.exe /c (T3 shell:true parity); real exes run direct.
    /// </summary>
    static ProcessStartInfo BuildSpawn(string binaryPath, string arguments, string? workingDirectory)
    {
        var resolved = binaryPath;
        if (!Path.IsPathRooted(resolved) || !File.Exists(resolved))
            resolved = FindOnPath(Path.GetFileNameWithoutExtension(resolved)) ?? resolved;

        if (resolved.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(resolved))
        {
            return new ProcessStartInfo(resolved, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? "",
            };
        }

        if (File.Exists(resolved) && !IsPortableExecutable(resolved))
        {
            return new ProcessStartInfo("cmd.exe", $"/c \"\"{resolved}\" {arguments}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? "",
            };
        }

        // Bare command: let cmd.exe resolve via PATHEXT (.cmd/.bat/.ps1 shims).
        if (!resolved.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !File.Exists(resolved))
        {
            return new ProcessStartInfo("cmd.exe", $"/c \"\"{binaryPath.Trim()}\" {arguments}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? "",
            };
        }

        return new ProcessStartInfo(resolved, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? "",
        };
    }

    static bool IsPortableExecutable(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[2];
            return stream.Read(header) == 2 && header[0] == 'M' && header[1] == 'Z';
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static async Task<string?> ProbeCliVersionAsync(string binaryPath, CancellationToken token = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(OpenCodeConstants.VersionProbeTimeout);
            var psi = BuildSpawn(ResolveBinaryPath(binaryPath), "--version", null);
            using var proc = Process.Start(psi);
            if (proc is null)
                return null;
            try
            {
                var stdout = await proc.StandardOutput.ReadToEndAsync(cts.Token);
                try { await proc.WaitForExitAsync(cts.Token); } catch (OperationCanceledException) { }
                return OpenCodeInventory.ParseGenericCliVersion(stdout);
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
        binaryPath = ResolveBinaryPath(binaryPath);
        var port = FindAvailablePort();
        var password = OpenCodeInventory.ResolveServerPassword(
            external: false, configuredPassword,
            instanceEnv, EnvSnapshot());
        var configContent = OpenCodeInventory.ResolveConfigContent(instanceEnv, EnvSnapshot());

        var psi = BuildSpawn(binaryPath,
            $"serve --hostname={OpenCodeConstants.DefaultHostname} --port={port}",
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

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var urlFound = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
                return;
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
                return;
            lock (stderr)
            {
                stderr.AppendLine(e.Data);
                if (stderr.Length > OpenCodeConstants.ServerStartupMaxOutputChars)
                    stderr.Remove(0, stderr.Length - OpenCodeConstants.ServerStartupMaxOutputChars);
            }
        };
        proc.EnableRaisingEvents = true;
        proc.Exited += (_, _) => exited.TrySetResult(proc.HasExited ? proc.ExitCode : -1);
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(OpenCodeConstants.ServerStartTimeout);
        await using var _ = timeout.Token.Register(() =>
        {
            urlFound.TrySetCanceled();
            exited.TrySetCanceled();
        });

        var completed = await Task.WhenAny(urlFound.Task, exited.Task);
        if (completed == exited.Task)
        {
            var code = await exited.Task;
            string so, se;
            lock (stdout) so = stdout.ToString().Trim();
            lock (stderr) se = stderr.ToString().Trim();
            Kill(proc);
            var detail = $"OpenCode server exited before startup completed (code: {code})."
                + (so.Length > 0 ? $"\n\nstdout:\n{so}" : "")
                + (se.Length > 0 ? $"\n\nstderr:\n{se}" : "");
            throw new OpenCodeException("startOpenCodeServerProcess", detail);
        }

        string url;
        try
        {
            url = await urlFound.Task;
        }
        catch (OperationCanceledException)
        {
            Kill(proc);
            throw new OpenCodeException("startOpenCodeServerProcess",
                $"Timed out waiting for OpenCode server start after {(int)OpenCodeConstants.ServerStartTimeout.TotalMilliseconds}ms.");
        }

        // Keep draining (BeginOutputReadLine stays attached until Kill/Dispose).
        var version = await VerifyHealthAsync(url, password, token);
        return new OpenCodeServerHandle(url, password, version, proc, External: false);
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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new OpenCodeException("global.health",
                $"Couldn't reach the configured OpenCode server at {baseUrl}. Check the URL, then refresh provider status.", ex);
        }
    }

    public static HttpClient BuildHttp(string? serverPassword, TimeSpan timeout)
    {
        var client = new HttpClient(new SocketsHttpHandler { UseCookies = false }) { Timeout = timeout };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Tinycast/0.1");
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
