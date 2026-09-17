namespace Tinycast.Features.Ai;

/// <summary>
/// T3 Code parity constants for the OpenCode bridge.
/// Mirrors apps/server/src/provider/opencodeRuntime.ts and OpenCodeServerOwner.ts.
/// Pure; safe for the harness.
/// </summary>
public static class OpenCodeConstants
{
    public const string MinimumVersion = "1.14.19";

    public const string DefaultBinaryPath = "opencode";
    public const string DefaultHostname = "127.0.0.1";

    /// <summary>Placeholder shown in Settings; empty ServerUrl means spawn local.</summary>
    public const string ServerUrlPlaceholder = "http://127.0.0.1:4096";

    public const string ReadyPrefix = "opencode server listening";
    public const string EmptyConfigContent = "{}";

    public static TimeSpan HealthTimeout { get; } = TimeSpan.FromSeconds(5);
    public static TimeSpan VersionProbeTimeout { get; } = TimeSpan.FromSeconds(4);
    public static TimeSpan ServerStartTimeout { get; } = TimeSpan.FromSeconds(30);
    public static TimeSpan SkillsTimeout { get; } = TimeSpan.FromSeconds(20);
    public static TimeSpan ServerIdleTtl { get; } = TimeSpan.FromSeconds(30);

    public const int ServerStartupMaxOutputChars = 64 * 1024;

    public const string DefaultModelSlug = "openai/gpt-5";
    public const string DefaultAgent = "build";
}
