using System.Diagnostics;
using System.Text;
using Tinycast.Features.Commands;

namespace Tinycast.Platform;

internal static class CommandProcess
{
    public static string Run(string fileName, IReadOnlyList<string> arguments, string extra, int timeoutMs = 8000) =>
        Run(fileName, extra.Length == 0 ? arguments : arguments.Append(extra).ToList(), "", null, false, timeoutMs);

    public static string Run(
        string fileName,
        IReadOnlyList<string> positional,
        string extra,
        string? workingDirectory,
        bool loadEnvironment,
        int timeoutMs = 30000)
    {
        var output = new StringBuilder();
        using var process = Start(fileName, positional, extra, workingDirectory, loadEnvironment, line => output.AppendLine(line));
        if (process is null)
            throw new InvalidOperationException("Could not start " + fileName);
        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch (Exception) { }
            throw new TimeoutException(fileName + " did not finish.");
        }

        return output.ToString().Trim();
    }

    public static async Task StreamAsync(
        string fileName,
        IReadOnlyList<string> positional,
        string? workingDirectory,
        bool loadEnvironment,
        Action<string> onChunk)
    {
        using var process = Start(fileName, positional, "", workingDirectory, loadEnvironment, onChunk);
        if (process is null)
            throw new InvalidOperationException("Could not start " + fileName);
        await process.WaitForExitAsync();
    }

    static Process? Start(
        string fileName,
        IReadOnlyList<string> positional,
        string extra,
        string? workingDirectory,
        bool loadEnvironment,
        Action<string> onLine)
    {
        var args = positional.ToList();
        if (!string.IsNullOrWhiteSpace(extra))
            args.Add(extra);
        ProcessStartInfo start;
        if (ShellCommandSpec.CommandTextIsExecutable(fileName))
        {
            start = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (var argument in args)
                start.ArgumentList.Add(argument);
        }
        else
        {
            var script = Path.Combine(Path.GetTempPath(), "tinycast-cmd-" + Guid.NewGuid().ToString("n") + ".cmd");
            File.WriteAllText(script, "@echo off\r\n" + fileName + "\r\n");
            var comspec = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            start = new ProcessStartInfo(comspec)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            if (!loadEnvironment)
                start.ArgumentList.Add("/d");
            start.ArgumentList.Add("/s");
            start.ArgumentList.Add("/c");
            start.ArgumentList.Add(script);
            foreach (var argument in args)
                start.ArgumentList.Add(argument);
        }

        if (!string.IsNullOrWhiteSpace(workingDirectory))
            start.WorkingDirectory = workingDirectory;
        start.Environment["TINYCAST"] = "1";
        var process = Process.Start(start);
        if (process is null)
            return null;
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data + Environment.NewLine); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data + Environment.NewLine); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }
}
