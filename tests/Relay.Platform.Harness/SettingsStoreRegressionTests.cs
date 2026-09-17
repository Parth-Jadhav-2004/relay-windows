using System.Text;
using Relay.Platform;

namespace Relay.Platform.Harness;

internal static class SettingsStoreRegressionTests
{
    public static void Run(Action<string, bool, string?> check)
    {
        RunCase("settings missing load creates only its isolated directory", MissingLoad);
        RunCase("settings initial save and replacements round-trip with recovery backup", SaveRoundTrip);
        RunCase("settings serialization failure retains previous bytes", SerializationFailure);
        RunCase("settings failed initial serialization leaves no file", InitialSerializationFailure);
        RunCase("settings locked replacement retains previous bytes", LockedReplacement);
        RunCase("settings read-only replacement retains previous bytes", ReadOnlyReplacement);
        RunCase("settings blocked replacement backup retains previous bytes", BlockedBackup);
        RunCase("settings corrupt and null JSON preserved before overwrite", CorruptLoad);
        RunCase("settings corrupt save without load preserves original", CorruptSave);
        RunCase("settings failed corruption preservation blocks overwrite", BlockedPreservation);
        RunCase("settings locked load safely returns defaults without moving original", LockedLoad);
        RunCase("settings inaccessible file path safely returns defaults", DirectoryAtFilePath);
        RunCase("settings directory creation failures are contained", BlockedDirectory);

        void RunCase(string name, Action<string, List<string>> test)
        {
            var root = Path.Combine(Path.GetTempPath(), "relay-settings-regression-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                try
                {
                    test(Path.Combine(root, "settings.json"), []);
                }
                finally
                {
                    Directory.Delete(root, true);
                }
                check(name, true, null);
            }
            catch (Exception exception)
            {
                check(name, false, exception.ToString());
            }
        }
    }

    static void MissingLoad(string path, List<string> logs)
    {
        path = Path.Combine(Path.GetDirectoryName(path)!, "nested", "settings.json");
        RequireDefaults(SettingsStore.Load(path, logs.Add));
        Require(Directory.Exists(Path.GetDirectoryName(path)), "Parent directory was not created.");
        Require(!File.Exists(path) && logs.Count == 0, "Missing settings were treated as corruption.");
    }

    static void SaveRoundTrip(string path, List<string> logs)
    {
        var first = new AppSettings { Appearance = AppAppearance.Light, WindowGap = 31, FileSearchScopes = ["C:\\資料"] };
        SettingsStore.Save(first, path, logs.Add);
        var original = File.ReadAllBytes(path);
        var loaded = SettingsStore.Load(path, logs.Add);
        Require(loaded.Appearance == first.Appearance && loaded.WindowGap == 31
            && loaded.FileSearchScopes.SequenceEqual(first.FileSearchScopes), "Initial settings did not round-trip.");
        var text = Encoding.UTF8.GetString(original);
        Require(text.Contains("\"appearance\": \"Light\"") && text.Contains('\n'), "JSON conventions changed.");
        Require(!File.Exists(path + ".bak"), "Initial save manufactured a backup.");
        SettingsStore.Save(new AppSettings { WindowGap = 42 }, path, logs.Add);
        Require(SettingsStore.Load(path, logs.Add).WindowGap == 42, "Replacement was not published.");
        Require(File.ReadAllBytes(path + ".bak").SequenceEqual(original), "Previous settings were not retained.");
        var second = File.ReadAllBytes(path);
        SettingsStore.Save(new AppSettings { WindowGap = 53 }, path, logs.Add);
        Require(SettingsStore.Load(path, logs.Add).WindowGap == 53, "Second replacement failed.");
        Require(File.ReadAllBytes(path + ".bak").SequenceEqual(second), "Recovery backup was not refreshed.");
        Require(logs.Count == 0, "Successful operations logged failures.");
        RequireNoTemporary(path);
    }

    static AppSettings InvalidSettings() => new()
    {
        FileSearchScopes = [new string('x', 100_000)],
        PaletteLeft = double.NaN,
    };

    static void SerializationFailure(string path, List<string> logs)
    {
        var original = Seed(path, logs);
        SettingsStore.Save(InvalidSettings(), path, logs.Add);
        RequireUnchanged(path, original, logs);
    }

    static void InitialSerializationFailure(string path, List<string> logs)
    {
        SettingsStore.Save(InvalidSettings(), path, logs.Add);
        Require(!File.Exists(path), "Failed initial save published partial settings.");
        Require(logs.Count > 0, "Serialization failure was not logged.");
        RequireNoTemporary(path);
    }

    static void LockedReplacement(string path, List<string> logs)
    {
        var original = Seed(path, logs);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            SettingsStore.Save(new AppSettings { WindowGap = 42 }, path, logs.Add);
        RequireUnchanged(path, original, logs);
    }

    static void ReadOnlyReplacement(string path, List<string> logs)
    {
        var original = Seed(path, logs);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try
        {
            SettingsStore.Save(new AppSettings { WindowGap = 42 }, path, logs.Add);
            RequireUnchanged(path, original, logs);
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }
    }

    static void BlockedBackup(string path, List<string> logs)
    {
        var original = Seed(path, logs);
        Directory.CreateDirectory(path + ".bak");
        SettingsStore.Save(new AppSettings { WindowGap = 42 }, path, logs.Add);
        RequireUnchanged(path, original, logs);
    }

    static void CorruptLoad(string path, List<string> logs)
    {
        foreach (var json in new[] { "{broken", "null", "", "[]", "{\"windowGap\":\"invalid\"}" })
        {
            var original = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(json)).ToArray();
            File.WriteAllBytes(path, original);
            var before = CorruptBackups(path).Length;
            RequireDefaults(SettingsStore.Load(path, logs.Add));
            var backups = CorruptBackups(path);
            Require(backups.Length == before + 1, "Corruption did not get a unique backup before Load returned.");
            var backup = backups.Single(p => File.ReadAllBytes(p).SequenceEqual(original));
            SettingsStore.Save(new AppSettings { WindowGap = 42 }, path, logs.Add);
            SettingsStore.Save(new AppSettings { WindowGap = 53 }, path, logs.Add);
            Require(File.ReadAllBytes(backup).SequenceEqual(original), "Later saves overwrote the corrupted original.");
            Require(SettingsStore.Load(path, logs.Add).WindowGap == 53, "Recovery prevented subsequent saves.");
        }
        Require(logs.Count >= 5, "Corruption recovery was silent.");
        RequireNoTemporary(path);
    }

    static void CorruptSave(string path, List<string> logs)
    {
        File.WriteAllText(path, "null");
        SettingsStore.Save(new AppSettings { WindowGap = 42 }, path, logs.Add);
        Require(File.ReadAllText(CorruptBackups(path).Single()) == "null", "Save discarded an unobserved corrupt original.");
        Require(SettingsStore.Load(path, logs.Add).WindowGap == 42, "Save did not recover after preserving corruption.");
    }

    static void BlockedPreservation(string path, List<string> logs)
    {
        File.WriteAllText(path, "{broken");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            RequireDefaults(SettingsStore.Load(path, logs.Add));
            SettingsStore.Save(new AppSettings { WindowGap = 42 }, path, logs.Add);
            Require(File.ReadAllText(path) == "{broken", "Failed preservation allowed overwrite.");
            Require(CorruptBackups(path).Length == 0, "Failed move created a false backup.");
            Require(logs.Count >= 2, "Preservation failures were not logged.");
            RequireNoTemporary(path);
        }
        SettingsStore.Save(new AppSettings { WindowGap = 42 }, path, logs.Add);
        Require(File.ReadAllText(CorruptBackups(path).Single()) == "{broken", "Retry discarded the original.");
        Require(SettingsStore.Load(path, logs.Add).WindowGap == 42, "Save did not retry preservation.");
    }

    static void LockedLoad(string path, List<string> logs)
    {
        var original = Seed(path, logs);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            RequireDefaults(SettingsStore.Load(path, logs.Add));
        RequireUnchanged(path, original, logs);
        Require(CorruptBackups(path).Length == 0, "Unreadable settings were treated as corruption.");
    }

    static void DirectoryAtFilePath(string path, List<string> logs)
    {
        Directory.CreateDirectory(path);
        RequireDefaults(SettingsStore.Load(path, logs.Add));
        SettingsStore.Save(new AppSettings(), path, logs.Add);
        Require(Directory.Exists(path) && logs.Count >= 2, "Inaccessible path was not retained and reported.");
        RequireNoTemporary(path);
    }

    static void BlockedDirectory(string path, List<string> logs)
    {
        File.WriteAllText(path, "parent is a file");
        var nested = Path.Combine(path, "nested", "settings.json");
        RequireDefaults(SettingsStore.Load(nested, logs.Add));
        SettingsStore.Save(new AppSettings(), nested, logs.Add);
        Require(File.ReadAllText(path) == "parent is a file" && logs.Count >= 2, "Directory failures were not contained.");
        RequireNoTemporary(path);
    }

    static byte[] Seed(string path, List<string> logs)
    {
        SettingsStore.Save(new AppSettings { WindowGap = 31 }, path, logs.Add);
        Require(logs.Count == 0, "Test setup failed to save settings.");
        return File.ReadAllBytes(path);
    }

    static void RequireUnchanged(string path, byte[] original, List<string> logs)
    {
        Require(File.ReadAllBytes(path).SequenceEqual(original), "Failure changed previous settings bytes.");
        Require(logs.Count > 0, "Failure was not logged.");
        RequireNoTemporary(path);
    }

    static string[] CorruptBackups(string path) =>
        Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".corrupt-*.bak");

    static void RequireNoTemporary(string path) =>
        Require(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp").Length == 0, "Staging file was leaked.");

    static void RequireDefaults(AppSettings settings) =>
        Require(settings.Appearance == new AppSettings().Appearance && settings.WindowGap == new AppSettings().WindowGap,
            "Load did not return defaults.");

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
