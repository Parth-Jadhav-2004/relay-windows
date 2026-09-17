using Microsoft.UI.Xaml;
using Tinycast.DesignSystem;
using Tinycast.Features.Commands;
using Tinycast.Features.HotKeys;
using Tinycast.Platform;

namespace Tinycast;

public sealed partial class OnboardingWindow : Window
{
    readonly AppCore _core;
    HotKeyChord? _chord;

    public OnboardingWindow(AppCore core)
    {
        _core = core;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        WindowChrome.ResizeDips(this, 480, 280);
        LoginSwitch.IsOn = _core.Settings.LaunchAtLogin;
        ChordStatus.Text = "Alt+Space already opens the palette. Record another chord if you want.";
    }

    void OnRecord(object sender, RoutedEventArgs e)
    {
        ChordStatus.Text = "Press a shortcut…";
        _core.RecordHotKey(chord =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _chord = chord;
                ChordStatus.Text = "Captured " + chord.Label;
            });
        });
    }

    void OnSkip(object sender, RoutedEventArgs e)
    {
        ApplyLogin();
        _core.CompleteOnboarding();
        Close();
    }

    void OnContinue(object sender, RoutedEventArgs e)
    {
        ApplyLogin();
        if (_chord is { } chord)
        {
            _core.HotKeys.Add(new HotKeyBinding
            {
                CommandId = BuiltinCommands.TogglePalette,
                Chord = chord,
            });
            _core.PersistHotKeys();
        }

        _core.CompleteOnboarding();
        Close();
    }

    void ApplyLogin()
    {
        _core.Settings.LaunchAtLogin = LoginSwitch.IsOn;
        LaunchAtLogin.SetEnabled(LoginSwitch.IsOn);
        _core.Persist();
    }
}
