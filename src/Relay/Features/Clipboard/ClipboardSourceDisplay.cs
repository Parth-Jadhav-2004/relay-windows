using System.Diagnostics;
using Relay.Features.Clipboard;
using Relay.Platform;

namespace Relay;

internal static class ClipboardSourceDisplay
{
    public static (string Name, string? IconPath)? Resolve(string? sourceId)
    {
        var title = ClipboardPresentation.SourceTitle(sourceId);
        if (title is null)
            return null;

        var name = title;
        string? icon = null;
        if (!string.IsNullOrWhiteSpace(sourceId) && File.Exists(sourceId))
        {
            try
            {
                var description = FileVersionInfo.GetVersionInfo(sourceId).FileDescription;
                if (!string.IsNullOrWhiteSpace(description))
                    name = description;
            }
            catch (Exception)
            {
            }

            icon = ShellIcons.FromFile(sourceId);
        }

        return (name, icon);
    }
}
