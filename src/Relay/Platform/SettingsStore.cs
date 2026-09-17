using System.Text.Json;
using System.Text.Json.Serialization;
using Relay;

namespace Relay.Platform;

internal static class SettingsStore
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load() => Load(AppPaths.SettingsFile, Log.Write);

    internal static AppSettings Load(string path, Action<string> log)
    {
        try
        {
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return ReadSettings(path);
        }
        catch (FileNotFoundException)
        {
            return new AppSettings();
        }
        catch (JsonException)
        {
            try
            {
                PreserveCorrupt(path, log);
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                log($"settings preservation failed ({exception.GetType().Name}): {path}");
            }
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            log($"settings load failed ({exception.GetType().Name}): {path}");
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings) => Save(settings, AppPaths.SettingsFile, Log.Write);

    internal static void Save(AppSettings settings, string path, Action<string> log)
    {
        string? temporary = null;
        try
        {
            ArgumentNullException.ThrowIfNull(settings);
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var candidate = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var stream = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                temporary = candidate;
                JsonSerializer.Serialize(stream, settings, Options);
                stream.Flush(flushToDisk: true);
            }

            var exists = true;
            try
            {
                ReadSettings(path);
            }
            catch (FileNotFoundException)
            {
                exists = false;
            }
            catch (JsonException)
            {
                PreserveCorrupt(path, log);
                exists = false;
            }

            if (exists)
                File.Replace(temporary, path, path + ".bak");
            else
                File.Move(temporary, path);
            temporary = null;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            log($"settings save failed ({exception.GetType().Name}): {path}");
        }
        finally
        {
            if (temporary is not null)
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (Exception exception) when (IsPersistenceFailure(exception))
                {
                    log($"settings temporary cleanup failed ({exception.GetType().Name}): {temporary}");
                }
            }
        }
    }

    static AppSettings ReadSettings(string path) =>
        JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options)
        ?? throw new JsonException("Settings must not be null.");

    static void PreserveCorrupt(string path, Action<string> log)
    {
        var backup = path + ".corrupt-" + Guid.NewGuid().ToString("N") + ".bak";
        File.Move(path, backup);
        log($"settings invalid JSON preserved: {backup}");
    }

    static bool IsPersistenceFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException;
}
