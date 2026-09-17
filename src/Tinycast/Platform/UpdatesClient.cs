using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Tinycast.Features.Updates;

namespace Tinycast.Platform;

internal sealed record GitHubRelease(
    string Tag,
    string Name,
    string Notes,
    Version Version,
    string AssetName,
    long AssetId,
    long Size);

internal static class UpdatesClient
{
    public const string Owner = "Parth-Jadhav-2004";
    public const string Repo = "tinycast-windows";

    public static string Repository => Owner + "/" + Repo;
    public static string ReleasesApi => "https://api.github.com/repos/" + Repository + "/releases";
    public static string RepoApi => "https://api.github.com/repos/" + Repository;
    public static string ReleasesPage => "https://github.com/" + Repository + "/releases";

    public static Version Installed
    {
        get
        {
            var version = typeof(UpdatesClient).Assembly.GetName().Version;
            return version is null || version.Major == 0 && version.Minor == 0
                ? new Version(0, 1, 0)
                : new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
        }
    }

    public static string InstalledLabel =>
        typeof(UpdatesClient).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Installed.ToString();

    public static async Task<GitHubRelease> FetchLatestAsync()
    {
        if (string.IsNullOrWhiteSpace(GitHubAuth.Token))
            throw new InvalidOperationException(GitHubAuth.MissingTokenMessage());

        using var client = CreateClient();
        await EnsureRepoVisible(client);

        using var response = await client.GetAsync(ReleasesApi + "?per_page=30");
        if (!response.IsSuccessStatusCode)
        {
            Log.Write("update releases HTTP " + (int)response.StatusCode);
            throw new InvalidOperationException(DescribeHttpFailure(response.StatusCode, "list releases"));
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("GitHub returned an unexpected releases payload.");

        var listings = new List<UpdateRelease.ReleaseListing>();
        var details = new Dictionary<string, ParsedRelease>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var parsed = ParseRelease(item);
            if (parsed is null)
                continue;
            listings.Add(new UpdateRelease.ReleaseListing(
                parsed.Tag, parsed.Version, parsed.Draft, parsed.Prerelease,
                parsed.Assets.Select(a => a.Name).ToList()));
            details[parsed.Tag] = parsed;
        }

        var selected = UpdateRelease.SelectLatest(listings, RuntimeInformation.ProcessArchitecture)
            ?? throw new InvalidOperationException("No Windows release is published yet. Installed " + InstalledLabel + ".");
        var latest = details[selected.Tag];
        var pick = UpdateRelease.PickAsset(latest.Assets.Select(a => a.Name), RuntimeInformation.ProcessArchitecture)!;
        var chosen = latest.Assets.First(a => a.Name.Equals(pick, StringComparison.OrdinalIgnoreCase));
        return new GitHubRelease(latest.Tag, latest.Name, latest.Notes, latest.Version, chosen.Name, chosen.Id, chosen.Size);
    }

    public static async Task<string> DownloadAsync(GitHubRelease release, CancellationToken token = default)
    {
        AppPaths.EnsureRoot();
        var stagingRoot = Path.Combine(AppPaths.UpdatesDir, release.Version.ToString());
        if (Directory.Exists(stagingRoot))
            Directory.Delete(stagingRoot, true);
        Directory.CreateDirectory(stagingRoot);
        var zipPath = Path.Combine(AppPaths.UpdatesDir, release.AssetName);
        using var client = CreateClient();
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        var url = "https://api.github.com/repos/" + Repository + "/releases/assets/" + release.AssetId;
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using (var input = await response.Content.ReadAsStreamAsync(token))
        await using (var output = File.Create(zipPath))
            await input.CopyToAsync(output, token);

        ExtractZip(zipPath, stagingRoot);
        try { File.Delete(zipPath); } catch (Exception) { }
        return ResolvePayload(stagingRoot);
    }

    public static void LaunchInstaller(string payloadDir)
    {
        var dest = Path.GetDirectoryName(Environment.ProcessPath);
        if (string.IsNullOrWhiteSpace(dest))
            throw new InvalidOperationException("Cannot locate the install folder.");

        dest = dest.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        payloadDir = Path.GetFullPath(payloadDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var exe = Path.Combine(dest, "Tinycast.exe");
        Directory.CreateDirectory(AppPaths.UpdatesDir);
        var bat = Path.Combine(AppPaths.UpdatesDir, "apply.cmd");
        var log = Path.Combine(AppPaths.UpdatesDir, "apply.log");
        var src = CmdLiteral(payloadDir);
        var dst = CmdLiteral(dest);
        var exeLit = CmdLiteral(exe);
        var logLit = CmdLiteral(log);
        File.WriteAllText(bat, $"""
            @echo off
            setlocal EnableExtensions
            set "SRC={src}"
            set "DST={dst}"
            set "EXE={exeLit}"
            >"{logLit}" echo apply %DATE% %TIME%
            >>"{logLit}" echo SRC=%SRC%
            >>"{logLit}" echo DST=%DST%
            >>"{logLit}" echo EXE=%EXE%
            if not exist "%SRC%\Tinycast.exe" (
              >>"{logLit}" echo missing payload
              exit /b 1
            )
            :wait
            ping -n 2 127.0.0.1 >nul
            tasklist /FI "IMAGENAME eq Tinycast.exe" | findstr /I /C:"Tinycast.exe" >nul
            if not errorlevel 1 goto wait
            robocopy "%SRC%" "%DST%" /E /IS /IT /R:4 /W:1 /NFL /NDL /NJH /NJS
            >>"{logLit}" echo robocopy=%ERRORLEVEL%
            if not exist "%EXE%" (
              >>"{logLit}" echo missing exe
              exit /b 1
            )
            start "" /D "%DST%" "%EXE%"
            exit /b 0
            """);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/d /c start \"TinycastUpdate\" /min cmd.exe /d /c \"" + bat + "\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppPaths.UpdatesDir,
        });
    }

    static string CmdLiteral(string path) =>
        path.Replace("\"", "", StringComparison.Ordinal)
            .TrimEnd('\\');

    static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = TimeSpan.FromMinutes(5),
        };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Tinycast-Windows/" + InstalledLabel);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        var token = GitHubAuth.Token;
        if (!string.IsNullOrWhiteSpace(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    static async Task EnsureRepoVisible(HttpClient client)
    {
        using var response = await client.GetAsync(RepoApi);
        if (response.IsSuccessStatusCode)
            return;
        Log.Write("update repo HTTP " + (int)response.StatusCode);
        throw new InvalidOperationException(DescribeHttpFailure(response.StatusCode, "open " + Repository));
    }

    static string DescribeHttpFailure(System.Net.HttpStatusCode status, string action) => status switch
    {
        System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
            "GitHub refused the token while trying to " + action + ". Check " + GitHubToken.EnvName + " in " + GitHubAuth.EnvFilePath + ".",
        System.Net.HttpStatusCode.NotFound =>
            "GitHub cannot see " + Repository + " with this token. Use a classic PAT with repo scope, or a fine-grained token that includes this private repo.",
        _ => "GitHub " + action + " failed (HTTP " + (int)status + ").",
    };

    sealed record ParsedRelease(
        string Tag,
        string Name,
        string Notes,
        Version Version,
        bool Draft,
        bool Prerelease,
        List<(string Name, long Id, long Size)> Assets);

    static ParsedRelease? ParseRelease(JsonElement root)
    {
        var tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? "" : "";
        var version = UpdateRelease.ParseTag(tag);
        if (version is null)
            return null;
        var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? tag : tag;
        var notes = UpdateRelease.NotesSummary(root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() : null);
        var draft = root.TryGetProperty("draft", out var draftEl) && draftEl.ValueKind == JsonValueKind.True;
        var prerelease = root.TryGetProperty("prerelease", out var preEl) && preEl.ValueKind == JsonValueKind.True;
        var assets = new List<(string Name, long Id, long Size)>();
        if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assetsEl.EnumerateArray())
            {
                var assetName = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(assetName))
                    continue;
                var id = asset.TryGetProperty("id", out var idEl) && idEl.TryGetInt64(out var assetId) ? assetId : 0;
                var size = asset.TryGetProperty("size", out var sizeEl) && sizeEl.TryGetInt64(out var assetSize) ? assetSize : 0;
                assets.Add((assetName, id, size));
            }
        }

        return new ParsedRelease(tag, name, notes, version, draft, prerelease, assets);
    }

    static void ExtractZip(string zipPath, string dest)
    {
        var root = Path.GetFullPath(dest) + Path.DirectorySeparatorChar;
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith('/'))
            {
                var dir = Path.GetFullPath(Path.Combine(dest, entry.FullName));
                if (!dir.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Zip entry escaped the staging folder.");
                Directory.CreateDirectory(dir);
                continue;
            }

            var path = Path.GetFullPath(Path.Combine(dest, entry.FullName));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Zip entry escaped the staging folder.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            entry.ExtractToFile(path, overwrite: true);
        }
    }

    static string ResolvePayload(string stagingRoot)
    {
        if (File.Exists(Path.Combine(stagingRoot, "Tinycast.exe")))
            return stagingRoot;
        var nested = Directory.GetDirectories(stagingRoot);
        if (nested.Length == 1 && File.Exists(Path.Combine(nested[0], "Tinycast.exe")))
            return nested[0];
        throw new InvalidOperationException("The update zip did not contain Tinycast.exe.");
    }
}
