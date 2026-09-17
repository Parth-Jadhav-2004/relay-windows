using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Tinycast.DesignSystem;
using Tinycast.Features.Clipboard;
using Tinycast.Features.FileSearch;
using Tinycast.Platform;

namespace Tinycast;

internal static class FilePreviewPane
{
    public static void Populate(Grid host, string path, bool dark)
    {
        host.Children.Clear();
        host.RowDefinitions.Clear();
        host.ColumnDefinitions.Clear();
        host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        FrameworkElement preview;
        if (ClipboardCapture.IsImagePath(path))
        {
            preview = new Border
            {
                CornerRadius = new CornerRadius(Theme.Radius.Card),
                BorderBrush = ThemeBrushes.InkBrush(Theme.Colors.CardStroke.For(dark), dark),
                BorderThickness = new Thickness(1),
                Child = new Image
                {
                    Source = new BitmapImage(new Uri(path)),
                    Stretch = Stretch.Uniform,
                    MaxHeight = Theme.Size.ClipboardMediaHeight,
                },
            };
        }
        else if (FileSearchFilter.Documents.Accepts(path, false) && File.Exists(path))
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception) { text = path; }
            if (text.Length > 4000)
                text = text[..4000] + "…";
            preview = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Cascadia Mono"),
                FontSize = 12,
                Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextSecondary.For(dark), dark),
            };
        }
        else
        {
            preview = new TextBlock
            {
                Text = path,
                TextWrapping = TextWrapping.Wrap,
                Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextSecondary.For(dark), dark),
            };
        }

        Grid.SetRow(preview, 0);
        host.Children.Add(preview);
        var info = File.Exists(path) ? new FileInfo(path) : null;
        var dir = Directory.Exists(path) ? new DirectoryInfo(path) : null;
        var lines = new List<string> { path };
        if (info is not null)
        {
            lines.Add(SizeLabel(info.Length));
            lines.Add(info.LastWriteTime.ToString("g"));
        }
        else if (dir is not null)
            lines.Add(dir.LastWriteTime.ToString("g"));
        var meta = new TextBlock
        {
            Text = string.Join(Environment.NewLine, lines),
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextSecondary.For(dark), dark),
        };
        Grid.SetRow(meta, 1);
        host.Children.Add(meta);
    }

    static string SizeLabel(long bytes)
    {
        if (bytes < 1000)
            return bytes + " B";
        if (bytes < 1_000_000)
            return (bytes / 1000.0).ToString("0.#") + " KB";
        if (bytes < 1_000_000_000)
            return (bytes / 1_000_000.0).ToString("0.#") + " MB";
        return (bytes / 1_000_000_000.0).ToString("0.#") + " GB";
    }
}
