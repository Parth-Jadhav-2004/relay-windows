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
    public const string TokenKey = "github-updates";

    public static string Repository => Owner + "/" + Repo;
    public static string ReleasesApi => "https://api.github.com/repos/" + Repository + "/releases/latest";
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
        using var client = CreateClient();
        using var response = await client.GetAsync(ReleasesApi);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException(MissingReleaseMessage());
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized
            || response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new InvalidOperationException("GitHub refused the request. Add a token with repo access in Settings → About.");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var version = UpdateRelease.ParseTag(tag)
            ?? throw new InvalidOperationException("Release tag is not a version: " + tag);
        var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? tag : tag;
        var notes = UpdateRelease.NotesSummary(root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() : null);
        if (!root.TryGetProperty("assets", out var assets) || assets.GetArrayLength() == 0)
            throw new InvalidOperationException("Release " + tag + " has no zip assets.");

        var names = new List<(string Name, long Id, long Size)>();
        foreach (var asset in assets.EnumerateArray())
        {
            var assetName = asset.GetProperty("name").GetString();
            if (string.IsNullOrWhiteSpace(assetName))
                continue;
            names.Add((assetName, asset.GetProperty("id").GetInt64(), asset.TryGetProperty("size", out var size) ? size.GetInt64() : 0));
        }

        var pick = UpdateRelease.PickAsset(names.Select(n => n.Name), RuntimeInformation.ProcessArchitecture)
            ?? throw new InvalidOperationException("Release " + tag + " has no Windows zip for this PC.");
        var chosen = names.First(n => n.Name.Equals(pick, StringComparison.OrdinalIgnoreCase));
        return new GitHubRelease(tag, name, notes, version, chosen.Name, chosen.Id, chosen.Size);
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
        var exe = Path.Combine(dest, "Tinycast.exe");
        var bat = Path.Combine(AppPaths.UpdatesDir, "apply.cmd");
        Directory.CreateDirectory(AppPaths.UpdatesDir);
        File.WriteAllText(bat, """
            @echo off
            setlocal
            set "SRC=%~1"
            set "DST=%~2"
            set "EXE=%~3"
            :wait
            timeout /t 1 /nobreak >nul
            tasklist /FI "IMAGENAME eq Tinycast.exe" | findstr /I "Tinycast.exe" >nul
            if not errorlevel 1 goto wait
            robocopy "%SRC%" "%DST%" /E /IS /IT /R:4 /W:1 /NFL /NDL /NJH /NJS
            start "" "%EXE%"
            """);
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c start \"\" /min \"" + bat + "\" \"" + payloadDir + "\" \"" + dest + "\" \"" + exe + "\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }

    static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
        {
            Timeout = TimeSpan.FromMinutes(5),
        };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Tinycast-Windows/" + InstalledLabel);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        var token = CredentialStore.Get(TokenKey);
        if (!string.IsNullOrWhiteSpace(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    static string MissingReleaseMessage() =>
        string.IsNullOrWhiteSpace(CredentialStore.Get(TokenKey))
            ? "No release found. This private repo needs a GitHub token in Settings → About."
            : "No Windows release is published yet. Installed " + InstalledLabel + ".";

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
