namespace Relay.Features.Ai;

/// <summary>
/// OpenCode bridge settings. Password lives in Credential Locker, never here
/// and never in a backup (see SettingsBackupCoverage.DeliberatelyExcluded).
/// Mirrors OpenCodeSettings in packages/contracts/src/settings.ts (binaryPath,
/// serverUrl, customModels; enabled maps to AppSettings.AiEnabled).
/// </summary>
public sealed class OpenCodeSettings
{
    public string BinaryPath { get; set; } = OpenCodeConstants.DefaultBinaryPath;

    /// <summary>Empty means spawn a local server (T3 parity).</summary>
    public string ServerUrl { get; set; } = "";

    public List<AiModelRef> CustomModels { get; set; } = [];

    public bool IsExternal => !string.IsNullOrWhiteSpace(ServerUrl);

    public OpenCodeSettings Clone() => new()
    {
        BinaryPath = BinaryPath,
        ServerUrl = ServerUrl,
        CustomModels = [.. CustomModels],
    };
}
