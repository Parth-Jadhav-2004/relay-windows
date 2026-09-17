using Tinycast.Features.Calculator;
using Tinycast.Features.Launcher;

namespace Tinycast.Palette;

public sealed record PaletteRow(
    string Id,
    string Title,
    string? Subtitle,
    string Glyph,
    string? Section = null,
    AppEntryKind Kind = AppEntryKind.Command,
    string? Preview = null,
    bool IsCard = false,
    string? CopyText = null,
    string? IconPath = null,
    string? LeadExpression = null,
    string? SourceBadge = null,
    string? TargetBadge = null,
    string? PrimaryAction = null,
    bool ShowActions = false,
    bool IsError = false,
    bool FillIcon = false,
    bool? Checked = null,
    string? Accessory = null)
{
    public bool IsLeadCard => IsCard && LeadExpression is not null;

    public static PaletteRow Calculator(string id, CalcResult result, string section) =>
        new(
            id,
            result.Display,
            result.Expression,
            "",
            section,
            AppEntryKind.Command,
            result.IsError ? result.Display : result.CopyText,
            true,
            result.IsActionable ? result.CopyText : null,
            LeadExpression: result.Expression,
            SourceBadge: result.SourceBadge,
            TargetBadge: result.TargetBadge,
            PrimaryAction: result.IsActionable ? "Copy Answer" : null,
            ShowActions: result.IsActionable,
            IsError: result.IsError);
}
