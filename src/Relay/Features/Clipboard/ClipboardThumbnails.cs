using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Relay;

internal static class ClipboardThumbnails
{
    const int MaxEntries = 32;

    sealed class Entry
    {
        public required BitmapImage Image { get; init; }
        public required IRandomAccessStream Stream { get; init; }
        public required LinkedListNode<(string Path, int Px)> Node { get; set; }
    }

    static readonly Dictionary<(string Path, int Px), Entry> Cache = [];
    static readonly LinkedList<(string Path, int Px)> Lru = [];

    public static BitmapImage? Load(string? path, int decodePx)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        var key = (Path.GetFullPath(path), decodePx);
        if (Cache.TryGetValue(key, out var hit))
        {
            Touch(hit);
            return hit.Image;
        }

        IRandomAccessStream? stream = null;
        try
        {
            stream = File.OpenRead(key.Item1).AsRandomAccessStream();
            var bmp = new BitmapImage { DecodePixelWidth = decodePx };
            bmp.SetSource(stream);
            EvictIfNeeded();
            var node = Lru.AddFirst(key);
            Cache[key] = new Entry { Image = bmp, Stream = stream, Node = node };
            return bmp;
        }
        catch (Exception)
        {
            stream?.Dispose();
            return null;
        }
    }

    static void Touch(Entry entry)
    {
        if (entry.Node.List is null || entry.Node.List.First == entry.Node)
            return;
        Lru.Remove(entry.Node);
        Lru.AddFirst(entry.Node);
    }

    static void EvictIfNeeded()
    {
        while (Cache.Count >= MaxEntries && Lru.Last is { } last)
        {
            Lru.RemoveLast();
            if (!Cache.Remove(last.Value, out var evicted))
                continue;
            evicted.Stream.Dispose();
        }
    }
}
