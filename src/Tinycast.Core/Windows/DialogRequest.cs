namespace Tinycast;

public enum DialogTone
{
    Neutral,
    Success,
    Danger,
}

public enum DialogActionRole
{
    Standard,
    Destructive,
    Cancel,
}

public sealed record DialogAction(string Title, DialogActionRole Role = DialogActionRole.Standard);

public sealed record DialogRequest(
    string Title,
    string Symbol,
    IReadOnlyList<DialogAction> Actions,
    int DefaultIndex,
    int CancelIndex,
    string? Message = null,
    DialogTone Tone = DialogTone.Neutral);
