using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Relay.DesignSystem;
using Relay.Features.Clipboard;
using Relay.Platform;

namespace Relay;

internal static class ClipboardPreviewPane
{
    public static void Populate(Grid host, ClipboardItem item, bool dark)
    {
        host.Children.Clear();
        host.RowDefinitions.Clear();
        host.ColumnDefinitions.Clear();
        host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var preview = BuildPreview(item, dark);
        Grid.SetRow(preview, 0);
        var info = BuildInfo(item, dark);
        Grid.SetRow(info, 1);
        host.Children.Add(preview);
        host.Children.Add(info);
    }

    static FrameworkElement BuildPreview(ClipboardItem item, bool dark)
    {
        if (item.Kind == ClipboardKind.Image)
            return ImagePreview(item.ImagePath, dark);
        if (item.Kind == ClipboardKind.File)
        {
            if (ClipboardCapture.IsImagePath(item.FilePath))
                return ImagePreview(item.FilePath, dark);
            return FilePreview(item, dark);
        }

        return TextPreview(item.Text, dark);
    }

    static FrameworkElement ImagePreview(string? path, bool dark)
    {
        var frame = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            CornerRadius = new CornerRadius(Theme.Radius.Card),
            BorderBrush = ThemeBrushes.InkBrush(Theme.Colors.CardStroke.For(dark), dark),
            BorderThickness = new Thickness(1),
            Background = ThemeBrushes.InkBrush(Theme.Colors.CardFill.For(dark), dark),
        };

        var image = ClipboardThumbnails.Load(path, (int)Theme.Size.ClipboardPreviewPixel);
        if (image is null)
        {
            frame.Child = new FontIcon
            {
                Glyph = "\uE91B",
                FontSize = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextTertiary.For(dark), dark),
            };
            return frame;
        }

        frame.Child = new Image
        {
            Source = image,
            Stretch = Stretch.Uniform,
            MaxHeight = Theme.Size.ClipboardMediaHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        return frame;
    }

    static FrameworkElement FilePreview(ClipboardItem item, bool dark)
    {
        var iconPath = item.FilePath is not null ? ShellIcons.FromFile(item.FilePath) : null;
        ImageSource? source = null;
        if (iconPath is not null)
            source = ClipboardThumbnails.Load(iconPath, 128);

        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = Theme.Spacing.Md,
        };
        if (source is not null)
        {
            stack.Children.Add(new Image
            {
                Source = source,
                Width = 64,
                Height = 64,
                Stretch = Stretch.Uniform,
            });
        }
        else
        {
            stack.Children.Add(new FontIcon
            {
                Glyph = "\uE8A5",
                FontSize = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextSecondary.For(dark), dark),
            });
        }

        stack.Children.Add(new TextBlock
        {
            Text = ClipboardPresentation.ListTitle(item),
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextPrimary.For(dark), dark),
        });
        return stack;
    }

    static FrameworkElement TextPreview(string text, bool dark)
    {
        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new TextBlock
            {
                Text = text,
                FontSize = 13,
                FontFamily = new FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = false,
                Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextPrimary.For(dark), dark),
            },
        };
    }

    static FrameworkElement BuildInfo(ClipboardItem item, bool dark)
    {
        var block = new StackPanel
        {
            Spacing = Theme.Spacing.Sm,
            Margin = new Thickness(0, Theme.Spacing.Xl, 0, 0),
        };
        block.Children.Add(new TextBlock
        {
            Text = "Information",
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextSecondary.For(dark), dark),
        });

        var rows = new StackPanel();
        var first = true;
        foreach (var row in InfoRows(item, dark))
        {
            if (!first)
            {
                rows.Children.Add(new Border
                {
                    Height = 1,
                    Background = ThemeBrushes.InkBrush(Theme.Colors.Separator.For(dark), dark),
                });
            }

            first = false;
            rows.Children.Add(row);
        }

        block.Children.Add(rows);
        return block;
    }

    static IEnumerable<FrameworkElement> InfoRows(ClipboardItem item, bool dark)
    {
        var source = ClipboardSourceDisplay.Resolve(item.SourceId);
        if (source is { } resolved)
            yield return InfoRow("Source", resolved.Name, dark, resolved.IconPath);

        yield return InfoRow("Type", ClipboardPresentation.TypeLabel(item), dark);

        if (item.Kind == ClipboardKind.Text)
        {
            yield return InfoRow("Characters", item.Text.Length.ToString("N0"), dark);
            yield return InfoRow("Words", ClipboardPresentation.WordCount(item.Text).ToString("N0"), dark);
        }
        else if (item.Kind == ClipboardKind.Image)
        {
            if (PixelSize(item.ImagePath) is { } px)
                yield return InfoRow("Dimensions", ClipboardPresentation.DimensionsLabel(px.Width, px.Height), dark);
            if (ClipboardPresentation.FileBytes(item.ImagePath) is { } bytes)
                yield return InfoRow("Size", ClipboardPresentation.FileSizeLabel(bytes), dark);
        }
        else if (item.Kind == ClipboardKind.File)
        {
            yield return InfoRow("Path", item.FilePath ?? "", dark);
            if (ClipboardCapture.IsImagePath(item.FilePath) && PixelSize(item.FilePath) is { } filePx)
                yield return InfoRow("Dimensions", ClipboardPresentation.DimensionsLabel(filePx.Width, filePx.Height), dark);
            if (ClipboardPresentation.FileBytes(item.FilePath) is { } fileBytes)
                yield return InfoRow("Size", ClipboardPresentation.FileSizeLabel(fileBytes), dark);
        }

        yield return InfoRow("Copied", ClipboardPresentation.CopiedLabel(item.CreatedAt, DateTime.Now), dark);
    }

    static FrameworkElement InfoRow(string label, string value, bool dark, string? iconPath = null)
    {
        var row = new Grid { Padding = new Thickness(0, Theme.Spacing.Sm, 0, Theme.Spacing.Sm), ColumnSpacing = Theme.Spacing.Sm };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextSecondary.For(dark), dark),
        });

        var valueHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = Theme.Spacing.Sm,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (ClipboardThumbnails.Load(iconPath, 40) is { } icon)
        {
            valueHost.Children.Add(new Image
            {
                Source = icon,
                Width = 20,
                Height = 20,
                Stretch = Stretch.Uniform,
            });
        }

        valueHost.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 13,
            MaxLines = 1,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextPrimary.For(dark), dark),
        });
        Grid.SetColumn(valueHost, 1);
        row.Children.Add(valueHost);
        return row;
    }

    static (int Width, int Height)? PixelSize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        return ClipboardPresentation.PngPixelSize(path)
            ?? FromBitmap(path);
    }

    static (int Width, int Height)? FromBitmap(string path)
    {
        if (ClipboardThumbnails.Load(path, (int)Theme.Size.ClipboardPreviewPixel) is BitmapImage bmp
            && bmp.PixelWidth > 0 && bmp.PixelHeight > 0)
            return (bmp.PixelWidth, bmp.PixelHeight);
        return null;
    }
}
