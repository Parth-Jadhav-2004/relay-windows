namespace Tinycast.Platform;

internal static class Log
{
    static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            AppPaths.EnsureRoot();
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}";
            lock (Gate)
                File.AppendAllText(Path.Combine(AppPaths.Root, "tinycast.log"), line);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
