using System.IO.Compression;
using System.Text.Json;
using Tinycast.Features.Settings;

namespace Tinycast.Features.Backup;

public sealed record BackupManifest(string Version, DateTime CreatedAt, IReadOnlyList<string> Categories);

public static class BackupArchive
{
    public const string SettingsAndShortcuts = "settings";
    public const string Clipboard = "clipboard";
    public const string Snippets = "snippets";
    public const string Notes = "notes";
    public const string Learning = "learning";
    public const string SchemaVersion = "1";

    public static IReadOnlyList<string> AllCategories { get; } =
        [SettingsAndShortcuts, Clipboard, Snippets, Notes, Learning];

    public static string? IncompatibleReason(BackupManifest? manifest)
    {
        if (manifest is null)
            return "This archive has no Tinycast manifest.";
        if (manifest.Version != SchemaVersion && manifest.Version != "1.0")
            return "This backup was made with a newer Tinycast and cannot be imported.";
        return null;
    }

    public static void Write(
        string zipPath,
        IReadOnlyList<string> categories,
        IReadOnlyDictionary<string, string> settings,
        string? snippetsJson,
        string? notesZipFolder,
        string? rankingJson,
        string? clipboardDbPath)
    {
        zipPath = Path.GetFullPath(zipPath);
        var temporary = Path.Combine(Path.GetDirectoryName(zipPath)!, "." + Path.GetFileName(zipPath) + "." + Guid.NewGuid().ToString("n") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                    WriteContents(zip, categories, settings, snippetsJson, notesZipFolder, rankingJson, clipboardDbPath);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, zipPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    static void WriteContents(
        ZipArchive zip,
        IReadOnlyList<string> categories,
        IReadOnlyDictionary<string, string> settings,
        string? snippetsJson,
        string? notesZipFolder,
        string? rankingJson,
        string? clipboardDbPath)
    {
        var manifest = new BackupManifest(SchemaVersion, DateTime.UtcNow, categories);
        WriteEntry(zip, "manifest.json", JsonSerializer.Serialize(manifest));
        if (categories.Contains(SettingsAndShortcuts))
        {
            var filtered = settings
                .Where(kv => SettingsBackupCoverage.Mirrored.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            WriteEntry(zip, "settings.json", JsonSerializer.Serialize(filtered));
        }

        if (categories.Contains(Snippets) && snippetsJson is not null)
            WriteEntry(zip, "snippets.json", snippetsJson);
        if (categories.Contains(Learning) && rankingJson is not null)
            WriteEntry(zip, "ranking.json", rankingJson);
        if (categories.Contains(Clipboard) && clipboardDbPath is not null && File.Exists(clipboardDbPath))
        {
            zip.CreateEntryFromFile(clipboardDbPath, "clipboard.sqlite");
            var images = Path.Combine(Path.GetDirectoryName(clipboardDbPath) ?? "", "images");
            if (Directory.Exists(images))
            {
                foreach (var file in Directory.EnumerateFiles(images))
                    zip.CreateEntryFromFile(file, "clipboard-images/" + Path.GetFileName(file));
            }
        }
        if (categories.Contains(Notes) && notesZipFolder is not null && Directory.Exists(notesZipFolder))
        {
            foreach (var file in Directory.EnumerateFiles(notesZipFolder, "*.md"))
                zip.CreateEntryFromFile(file, "notes/" + Path.GetFileName(file));
        }
    }

    public static BackupManifest? ReadManifest(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry("manifest.json");
        if (entry is null)
            return null;
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<BackupManifest>(stream);
    }

    public static Dictionary<string, string> ReadSettings(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry("settings.json");
        if (entry is null)
            return [];
        using var stream = entry.Open();
        var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
        return raw
            .Where(kv => SettingsBackupCoverage.Mirrored.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public static string? ReadEntryText(string zipPath, string name)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry(name);
        if (entry is null)
            return null;
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    public static bool ExtractFile(string zipPath, string name, string destPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry(name);
        if (entry is null)
            return false;
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        entry.ExtractToFile(destPath, overwrite: true);
        return true;
    }

    public static void ExtractPrefix(string zipPath, string prefix, string destDir, bool uniquifyCollisions = true)
    {
        Directory.CreateDirectory(destDir);
        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith('/') || !entry.FullName.StartsWith(prefix, StringComparison.Ordinal))
                continue;
            var name = Path.GetFileName(entry.FullName);
            if (string.IsNullOrEmpty(name) || name.Contains("..", StringComparison.Ordinal))
                continue;
            var dest = Path.Combine(destDir, name);
            if (uniquifyCollisions && File.Exists(dest))
                dest = Path.Combine(destDir, Path.GetFileNameWithoutExtension(name) + "-" + Guid.NewGuid().ToString("n")[..8] + Path.GetExtension(name));
            entry.ExtractToFile(dest, overwrite: !uniquifyCollisions);
        }
    }

    static void WriteEntry(ZipArchive zip, string name, string text)
    {
        var entry = zip.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(text);
    }
}
