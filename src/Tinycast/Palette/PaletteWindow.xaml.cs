using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Tinycast.DesignSystem;
using Tinycast.Features.Calculator;
using Tinycast.Features.Clipboard;
using Tinycast.Features.Emoji;
using Tinycast.Features.FileSearch;
using Tinycast.Features.Launcher;
using Tinycast.Palette;
using Tinycast.Platform;
using Windows.System;

namespace Tinycast;

public sealed partial class PaletteWindow : Window
{
    readonly AppCore _core;
    IntPtr _previousHwnd;
    List<PaletteRow> _rows = [];
    Guid _lastFocusToken;
    string? _lastClipClickId;
    DateTime _lastClipClickAt;
    static readonly Dictionary<string, BitmapImage> IconBitmaps = new(StringComparer.OrdinalIgnoreCase);

    public PaletteWindow(AppCore core)
    {
        _core = core;
        InitializeComponent();
        SystemBackdrop = null;
        WindowChrome.ApplyPaletteChrome(this, Theme.Radius.Panel, true);
        ResizePalette();
        WindowChrome.PlacePalette(this, _core.Settings);
        ApplySurface();
        Root.SizeChanged += (_, _) => WindowChrome.ApplyRoundRegion(this, Theme.Radius.Panel);
        _core.Palette.Changed += () => DispatcherQueue.TryEnqueue(Render);
        QueryBox.TextChanged += (_, _) =>
        {
            if (_core.Palette.Query == QueryBox.Text)
                return;
            _core.Palette.Query = QueryBox.Text;
            _core.Palette.Selection = 0;
            _core.Palette.Notify();
        };
        QueryBox.PreviewKeyDown += OnRootKeyDown;
        Closed += (_, _) => { };
    }

    public bool IsPaletteVisible => AppWindow.IsVisible;

    public IntPtr Hwnd => WindowChrome.Hwnd(this);

    public IntPtr PreviousHwnd => _previousHwnd;

    public string? IconPath => Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");

    public void ShowPalette()
    {
        if (!IsPaletteVisible)
        {
            var front = NativeMethods.GetForegroundWindow();
            if (front != IntPtr.Zero && front != Hwnd && !IsCurrentProcess(front))
                _previousHwnd = front;
        }
        ApplySurface();
        ResizePalette();
        WindowChrome.PlacePalette(this, _core.Settings);
        Render();
        Activate();
        WindowChrome.RefreshPaletteChrome(this, Theme.Radius.Panel, IsDark);
        NativeMethods.SetForegroundWindow(Hwnd);
        QueryBox.Focus(FocusState.Programmatic);
        QueryBox.Select(QueryBox.Text.Length, 0);
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            WindowChrome.RefreshPaletteChrome(this, Theme.Radius.Panel, IsDark));
    }

    public void HidePalette(bool restoreFocus)
    {
        if (_core.Settings.PaletteRememberPosition)
        {
            var pos = AppWindow.Position;
            _core.Settings.PaletteLeft = pos.X;
            _core.Settings.PaletteTop = pos.Y;
            _core.Persist();
        }

        AppWindow.Hide();
        if (restoreFocus && _previousHwnd != IntPtr.Zero && _previousHwnd != Hwnd)
            NativeMethods.SetForegroundWindow(_previousHwnd);
    }

    public void ResizePalette()
    {
        var (width, height) = Theme.Size.PalettePanel(_core.Settings.InterfaceSize, _core.Settings.CompactPalette);
        WindowChrome.ResizeDips(this, width, height, client: true);
    }

    static bool IsCurrentProcess(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        return pid == (uint)Environment.ProcessId;
    }

    public void ApplySurface()
    {
        var dark = ThemeBrushes.IsDark(_core.Settings.Appearance, Application.Current.RequestedTheme);
        Root.RequestedTheme = _core.Settings.Appearance switch
        {
            AppAppearance.Light => ElementTheme.Light,
            AppAppearance.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        Root.Background = new SolidColorBrush(ThemeBrushes.PanelFill(dark));
        OpaqueFill.Background = new SolidColorBrush(ThemeBrushes.PanelFill(dark));
        Frost.Background = ThemeBrushes.PanelAcrylic(dark);
        Scrim.Background = ThemeBrushes.Scrim(dark, _core.Settings.PaletteTransparency);
        TabHint.Text = PaletteTabRing.Hint(_core.Palette.Mode, _core.Settings.ClipboardEnabled, _core.Settings.AiEnabled);
        QueryBox.Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextPrimary.For(dark), dark);
        QueryBox.PlaceholderForeground = ThemeBrushes.InkBrush(Theme.Colors.TextTertiary.For(dark), dark);
        TabHint.Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextTertiary.For(dark), dark);
        FooterLabel.Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextPrimary.For(dark), dark);
        ActionsLabel.Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextSecondary.For(dark), dark);
        MenuGlyph.Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextPrimary.For(dark), dark);
        SearchGlyph.Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextSecondary.For(dark), dark);
        BackGlyph.Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextSecondary.For(dark), dark);
        BackButton.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        EmptyTitle.Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextPrimary.For(dark), dark);
        EmptyBody.Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextSecondary.For(dark), dark);
        ActionCapsule.Background = ThemeBrushes.InkBrush(Theme.Colors.GlassFrost.For(dark), dark);
        OpenButton.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        ActionsButton.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        MenuCircle.Background = ThemeBrushes.InkBrush(Theme.Colors.GlassFrost.For(dark), dark);
        var keyBorder = ThemeBrushes.InkBrush(Theme.Colors.Border.For(dark), dark);
        EnterKeyCap.BorderBrush = keyBorder;
        CtrlKeyCap.BorderBrush = keyBorder;
        KKeyCap.BorderBrush = keyBorder;
        WindowChrome.RefreshPaletteChrome(this, Theme.Radius.Panel, dark);
        Render();
    }

    bool IsDark => ThemeBrushes.IsDark(_core.Settings.Appearance, Application.Current.RequestedTheme);

    void Render()
    {
        var dark = ThemeBrushes.IsDark(_core.Settings.Appearance, Application.Current.RequestedTheme);
        if (QueryBox.Text != _core.Palette.Query)
            QueryBox.Text = _core.Palette.Query;
        QueryBox.PlaceholderText = _core.Palette.Mode switch
        {
            PaletteMode.FileSearch => _core.FileSearchCoordinator.Placeholder,
            PaletteMode.Clipboard => "Type to filter entries...",
            _ => "Search",
        };
        var browsing = _core.Palette.Mode == PaletteMode.FileSearch && _core.FileSearchCoordinator.IsBrowsing;
        var stacked = _core.Palette.Stack.Count > 0;
        BackButton.Visibility = browsing || stacked ? Visibility.Visible : Visibility.Collapsed;
        SearchGlyph.Visibility = browsing || stacked ? Visibility.Collapsed : Visibility.Visible;

        if (_core.Palette.FocusToken != _lastFocusToken)
        {
            _lastFocusToken = _core.Palette.FocusToken;
            QueryBox.Focus(FocusState.Programmatic);
        }

        EmptyState.Visibility = Visibility.Collapsed;
        _rows = _core.LauncherCoordinator.Rows(_core.Palette.Query).ToList();
        _core.Palette.Selection = PaletteRowIndex.Clamp(_core.Palette.Selection, _rows.Count);
        RowHost.Children.Clear();
        TabHint.Text = _core.Palette.Mode switch
        {
            PaletteMode.FileSearch => "Ctrl+P  " + _core.FileSearchCoordinator.Filter.Title(),
            PaletteMode.Clipboard => "Ctrl+P  " + ClipboardListFilterLogic.Label(_core.ClipboardCoordinator.Filter),
            _ => PaletteTabRing.Hint(_core.Palette.Mode, _core.Settings.ClipboardEnabled, _core.Settings.AiEnabled),
        };
        TabHint.Visibility = Visibility.Visible;

        if (_core.Palette.Mode == PaletteMode.Emoji)
        {
            RenderEmojiGrid(dark);
            ApplyClipboardSplit(false, dark);
            UpdateFooterAction();
            return;
        }

        if (_rows.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            EmptyTitle.Text = EmptyTitleFor(_core.Palette.Mode);
            EmptyBody.Text = EmptyBodyFor(_core.Palette.Mode);
            ApplyClipboardSplit(false, dark);
            UpdateFooterAction();
            return;
        }

        string? section = null;
        FrameworkElement? selectedElement = null;
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            if (!string.IsNullOrEmpty(row.Section) && row.Section != section)
            {
                section = row.Section;
                RowHost.Children.Add(new TextBlock
                {
                    Text = section,
                    FontSize = 13,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextSecondary.For(dark), dark),
                    Margin = new Thickness(Theme.Spacing.Md, Theme.Spacing.Xs, Theme.Spacing.Md, Theme.Spacing.Xs),
                });
            }

            var element = row.IsLeadCard
                ? BuildLeadCard(row, i == _core.Palette.Selection, dark)
                : BuildRow(row, i == _core.Palette.Selection, dark);
            if (i == _core.Palette.Selection)
                selectedElement = element;
            RowHost.Children.Add(element);
        }

        var selected = _rows[PaletteRowIndex.Clamp(_core.Palette.Selection, _rows.Count)];
        var clipSplit = _core.Palette.Mode == PaletteMode.Clipboard && selected.Id.StartsWith("clip:", StringComparison.Ordinal);
        var filePath = _core.Palette.Mode == PaletteMode.FileSearch ? _core.FileSearchCoordinator.PathOf(selected.Id) : null;
        var fileSplit = filePath is not null && File.Exists(filePath);
        ApplyClipboardSplit(clipSplit || fileSplit, dark);
        if (clipSplit && _core.ClipboardCoordinator.ItemFor(selected.Id) is { } clip)
            ClipboardPreviewPane.Populate(PreviewHost, clip, dark);
        else if (fileSplit && filePath is not null)
            FilePreviewPane.Populate(PreviewHost, filePath, dark);
        else
        {
            PreviewHost.Children.Clear();
            PreviewHost.RowDefinitions.Clear();
        }

        UpdateFooterAction();
        BringSelectionIntoView(selectedElement);
    }

    void RenderEmojiGrid(bool dark)
    {
        var columns = Math.Clamp(_core.Settings.EmojiColumns, 6, 10);
        var selected = PaletteRowIndex.Clamp(_core.Palette.Selection, _rows.Count);
        StackPanel? line = null;
        for (var i = 0; i < _rows.Count; i++)
        {
            if (i % columns == 0)
            {
                line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                RowHost.Children.Add(line);
            }

            var index = i;
            var row = _rows[i];
            var cell = new Button
            {
                Content = row.Glyph,
                Width = 40,
                Height = 40,
                MinWidth = 0,
                MinHeight = 0,
                Padding = new Thickness(0),
                FontSize = 20,
                BorderThickness = new Thickness(i == selected ? 1 : 0),
                Background = i == selected
                    ? ThemeBrushes.InkBrush(Theme.Colors.Selection.For(dark), dark)
                    : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            };
            cell.Click += (_, _) =>
            {
                _core.Palette.Selection = index;
                _core.LauncherCoordinator.Activate(row.Id);
            };
            line!.Children.Add(cell);
        }

        if (_rows.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            EmptyTitle.Text = "Emoji";
            EmptyBody.Text = "Type a name to filter.";
        }
    }

    void ApplyClipboardSplit(bool split, bool dark)
    {
        ListColumn.Width = split
            ? new GridLength(Theme.Size.ClipboardListWidth)
            : new GridLength(1, GridUnitType.Star);
        SplitColumn.Width = split ? GridLength.Auto : new GridLength(0);
        PreviewColumn.Width = split ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        SplitHairline.Visibility = split ? Visibility.Visible : Visibility.Collapsed;
        PreviewHost.Visibility = split ? Visibility.Visible : Visibility.Collapsed;
        SplitHairline.Background = ThemeBrushes.InkBrush(Theme.Colors.Separator.For(dark), dark);
        ListScroll.Padding = split
            ? new Thickness(8, 64, 4, 60)
            : new Thickness(8, 64, 8, 60);
        if (!split)
        {
            PreviewHost.Children.Clear();
            PreviewHost.RowDefinitions.Clear();
        }
    }

    void BringSelectionIntoView(FrameworkElement? element)
    {
        if (element is null)
            return;
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (element.XamlRoot is null)
                return;
            element.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
        });
    }

    static string EmptyTitleFor(PaletteMode mode) => mode switch
    {
        PaletteMode.Clipboard => "Clipboard History",
        PaletteMode.FileSearch => "Search Files",
        PaletteMode.AiChat => "AI Chat",
        PaletteMode.Schedule => "Schedule",
        _ => "No results",
    };

    static string EmptyBodyFor(PaletteMode mode) => mode switch
    {
        PaletteMode.Clipboard => "Copy something. Pins stay at the top.",
        PaletteMode.FileSearch => "Type a drive letter such as D, then Enter to browse.",
        PaletteMode.AiChat => "AI is off until you add a key in Settings.",
        PaletteMode.Schedule => "Turn on Calendar in Settings to list events.",
        _ => "Try another name, or open Settings from the tray.",
    };

    FrameworkElement BuildLeadCard(PaletteRow row, bool selected, bool dark)
    {
        FrameworkElement content;
        if (row.IsError)
        {
            var error = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Theme.Spacing.Md,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            error.Children.Add(new FontIcon
            {
                Glyph = "\uE7BA",
                FontSize = 16,
                Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextSecondary.For(dark), dark),
            });
            error.Children.Add(new TextBlock
            {
                Text = row.Title,
                FontSize = 15,
                Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextSecondary.For(dark), dark),
            });
            content = error;
        }
        else
        {
            var columns = new Grid();
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var left = LeadColumn(row.LeadExpression ?? row.Subtitle ?? "", dark, semibold: false);
            var arrow = new TextBlock
            {
                Text = "\u2192",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextTertiary.For(dark), dark),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(Theme.Spacing.Md, 0, Theme.Spacing.Md, 0),
            };
            var right = LeadColumn(row.Title, dark, semibold: true);
            Grid.SetColumn(left, 0);
            Grid.SetColumn(arrow, 1);
            Grid.SetColumn(right, 2);
            columns.Children.Add(left);
            columns.Children.Add(arrow);
            columns.Children.Add(right);
            content = columns;
        }

        var chrome = new Grid
        {
            Margin = new Thickness(Theme.Spacing.Xs),
            Tag = row.Id,
        };
        chrome.Children.Add(new Border
        {
            Background = ThemeBrushes.InkBrush(Theme.Colors.CardFill.For(dark), dark),
            BorderBrush = ThemeBrushes.InkBrush(Theme.Colors.CardStroke.For(dark), dark),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(Theme.Radius.Card),
        });
        if (selected)
        {
            chrome.Children.Add(new Border
            {
                Background = ThemeBrushes.InkBrush(Theme.Colors.Selection.For(dark), dark),
                CornerRadius = new CornerRadius(Theme.Radius.Card),
            });
        }

        var padded = new Border
        {
            Padding = new Thickness(Theme.Spacing.Xl, Theme.Spacing.Xxl, Theme.Spacing.Xl, Theme.Spacing.Xxl),
            Child = content,
        };
        chrome.Children.Add(padded);
        chrome.PointerPressed += (_, e) =>
        {
            _core.Palette.Selection = _rows.FindIndex(r => r.Id == row.Id);
            if (row.CopyText is not null)
                _core.LauncherCoordinator.Activate(row.Id);
            else
                _core.Palette.Notify();
            e.Handled = true;
        };
        return chrome;
    }

    static FrameworkElement LeadColumn(string text, bool dark, bool semibold)
    {
        var line = new TextBlock
        {
            FontSize = Theme.Size.CalcResult,
            FontWeight = semibold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(Theme.Spacing.Md, 0, Theme.Spacing.Md, 0),
        };
        foreach (var (piece, dim) in CalcSyntax.Highlight(text))
        {
            line.Inlines.Add(new Run
            {
                Text = piece,
                Foreground = ThemeBrushes.InkBrush(
                    dim ? Theme.Colors.TextTertiary.For(dark) : Theme.Colors.TextPrimary.For(dark),
                    dark),
            });
        }

        return line;
    }

    FrameworkElement BuildRow(PaletteRow row, bool selected, bool dark)
    {
        var background = selected
            ? ThemeBrushes.InkBrush(Theme.Colors.Selection.For(dark), dark)
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        var root = new Grid
        {
            Background = background,
            CornerRadius = new CornerRadius(Theme.Radius.Row),
            Padding = new Thickness(Theme.Spacing.Md, row.IsCard ? Theme.Spacing.Md : Theme.Spacing.Sm, Theme.Spacing.Md, row.IsCard ? Theme.Spacing.Md : Theme.Spacing.Sm),
            Margin = new Thickness(Theme.Spacing.Xs, 1, Theme.Spacing.Xs, 1),
            ColumnSpacing = Theme.Spacing.Lg,
            MinHeight = row.IsCard ? 64 : 0,
            Tag = row.Id,
        };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Theme.Size.RowIcon) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new Border
        {
            Width = Theme.Size.RowIcon,
            Height = Theme.Size.RowIcon,
            CornerRadius = new CornerRadius(row.FillIcon ? Theme.Radius.Thumbnail : 6),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = ThemeBrushes.InkBrush(Theme.Colors.ControlSurface.For(dark), dark),
        };
        var image = row.FillIcon
            ? ClipboardThumbnails.Load(row.IconPath, 64)
            : IconSource(row.IconPath);
        if (image is not null)
        {
            icon.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            icon.Child = new Image
            {
                Source = image,
                Width = Theme.Size.RowIcon,
                Height = Theme.Size.RowIcon,
                Stretch = row.FillIcon ? Stretch.UniformToFill : Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        else if (IsEmojiGlyph(row.Glyph))
        {
            icon.Child = new TextBlock
            {
                Text = row.Glyph,
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
        else
        {
            icon.Child = new FontIcon
            {
                Glyph = string.IsNullOrEmpty(row.Glyph) ? "\uE7C5" : row.Glyph,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextPrimary.For(dark), dark),
            };
        }
        Grid.SetColumn(icon, 0);

        var text = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = row.Title,
            FontSize = row.IsCard ? 22 : 15,
            FontWeight = row.IsCard ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextPrimary.For(dark), dark),
        });
        var subtitle = row.IsCard ? row.Preview ?? row.Subtitle : row.Subtitle;
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            text.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextTertiary.For(dark), dark),
            });
        }
        Grid.SetColumn(text, 1);

        var copy = row.CopyText is not null;
        var key = new Border
        {
            MinWidth = copy ? Theme.Spacing.Xl * 4 : Theme.Size.KeyCap,
            Height = copy ? Theme.Size.BarButtonHeight : Theme.Size.KeyCap,
            CornerRadius = new CornerRadius(copy ? Theme.Radius.BarControl : Theme.Radius.KeyCap),
            BorderBrush = ThemeBrushes.InkBrush(Theme.Colors.Border.For(dark), dark),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(copy ? Theme.Spacing.Md : Theme.Spacing.Xs, 0, copy ? Theme.Spacing.Md : Theme.Spacing.Xs, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = new TextBlock
            {
                Text = copy ? "Copy" : "↵",
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ThemeBrushes.InkBrush(Theme.Colors.TextSecondary.For(dark), dark),
            },
            Visibility = selected && _core.Palette.Mode != PaletteMode.Clipboard
                ? Visibility.Visible
                : Visibility.Collapsed,
        };
        Grid.SetColumn(key, 2);

        root.Children.Add(icon);
        root.Children.Add(text);
        root.Children.Add(key);
        root.PointerPressed += (_, e) =>
        {
            _core.Palette.Selection = _rows.FindIndex(r => r.Id == row.Id);
            if (_core.Palette.Mode == PaletteMode.Clipboard)
            {
                var now = DateTime.UtcNow;
                if (_lastClipClickId == row.Id && now - _lastClipClickAt < TimeSpan.FromMilliseconds(400))
                    _core.LauncherCoordinator.Activate(row.Id);
                else
                {
                    _lastClipClickId = row.Id;
                    _lastClipClickAt = now;
                    _core.Palette.Notify();
                }

                e.Handled = true;
                return;
            }

            _core.LauncherCoordinator.Activate(row.Id);
            e.Handled = true;
        };
        return root;
    }

    void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            _core.PaletteCoordinator.HandleEscape();
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Back && _core.Palette.Mode == PaletteMode.FileSearch
            && _core.FileSearchCoordinator.HandleBackspace())
        {
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            if (NativeMethods.IsKeyDown(NativeMethods.VkShift)
                && NativeMethods.IsKeyDown(NativeMethods.VkControl)
                && _rows.Count > 0)
            {
                var row = _rows[PaletteRowIndex.Clamp(_core.Palette.Selection, _rows.Count)];
                if (row.ShowActions && row.LeadExpression is not null)
                {
                    _core.LauncherCoordinator.CopyCalculator(row.Id, withExpression: true);
                    e.Handled = true;
                    return;
                }
            }

            ActivateSelection(reveal: NativeMethods.IsKeyDown(NativeMethods.VkControl));
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Tab)
        {
            _core.PaletteCoordinator.RingTab(NativeMethods.IsKeyDown(NativeMethods.VkShift));
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Down)
        {
            MoveSelection(_core.Palette.Mode == PaletteMode.Emoji
                ? Math.Clamp(_core.Settings.EmojiColumns, 6, 10)
                : 1);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Up)
        {
            if (_core.Palette.Mode == PaletteMode.FileSearch && NativeMethods.IsKeyDown(NativeMethods.VkMenu)
                && _core.FileSearchCoordinator.Pop())
            {
                e.Handled = true;
                return;
            }

            MoveSelection(_core.Palette.Mode == PaletteMode.Emoji
                ? -Math.Clamp(_core.Settings.EmojiColumns, 6, 10)
                : -1);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.C && NativeMethods.IsKeyDown(NativeMethods.VkControl) && _rows.Count > 0)
        {
            var row = _rows[PaletteRowIndex.Clamp(_core.Palette.Selection, _rows.Count)];
            if (_core.Palette.Mode == PaletteMode.FileSearch)
            {
                _core.FileSearchCoordinator.CopyPath(row.Id);
                e.Handled = true;
                return;
            }

            if (_core.Palette.Mode == PaletteMode.Clipboard)
            {
                var item = _core.ClipboardCoordinator.ItemFor(row.Id);
                if (item is not null)
                {
                    _core.ClipboardCoordinator.Copy(item);
                    _core.ShowMessage("Copied");
                    e.Handled = true;
                    return;
                }
            }

            if (!string.IsNullOrEmpty(row.CopyText))
            {
                _core.Clipboard.CopyText(row.CopyText);
                _core.ShowMessage("Copied");
                e.Handled = true;
                return;
            }
        }

        if (e.Key == VirtualKey.P && NativeMethods.IsKeyDown(NativeMethods.VkControl) && _core.Palette.Mode == PaletteMode.FileSearch)
        {
            _core.FileSearchCoordinator.CycleFilter();
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.P && NativeMethods.IsKeyDown(NativeMethods.VkControl) && _core.Palette.Mode == PaletteMode.Clipboard)
        {
            _core.ClipboardCoordinator.CycleFilter();
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.N && NativeMethods.IsKeyDown(NativeMethods.VkControl))
        {
            MoveSelection(1);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.P && NativeMethods.IsKeyDown(NativeMethods.VkControl))
        {
            MoveSelection(-1);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.V && NativeMethods.IsKeyDown(NativeMethods.VkControl) && _core.Palette.Mode == PaletteMode.FileSearch && _rows.Count > 0)
        {
            var row = _rows[PaletteRowIndex.Clamp(_core.Palette.Selection, _rows.Count)];
            _core.FileSearchCoordinator.PasteIntoApp(row.Id);
            e.Handled = true;
            return;
        }

        if (!NativeMethods.IsKeyDown(NativeMethods.VkControl)
            && !NativeMethods.IsKeyDown(NativeMethods.VkMenu)
            && DigitIndex(e.Key) is { } digit)
        {
            if (_core.Palette.Mode == PaletteMode.Clipboard && string.IsNullOrEmpty(_core.Palette.Query))
            {
                if (_core.ClipboardCoordinator.ActivatePinned(digit - 1))
                    e.Handled = true;
                return;
            }

            if (_core.Palette.Mode is PaletteMode.Launcher or PaletteMode.Emoji
                && string.IsNullOrEmpty(_core.Palette.Query)
                && _rows.Count > 0)
            {
                var index = Math.Min(digit - 1, _rows.Count - 1);
                _core.Palette.Selection = index;
                ActivateSelection();
                e.Handled = true;
                return;
            }
        }

        if (e.Key == VirtualKey.Delete && _core.Palette.Mode == PaletteMode.FileSearch && _rows.Count > 0)
        {
            var row = _rows[PaletteRowIndex.Clamp(_core.Palette.Selection, _rows.Count)];
            _core.FileSearchCoordinator.Trash(row.Id);
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Delete && _core.Palette.Mode == PaletteMode.Clipboard)
        {
            DeleteSelection();
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.F && NativeMethods.IsKeyDown(NativeMethods.VkControl) && _rows.Count > 0)
        {
            var row = _rows[PaletteRowIndex.Clamp(_core.Palette.Selection, _rows.Count)];
            _core.Favorites.Toggle(row.Id);
            _core.Palette.Notify();
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.K && NativeMethods.IsKeyDown(NativeMethods.VkControl))
        {
            if (_rows.Count > 0
                && _rows[PaletteRowIndex.Clamp(_core.Palette.Selection, _rows.Count)].ShowActions)
            {
                ActionsButton.Flyout?.ShowAt(ActionsButton);
            }
            else
            {
                MenuCircle.Flyout?.ShowAt(MenuCircle);
            }

            e.Handled = true;
        }
    }

    void PinSelection()
    {
        if (_rows.Count == 0)
            return;
        var row = _rows[PaletteRowIndex.Clamp(_core.Palette.Selection, _rows.Count)];
        _core.ClipboardCoordinator.TogglePin(row.Id);
    }

    void DeleteSelection()
    {
        if (_rows.Count == 0)
            return;
        var row = _rows[PaletteRowIndex.Clamp(_core.Palette.Selection, _rows.Count)];
        _core.ClipboardCoordinator.Delete(row.Id);
    }

    void ActivateSelection(bool reveal = false)
    {
        if (_rows.Count == 0)
            return;
        var index = PaletteRowIndex.Clamp(_core.Palette.Selection, _rows.Count);
        _core.LauncherCoordinator.Activate(_rows[index].Id, reveal);
    }

    void MoveSelection(int step)
    {
        _core.Palette.Selection = PaletteRowIndex.Move(_core.Palette.Selection, step, _rows.Count);
        _core.Palette.Notify();
    }

    static int? DigitIndex(VirtualKey key) => key switch
    {
        VirtualKey.Number1 or VirtualKey.NumberPad1 => 1,
        VirtualKey.Number2 or VirtualKey.NumberPad2 => 2,
        VirtualKey.Number3 or VirtualKey.NumberPad3 => 3,
        VirtualKey.Number4 or VirtualKey.NumberPad4 => 4,
        VirtualKey.Number5 or VirtualKey.NumberPad5 => 5,
        VirtualKey.Number6 or VirtualKey.NumberPad6 => 6,
        VirtualKey.Number7 or VirtualKey.NumberPad7 => 7,
        VirtualKey.Number8 or VirtualKey.NumberPad8 => 8,
        VirtualKey.Number9 or VirtualKey.NumberPad9 => 9,
        _ => null,
    };

    void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (_core.Palette.Mode == PaletteMode.FileSearch && _core.FileSearchCoordinator.Pop())
            return;
        _core.Palette.Pop();
    }

    void OnActionsFlyoutOpening(object sender, object e)
    {
        ActionsFlyout.Items.Clear();
        if (_rows.Count == 0)
            return;
        var row = _rows[PaletteRowIndex.Clamp(_core.Palette.Selection, _rows.Count)];
        if (_core.Palette.Mode == PaletteMode.Clipboard)
        {
            var item = _core.ClipboardCoordinator.ItemFor(row.Id);
            var pin = new MenuFlyoutItem { Text = item?.Pinned == true ? "Unpin Entry" : "Pin Entry" };
            pin.Click += (_, _) => _core.ClipboardCoordinator.TogglePin(row.Id);
            var delete = new MenuFlyoutItem { Text = "Delete" };
            delete.Click += (_, _) => _core.ClipboardCoordinator.Delete(row.Id);
            ActionsFlyout.Items.Add(pin);
            ActionsFlyout.Items.Add(delete);
            return;
        }

        if (row.Id == "calc-live" || row.Id.StartsWith("calc-hist:", StringComparison.Ordinal))
        {
            var copyAnswer = new MenuFlyoutItem { Text = "Copy Answer" };
            copyAnswer.Click += OnCopyAnswerClick;
            ActionsFlyout.Items.Add(copyAnswer);
            var copyCalc = new MenuFlyoutItem { Text = "Copy Calculation" };
            copyCalc.Click += OnCopyCalculationClick;
            ActionsFlyout.Items.Add(copyCalc);
            return;
        }

        if (row.Kind == AppEntryKind.Application)
        {
            var app = _core.Apps.FirstOrDefault(a => a.Id == row.Id);
            if (app is null)
                return;
            AddAppAction("Open", () =>
            {
                _core.PaletteCoordinator.HidePalette(restoreFocus: false);
                ProcessLauncher.Launch(app);
            });
            AddAppAction("Show in Explorer", () =>
            {
                _core.PaletteCoordinator.HidePalette(restoreFocus: false);
                AppProcess.Reveal(app);
            });
            AddAppAction(_core.Favorites.IsFavorite(app.Id) ? "Remove Favorite" : "Add Favorite", () =>
            {
                _core.Favorites.Toggle(app.Id);
                _core.Palette.Notify();
            });
            AddAppAction(_core.Visibility.IsHidden(app.Id) ? "Show in Search" : "Hide from Search", () =>
            {
                if (_core.Visibility.IsHidden(app.Id))
                    _core.Visibility.Remove(app.Id);
                else
                    _core.Visibility.Set(app.Id, true);
                _core.Palette.Notify();
            });
            AddAppAction("Restart", () =>
            {
                _core.PaletteCoordinator.HidePalette();
                AppProcess.Restart(app);
            });
            AddAppAction("Quit", () =>
            {
                _core.PaletteCoordinator.HidePalette();
                AppProcess.Quit(app);
            });
            AddAppAction("Uninstall leftovers", () =>
                _core.PaletteCoordinator.ShowPalette(PaletteMode.Uninstall, seeding: app.Title));
        }

        void AddAppAction(string title, Action action)
        {
            var item = new MenuFlyoutItem { Text = title };
            item.Click += (_, _) => action();
            ActionsFlyout.Items.Add(item);
        }
    }

    static ImageSource? IconSource(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        if (IconBitmaps.TryGetValue(path, out var existing))
            return existing;
        try
        {
            var bmp = new BitmapImage
            {
                UriSource = new Uri(Path.GetFullPath(path)),
                DecodePixelWidth = 128,
            };
            IconBitmaps[path] = bmp;
            return bmp;
        }
        catch (Exception)
        {
            return null;
        }
    }

    static bool IsEmojiGlyph(string glyph)
    {
        if (string.IsNullOrEmpty(glyph))
            return false;
        if (char.IsSurrogate(glyph[0]))
            return true;
        return glyph[0] < '\uE000'
            && char.GetUnicodeCategory(glyph[0]) == System.Globalization.UnicodeCategory.OtherSymbol;
    }

    void UpdateFooterAction()
    {
        if (_rows.Count == 0)
        {
            FooterLabel.Text = "Open";
            ActionsButton.Visibility = Visibility.Collapsed;
            ActionCapsule.Visibility = Visibility.Visible;
            return;
        }

        var row = _rows[PaletteRowIndex.Clamp(_core.Palette.Selection, _rows.Count)];
        if (row.IsError)
        {
            ActionCapsule.Visibility = Visibility.Collapsed;
            return;
        }

        ActionCapsule.Visibility = Visibility.Visible;
        FooterLabel.Text = row.PrimaryAction
            ?? (row.Id.StartsWith("fs:dir:", StringComparison.Ordinal)
                || row.Id.StartsWith("fs:vol:", StringComparison.Ordinal)
                || row.Id == "fs:up"
                ? "Enter"
                : row.CopyText is not null
                ? "Copy"
                : _core.Palette.Mode switch
                {
                    PaletteMode.Clipboard or PaletteMode.Emoji => "Paste",
                    PaletteMode.Snippets => "Paste",
                    _ => row.Kind == AppEntryKind.Application ? "Open" : "Open",
                });
        ActionsButton.Visibility = row.ShowActions || row.Kind == AppEntryKind.Application
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    void OnOpenClick(object sender, RoutedEventArgs e) => ActivateSelection();

    void OnCopyAnswerClick(object sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0)
            return;
        var row = _rows[PaletteRowIndex.Clamp(_core.Palette.Selection, _rows.Count)];
        _core.LauncherCoordinator.CopyCalculator(row.Id, withExpression: false);
    }

    void OnCopyCalculationClick(object sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0)
            return;
        var row = _rows[PaletteRowIndex.Clamp(_core.Palette.Selection, _rows.Count)];
        _core.LauncherCoordinator.CopyCalculator(row.Id, withExpression: true);
    }

    void OnSettingsClick(object sender, RoutedEventArgs e) => _core.LauncherCoordinator.Activate(Tinycast.Features.Commands.BuiltinCommands.Settings);

    void OnQuitClick(object sender, RoutedEventArgs e) => _core.Quit();
}
