using System.Security.AccessControl;
using System.Security.Principal;
using Tinycast.Features.Updates;

namespace Tinycast.Platform;

internal static class GitHubAuth
{
    public const string LegacyVaultKey = "github-updates";

    public static string EnvFilePath => AppPaths.EnvFile;

    public static string? Token => Resolve().Token;

    public static string StatusLine
    {
        get
        {
            var (_, source) = Resolve();
            return string.IsNullOrEmpty(source)
                ? "No GitHub token. Add " + GitHubToken.EnvName + " to " + EnvFilePath
                : "GitHub token loaded from " + source + ".";
        }
    }

    public static string MissingTokenMessage() =>
        "This private repo needs a GitHub token. Set " + GitHubToken.EnvName
        + " or add it to " + EnvFilePath + ".";

    public static (string? Token, string Source) Resolve()
    {
        var fromEnv = GitHubToken.Sanitize(Environment.GetEnvironmentVariable(GitHubToken.EnvName));
        if (fromEnv is not null)
            return (fromEnv, GitHubToken.EnvName);

        var fromGithub = GitHubToken.Sanitize(Environment.GetEnvironmentVariable(GitHubToken.FallbackEnvName));
        if (fromGithub is not null)
            return (fromGithub, GitHubToken.FallbackEnvName);

        foreach (var path in CandidateFiles())
        {
            var fromFile = ReadFile(path);
            if (fromFile is not null)
                return (fromFile, path);
        }

        var vault = GitHubToken.Sanitize(CredentialStore.Get(LegacyVaultKey));
        return vault is null ? (null, "") : (vault, "Credential Locker");
    }

    public static void MigrateVault()
    {
        var vault = GitHubToken.Sanitize(CredentialStore.Get(LegacyVaultKey));
        if (vault is null)
            return;

        AppPaths.EnsureRoot();
        var existing = ReadFile(EnvFilePath);
        if (existing is null)
        {
            WriteEnvFile(EnvFilePath, vault);
            CredentialStore.Set(LegacyVaultKey, "");
            Log.Write("update token moved from Credential Locker to " + EnvFilePath);
            return;
        }

        if (string.Equals(existing, vault, StringComparison.Ordinal))
        {
            CredentialStore.Set(LegacyVaultKey, "");
            Log.Write("update token moved from Credential Locker to " + EnvFilePath);
            return;
        }

        Log.Write("update token in Credential Locker differs from " + EnvFilePath + "; leaving both");
    }

    static IEnumerable<string> CandidateFiles()
    {
        yield return EnvFilePath;
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exe))
        {
            var dir = Path.GetDirectoryName(exe);
            if (!string.IsNullOrWhiteSpace(dir))
                yield return Path.Combine(dir, ".env");
        }
    }

    static string? ReadFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            var map = DotEnv.Parse(File.ReadAllText(path));
            return GitHubToken.Sanitize(DotEnv.Get(map, GitHubToken.EnvName, GitHubToken.FallbackEnvName));
        }
        catch (Exception ex)
        {
            Log.Write("env read " + path + ": " + ex.Message);
            return null;
        }
    }

    static void WriteEnvFile(string path, string token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,
            "# Tinycast GitHub token for private Releases. Do not commit this file.\n"
            + GitHubToken.EnvName + "=" + token + "\n");
        RestrictToCurrentUser(path);
    }

    static void RestrictToCurrentUser(string path)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (identity.User is null)
            return;
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            identity.User,
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }
}
