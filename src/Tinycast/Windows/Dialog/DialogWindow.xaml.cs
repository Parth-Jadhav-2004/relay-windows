using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Tinycast.DesignSystem;
using Tinycast.Platform;
using Windows.System;

namespace Tinycast;

public sealed partial class DialogWindow : Window
{
    readonly DialogRequest _request;
    readonly Action<int> _finish;
    bool _finished;

    public DialogWindow(AppCore core, DialogRequest request, Action<int> finish)
    {
        _request = request;
        _finish = finish;
        InitializeComponent();
        var dark = ThemeBrushes.IsDark(core.Settings.Appearance, Application.Current.RequestedTheme);
        WindowChrome.ApplyPaletteChrome(this, Theme.Radius.Dialog, dark);
        WindowClip.RoundDialog(Root);
        WindowChrome.ResizeDips(this, Theme.Size.DialogWidth, 180, client: true);
        Root.RequestedTheme = core.Settings.Appearance switch
        {
            AppAppearance.Light => ElementTheme.Light,
            AppAppearance.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        Scrim.Background = ThemeBrushes.Scrim(dark, core.Settings.PaletteTransparency);
        TitleBlock.Text = request.Title;
        TitleBlock.Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextPrimary.For(dark), dark);
        MessageBlock.Text = request.Message ?? "";
        MessageBlock.Visibility = string.IsNullOrWhiteSpace(request.Message) ? Visibility.Collapsed : Visibility.Visible;
        MessageBlock.Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextSecondary.For(dark), dark);
        Glyph.Glyph = request.Symbol;
        Glyph.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            request.Tone == DialogTone.Neutral
                ? ThemeBrushes.Ink(Theme.Colors.TextPrimary.For(dark), dark)
                : ThemeBrushes.ToneColor(request.Tone));

        for (var i = 0; i < request.Actions.Count; i++)
        {
            var index = i;
            var action = request.Actions[i];
            var button = new Button
            {
                Content = action.Title,
                Tag = index,
                MinWidth = 88,
            };
            if (action.Role == DialogActionRole.Destructive)
                button.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(ThemeBrushes.ToneColor(DialogTone.Danger));
            if (i == request.DefaultIndex)
                button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            button.Click += (_, _) => Complete(index);
            Buttons.Children.Add(button);
        }

        Closed += (_, _) => Complete(_request.CancelIndex);
    }

    void Complete(int index)
    {
        if (_finished)
            return;
        _finished = true;
        _finish(index);
    }

    void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            Complete(_request.CancelIndex);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter)
        {
            Complete(_request.DefaultIndex);
            e.Handled = true;
        }
    }
}
