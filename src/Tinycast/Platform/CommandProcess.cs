using System.Diagnostics;
using System.Text;

namespace Tinycast.Platform;

internal static class CommandProcess
{
    public static string Run(string fileName, IReadOnlyList<string> arguments, string extra, int timeoutMs = 8000)
    {
        var args = arguments.ToList();
        if (!string.IsNullOrWhiteSpace(extra))
            args.Add(extra);
        var start = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var argument in args)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start);
        if (process is null)
            throw new InvalidOperationException("Could not start " + fileName);
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch (Exception) { }
            throw new TimeoutException(fileName + " did not finish.");
        }

        return output.ToString().Trim();
    }
}
