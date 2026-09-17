namespace Tinycast.Features.Commands;

public sealed record CustomCommand(string Id, string Name, string FileName, IReadOnlyList<string> Arguments, bool Confirm);

public sealed record NoteDocument(string Id, string Title, string Path, string Text);

public static class BuiltinCommands
{
    public const string Settings = "command:settings";
    public const string Quit = "command:quit";
    public const string Clipboard = "command:clipboard";
    public const string Emoji = "command:emoji";
    public const string FileSearch = "command:file-search";
    public const string Notes = "command:notes";
    public const string CalculatorHistory = "command:calculator-history";
    public const string Snippets = "command:snippets";
    public const string Quicklinks = "command:quicklinks";
    public const string SwitchWindows = "command:switch-windows";
    public const string MenuSearch = "command:menu-search";
    public const string Uninstall = "command:uninstall";
    public const string Support = "command:support";
    public const string Backup = "command:backup";
    public const string Updates = "command:updates";
    public const string Camera = "command:camera";
    public const string AiChat = "command:ai-chat";
    public const string Schedule = "command:schedule";
    public const string TogglePalette = "command:toggle-palette";
    public const string SaveLayout = "command:save-layout";
}

public sealed record SupportReminderSchedule(DateTime InstalledAt, DateTime? LastShown)
{
    public static TimeSpan Interval { get; } = TimeSpan.FromDays(30);

    public bool IsDue(DateTime now)
    {
        var anchor = LastShown ?? InstalledAt;
        return now - anchor >= Interval;
    }

    public SupportReminderSchedule Shown(DateTime now) => this with { LastShown = now };
}

public sealed record AiChatMessage(string Role, string Content);

public sealed record McpServerSpec(string Id, string Name, string Command, IReadOnlyList<string> Arguments, bool Trusted);

public sealed record QuickAction(string Id, string Name, string Prompt);
