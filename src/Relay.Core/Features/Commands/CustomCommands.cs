namespace Relay.Features.Commands;

public sealed class ArgumentSession
{
    public string OwnerId { get; }
    public string Kind { get; }
    public IReadOnlyList<ArgumentPrompt> Prompts { get; }
    public List<string> Values { get; } = [];
    public int Index { get; private set; }

    public ArgumentSession(string ownerId, string kind, IReadOnlyList<ArgumentPrompt> prompts)
    {
        OwnerId = ownerId;
        Kind = kind;
        Prompts = prompts;
        Index = 0;
    }

    public bool IsComplete => Index >= Prompts.Count;
    public ArgumentPrompt? Current => IsComplete ? null : Prompts[Index];
    public string Placeholder => Current is { } prompt ? prompt.Name : "";

    public bool Submit(string value)
    {
        if (IsComplete)
            return true;
        var prompt = Prompts[Index];
        if (prompt.Required && string.IsNullOrWhiteSpace(value) && prompt.DefaultValue is null)
            return false;
        var resolved = string.IsNullOrWhiteSpace(value) ? prompt.DefaultValue ?? "" : value;
        if (Index < Values.Count)
            Values[Index] = resolved;
        else
            Values.Add(resolved);
        Index++;
        return true;
    }

    public bool Back()
    {
        if (Index <= 0)
            return false;
        Index--;
        return true;
    }

    public string CurrentDraft => Index < Values.Count ? Values[Index] : "";
}

public sealed record ArgumentPrompt(string Name, bool Required = true, string? DefaultValue = null, IReadOnlyList<string>? Options = null);

public static class CommandAvailability
{
    public static bool InLauncher(string id, AppSettings settings) => id switch
    {
        BuiltinCommands.FileSearch => settings.FileSearchEnabled,
        BuiltinCommands.Notes or BuiltinCommands.SearchNotes or BuiltinCommands.RevealNotes => settings.NotesEnabled,
        BuiltinCommands.Snippets => settings.SnippetsEnabled && settings.SnippetsShowInLauncher,
        BuiltinCommands.Quicklinks => settings.QuicklinksEnabled,
        BuiltinCommands.SwitchWindows or BuiltinCommands.MenuSearch => settings.NavigationEnabled,
        BuiltinCommands.SaveLayout or BuiltinCommands.CreateLayout => settings.WindowManagementEnabled && settings.WindowLayoutsShowInLauncher,
        BuiltinCommands.AiChat => settings.AiEnabled,
        BuiltinCommands.Schedule or BuiltinCommands.JoinNext or BuiltinCommands.CreateEvent
            or BuiltinCommands.CopyMeetingLink or BuiltinCommands.OpenCalendar => settings.CalendarEnabled,
        BuiltinCommands.Camera => settings.CameraPreview,
        BuiltinCommands.Clipboard => settings.ClipboardEnabled,
        BuiltinCommands.Uninstall => true,
        _ => true,
    };

    public static bool CustomInLauncher(CustomCommand command, AppSettings settings) =>
        settings.CustomCommandsEnabled && settings.CustomCommandsShowInLauncher && command.Enabled;

    public static bool CanRun(string id, AppSettings settings) => id switch
    {
        BuiltinCommands.FileSearch => settings.FileSearchEnabled,
        BuiltinCommands.Notes or BuiltinCommands.SearchNotes or BuiltinCommands.RevealNotes => settings.NotesEnabled,
        BuiltinCommands.Snippets => settings.SnippetsEnabled,
        BuiltinCommands.Quicklinks => settings.QuicklinksEnabled,
        BuiltinCommands.SwitchWindows or BuiltinCommands.MenuSearch => settings.NavigationEnabled,
        BuiltinCommands.SaveLayout or BuiltinCommands.CreateLayout => settings.WindowManagementEnabled,
        BuiltinCommands.AiChat => settings.AiEnabled,
        BuiltinCommands.Schedule or BuiltinCommands.JoinNext or BuiltinCommands.CreateEvent
            or BuiltinCommands.CopyMeetingLink or BuiltinCommands.OpenCalendar => settings.CalendarEnabled,
        BuiltinCommands.Camera => settings.CameraPreview,
        BuiltinCommands.Clipboard => settings.ClipboardEnabled,
        BuiltinCommands.Quit => true,
        _ => true,
    };

    public static bool IsBindable(string id) =>
        id is not BuiltinCommands.Quit
            and not "fallback:browser"
            and not "fallback:shell";
}

public sealed class CustomCommand
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FileName { get; set; } = "";
    public List<string> Arguments { get; set; } = [];
    public bool Confirm { get; set; } = true;
    public bool ShowOutput { get; set; }
    public bool LoadEnvironment { get; set; }
    public string WorkingDirectory { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool ShowConfirmation { get; set; }
    public List<CustomCommandParameter> Parameters { get; set; } = [];
}

public sealed class CustomCommandParameter
{
    public string Name { get; set; } = "Argument";
    public bool Required { get; set; } = true;
}

public static class ShellCommandSpec
{
    public static IReadOnlyList<string> Positional(IEnumerable<string> values) => values.ToList();

    public static string? ResolvedWorkingDirectory(string? stored, string home)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return home;
        var expanded = stored.Trim();
        if (expanded == "~")
            expanded = home;
        else if (expanded.StartsWith("~\\", StringComparison.Ordinal) || expanded.StartsWith("~/", StringComparison.Ordinal))
            expanded = Path.Combine(home, expanded[2..]);
        expanded = Environment.ExpandEnvironmentVariables(expanded);
        if (!Directory.Exists(expanded))
            return null;
        return expanded;
    }

    public static bool CommandTextIsExecutable(string fileName) =>
        File.Exists(fileName);

    public static IReadOnlyList<string> NeverSplicedArguments(string commandText, IReadOnlyList<string> values)
    {
        _ = commandText;
        return values.ToList();
    }
}

public static class RaycastScriptImport
{
    public static CustomCommand? Parse(string path, string contents)
    {
        var head = contents.Length > 8192 ? contents[..8192] : contents;
        var lines = head.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0 || !lines[0].StartsWith("#!", StringComparison.Ordinal))
            return null;
        var shebang = lines[0][2..].Trim();
        string? title = null;
        var mode = "";
        var needsConfirmation = false;
        var directory = Path.GetDirectoryName(path) ?? "";
        var parameters = new List<CustomCommandParameter>();
        foreach (var line in lines.Skip(1))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#'))
                trimmed = trimmed[1..].Trim();
            if (trimmed.StartsWith("@raycast.title", StringComparison.OrdinalIgnoreCase))
                title = Value(trimmed);
            else if (trimmed.StartsWith("@raycast.mode", StringComparison.OrdinalIgnoreCase))
                mode = Value(trimmed);
            else if (trimmed.StartsWith("@raycast.needsConfirmation", StringComparison.OrdinalIgnoreCase))
                needsConfirmation = Value(trimmed) is "true";
            else if (trimmed.StartsWith("@raycast.currentDirectoryPath", StringComparison.OrdinalIgnoreCase))
                directory = Value(trimmed);
            else if (trimmed.StartsWith("@raycast.argument", StringComparison.OrdinalIgnoreCase))
            {
                var placeholder = Attribute(trimmed, "placeholder")
                    ?? JsonAttribute(trimmed, "placeholder")
                    ?? "Argument";
                var optional = Attribute(trimmed, "optional") is "true"
                    || JsonAttribute(trimmed, "optional") is "true";
                parameters.Add(new CustomCommandParameter { Name = placeholder, Required = !optional });
            }
        }

        if (string.IsNullOrWhiteSpace(title))
            return null;
        var showOutput = mode is "compact" or "fullOutput";
        var interpreter = shebang.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        var quoted = "'" + path.Replace("'", "'\\''") + "'";
        return new CustomCommand
        {
            Id = "custom:" + Guid.NewGuid().ToString("n"),
            Name = title,
            FileName = interpreter + " " + quoted + " %*",
            Confirm = needsConfirmation,
            ShowOutput = showOutput,
            WorkingDirectory = directory,
            Parameters = parameters,
            Enabled = true,
        };
    }

    static string Value(string line)
    {
        var space = line.IndexOf(' ');
        return space < 0 ? "" : line[(space + 1)..].Trim().Trim('"');
    }

    static string? Attribute(string line, string name)
    {
        var needle = name + "=\"";
        var start = line.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;
        start += needle.Length;
        var end = line.IndexOf('"', start);
        return end < 0 ? null : line[start..end];
    }

    static string? JsonAttribute(string line, string name)
    {
        var needle = "\"" + name + "\"";
        var start = line.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;
        var colon = line.IndexOf(':', start + needle.Length);
        if (colon < 0)
            return null;
        var rest = line[(colon + 1)..].Trim();
        if (rest.StartsWith('"'))
        {
            var end = rest.IndexOf('"', 1);
            return end < 0 ? rest.Trim('"') : rest[1..end];
        }

        var cut = rest.IndexOfAny([',', '}', ' ']);
        return (cut < 0 ? rest : rest[..cut]).Trim();
    }
}
