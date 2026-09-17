using Microsoft.UI.Xaml;
using Relay.DesignSystem;
using Relay.Platform;

namespace Relay;

public sealed partial class SupportWindow : Window
{
    readonly AppCore _core;

    public SupportWindow(AppCore core)
    {
        _core = core;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        WindowChrome.ResizeDips(this, 520, 280);
        IdentityBlock.Text = UpdatesClient.InstalledLabel;
    }

    void OnGithub(object sender, RoutedEventArgs e) =>
        ProcessLauncher.OpenUri("https://github.com/" + UpdatesClient.Repository);

    void OnLicense(object sender, RoutedEventArgs e)
    {
        var license = Path.Combine(AppContext.BaseDirectory, "LICENSE");
        if (File.Exists(license))
            ProcessLauncher.Open(license);
        else
            ProcessLauncher.OpenUri("https://www.gnu.org/licenses/agpl-3.0.html");
    }

    void OnLater(object sender, RoutedEventArgs e)
    {
        _core.PostponeSupportReminder();
        Close();
    }
}
