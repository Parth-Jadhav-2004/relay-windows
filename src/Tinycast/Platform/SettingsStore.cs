using System.Text.Json;
using System.Text.Json.Serialization;
using Tinycast;

namespace Tinycast.Platform;

internal static class SettingsStore
{
    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load()
    {
        AppPaths.EnsureRoot();
        if (!File.Exists(AppPaths.SettingsFile))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(AppPaths.SettingsFile);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        AppPaths.EnsureRoot();
        File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(settings, Options));
    }
}
