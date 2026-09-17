using System.Text.Json;

namespace Tinycast.Platform;

internal static class JsonList
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static List<T> Load<T>(string path)
    {
        try
        {
            if (!File.Exists(path))
                return [];
            return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), Options) ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    public static void Save<T>(string path, List<T> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(items, Options));
    }
}
