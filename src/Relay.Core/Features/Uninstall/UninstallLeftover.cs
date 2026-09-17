namespace Relay.Features.Uninstall;

public sealed record UninstallLeftover(
    string Path,
    string Title,
    long Bytes,
    bool Selected,
    string Kind);

public static class UninstallLeftoverLogic
{
    public static string SizeLabel(long bytes)
    {
        if (bytes < 1000)
            return bytes + " B";
        if (bytes < 1_000_000)
            return (bytes / 1000.0).ToString("0.#") + " KB";
        if (bytes < 1_000_000_000)
            return (bytes / 1_000_000.0).ToString("0.#") + " MB";
        return (bytes / 1_000_000_000.0).ToString("0.#") + " GB";
    }

    public static long FolderBytes(string path)
    {
        try
        {
            if (File.Exists(path))
                return new FileInfo(path).Length;
            if (!Directory.Exists(path))
                return 0;
            return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Take(4000)
                .Select(file =>
                {
                    try { return new FileInfo(file).Length; }
                    catch (Exception) { return 0L; }
                })
                .Sum();
        }
        catch (Exception)
        {
            return 0;
        }
    }
}
