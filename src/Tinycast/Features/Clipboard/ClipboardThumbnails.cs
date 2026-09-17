using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Tinycast;

internal static class ClipboardThumbnails
{
    static readonly Dictionary<(string Path, int Px), BitmapImage> Cache = [];

    public static BitmapImage? Load(string? path, int decodePx)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        var key = (Path.GetFullPath(path), decodePx);
        if (Cache.TryGetValue(key, out var hit))
            return hit;
        try
        {
            var bytes = File.ReadAllBytes(key.Item1);
            var mem = new InMemoryRandomAccessStream();
            var writer = new DataWriter(mem);
            writer.WriteBytes(bytes);
            writer.StoreAsync().AsTask().GetAwaiter().GetResult();
            writer.DetachStream();
            writer.Dispose();
            mem.Seek(0);
            var bmp = new BitmapImage { DecodePixelWidth = decodePx };
            bmp.SetSource(mem);
            Cache[key] = bmp;
            return bmp;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
