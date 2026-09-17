using Microsoft.UI.Xaml;
using Relay.Platform;

namespace Relay;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            Log.Write(e.Exception.ToString());
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args) => AppCore.Shared.Start();
}
