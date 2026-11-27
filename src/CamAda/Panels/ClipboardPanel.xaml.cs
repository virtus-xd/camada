using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CamAda.Clips;
using CamAda.Core.Clipboard;
using CamAda.Core.Text;

namespace CamAda.Panels;

/// <summary>
/// Pano paneli (tasarım "Panel"): sabitlenenler üstte, son kopyalananlar altta. Satıra tıklamak ya da Enter onu seçer.
/// Kısayolla açılmamışsa (<c>pasteOnChoose == false</c>) seçilen panoya alınır, satırda kısa süre "Kopyalandı" görünür ve
/// panel açık kalır; kısayolla açılmışsa yalnız <see cref="Chosen"/> yayınlanır (yapıştırmayı App yapar).
/// </summary>
public partial class ClipboardPanel : UserControl
{
    public const double PanelWidth = 380;

    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private static readonly FontFamily CodeFont = new("Cascadia Mono, Consolas");

    private readonly ClipboardService _clips;
    private readonly ScreenshotLibrary? _shots;
    private readonly bool _pasteOnChoose;
    private readonly DispatcherTimer _copiedTimer;
    private readonly List<Button> _rows = new();
    private readonly List<Button> _shotButtons = new();
    private string? _copiedId;
    private bool _copyFailed; // _copiedId'nin satırında "Kopyalandı" yerine "Kopyalanamadı"

    public ClipboardPanel(ClipboardService clips, ScreenshotLibrary? shots, bool pasteOnChoose)
    {
        InitializeComponent();
        _clips = clips;
        _shots = shots;
        _pasteOnChoose = pasteOnChoose;
        _copiedTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, (_, _) =>
        {
            _copiedTimer!.Stop();
            _copiedId = null;
            _copyFailed = false;
            Rebuild(FocusedId(), focusFirst: false);
            RebuildShots();
        }, Dispatcher);
        _copiedTimer.Stop();

        Loaded += (_, _) =>
        {
            _clips.Changed += OnChanged;
            if (_shots is not null)
            {
                _shots.Arrived += OnShotArrived;
            }

            Rebuild(focusId: null, focusFirst: true);
            RebuildShots();
            if (_rows.Count == 0 && _shotButtons.Count > 0)
            {
                Dispatcher.BeginInvoke(() => Keyboard.Focus(_shotButtons[0]), DispatcherPriority.Input);
            }
        };
        Unloaded += (_, _) =>
        {
            _copiedTimer.Stop();
            _clips.Changed -= OnChanged;
            if (_shots is not null)
            {
                _shots.Arrived -= OnShotArrived;
            }
        };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>Kullanıcı bir öğe seçti (tıklama ya da Enter).</summary>
    public event EventHandler<ClipChoice>? Chosen;

    private void OnChanged(object? sender, EventArgs e) => Rebuild(FocusedId(), focusFirst: false);

    private void OnShotArrived(object? sender, ScreenshotFile e) => RebuildShots();

    /// <summary>Şerit: en yeni 10 ekran görüntüsü; tıklama/Enter seçer, sağ tık Düzenle / Klasörde göster.</summary>
    private void RebuildShots()
    {
        var focused = _shotButtons.Find(b => b.IsKeyboardFocused)?.Tag as string;
        ShotsList.Children.Clear();
        _shotButtons.Clear();
        var files = _shots?.Recent() ?? [];
        foreach (var file in files)
        {
            ShotsList.Children.Add(CreateShotButton(file));
        }

        NoShotsText.Visibility = files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        OpenFolderButton.Visibility = _shots?.Folder is { } folder && Directory.Exists(folder) ? Visibility.Visible : Visibility.Collapsed;
        if (_shotButtons.Find(b => Equals(b.Tag, focused)) is { } target)
        {
            Dispatcher.BeginInvoke(() => Keyboard.Focus(target), DispatcherPriority.Input);
        }
    }

    private Button CreateShotButton(ScreenshotFile file)
    {
        var name = Path.GetFileNameWithoutExtension(file.Path);
        var content = new Grid();
        content.Children.Add(new Border
        {
            Width = 96,
            Height = 54,
            CornerRadius = new CornerRadius(8),
            Background = _shots!.Thumbnail(file) is { } thumbnail
                ? new ImageBrush(thumbnail) { Stretch = Stretch.UniformToFill }
                : (Brush)FindResource("PanelItemBrush"),
        });
        if (file.Path == _copiedId)
        {
            content.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(0xB0, 0x14, 0x18, 0x2A)),
                Child = new TextBlock
                {
                    Text = _copyFailed ? UiStrings.Island_CopyFailed : UiStrings.Island_Copied,
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource(_copyFailed ? "WarningBrush" : "AccentBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            });
        }

        var button = new Button
        {
            Style = (Style)FindResource("PanelCardButton"),
            Padding = new Thickness(3),
            Margin = new Thickness(0, 0, 6, 0),
            Tag = file.Path,
            Content = content,
            ToolTip = name,
        };
        AutomationProperties.SetName(button, string.Format(UiStrings.Clip_ScreenshotA11y, name));
        button.Click += (_, _) => ChooseShot(file.Path);

        var edit = new MenuItem { Header = UiStrings.Island_Edit };
        edit.Click += (_, _) => ScreenshotLibrary.OpenForEdit(file.Path);
        var show = new MenuItem { Header = UiStrings.Clip_ShowInFolder };
        show.Click += (_, _) => ScreenshotLibrary.ShowInFolder(file.Path);
        button.ContextMenu = new ContextMenu { Items = { edit, show } };

        _shotButtons.Add(button);
        return button;
    }

    private void ChooseShot(string path)
    {
        Chosen?.Invoke(this, new ScreenshotChoice(path));
        if (!_pasteOnChoose)
        {
            _ = CopyShotAsync(path);
        }
    }

    private async Task CopyShotAsync(string path)
    {
        _copiedTimer.Stop();
        _copiedId = path;
        ShowCopyResult(await _clips.PutAsync(new ScreenshotChoice(path)));
        RebuildShots();
    }

    /// <summary>Kopyalama sonucu satırda kısa süre görünür; başarısızlık daha uzun kalır (pano başka uygulamada kilitli).</summary>
    private void ShowCopyResult(bool copied)
    {
        _copyFailed = !copied;
        _copiedTimer.Interval = TimeSpan.FromSeconds(copied ? 1 : 2.5);
        _copiedTimer.Start();
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (_shots?.Folder is { } folder)
        {
            ScreenshotLibrary.OpenFolder(folder);
        }
    }

    private void ShotsScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ShotsScroll.ScrollToHorizontalOffset(ShotsScroll.HorizontalOffset - e.Delta / 2.0);
        e.Handled = true;
    }

    private void Rebuild(string? focusId, bool focusFirst)
    {
        PinnedList.Children.Clear();
        RecentList.Children.Clear();
        _rows.Clear();

        var now = DateTimeOffset.Now;
        var history = _clips.History;
        foreach (var item in history.Pinned)
        {
            PinnedList.Children.Add(CreateRow(item, now));
        }

        foreach (var item in history.Recent)
        {
            RecentList.Children.Add(CreateRow(item, now));
        }

        PinnedSeparator.Visibility = history.Pinned.Count > 0 && history.Recent.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClearButton.Visibility = history.Recent.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        var target = _rows.Find(r => Equals(r.Tag, focusId)) ?? (focusFirst ? _rows.FirstOrDefault() : null);
        if (target is not null)
        {
            Dispatcher.BeginInvoke(() => Keyboard.Focus(target), DispatcherPriority.Input);
        }
    }

    private Button CreateRow(ClipItem item, DateTimeOffset now)
    {
        var row = new Button { Style = (Style)FindResource("PanelRowButton"), Tag = item.Id };
        AutomationProperties.SetName(row, $"{KindName(item.Kind)}: {Preview(item)}");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(CreateIcon(item));

        var preview = new TextBlock
        {
            Text = Preview(item),
            Margin = new Thickness(10, 0, 8, 0),
            FontSize = item.Kind == ClipKind.Code ? 12 : 12.5,
            FontFamily = item.Kind == ClipKind.Code ? CodeFont : FontFamily,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = 36,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(preview, 1);
        grid.Children.Add(preview);

        var copied = item.Id == _copiedId;
        var age = new TextBlock
        {
            Text = !copied ? ClipAge.Format(item.CopiedAt, now, System.Globalization.CultureInfo.CurrentUICulture) : _copyFailed ? UiStrings.Island_CopyFailed : UiStrings.Island_Copied,
            FontSize = 11.5,
            FontWeight = copied ? FontWeights.Bold : FontWeights.SemiBold,
            Foreground = (Brush)FindResource(!copied ? "TextSecondaryBrush" : _copyFailed ? "WarningBrush" : "AccentBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(age, 2);
        grid.Children.Add(age);

        var pin = CreateIconButton(item.Pinned ? "" : "", item.Pinned ? UiStrings.Clip_Unpin : UiStrings.Clip_Pin, () =>
        {
            if (item.Pinned)
            {
                _clips.Unpin(item.Id);
            }
            else
            {
                _clips.Pin(item.Id);
            }
        });
        if (item.Pinned)
        {
            pin.Foreground = (Brush)FindResource("AccentBrush");
        }

        var remove = CreateIconButton("", UiStrings.Clip_Delete, () => _clips.Remove(item.Id));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0) };
        actions.Children.Add(pin);
        actions.Children.Add(remove);
        Grid.SetColumn(actions, 3);
        grid.Children.Add(actions);

        // Sabitle ve sil yalnız satırın üzerindeyken görünür; sabitli öğenin işareti hep görünür.
        void ShowActions(bool show)
        {
            pin.Visibility = show || item.Pinned ? Visibility.Visible : Visibility.Hidden;
            remove.Visibility = show ? Visibility.Visible : Visibility.Hidden;
        }

        ShowActions(false);
        row.MouseEnter += (_, _) => ShowActions(true);
        row.MouseLeave += (_, _) => ShowActions(false);

        row.Content = grid;
        row.Click += (_, _) => Choose(item.Id);
        _rows.Add(row);
        return row;
    }

    private FrameworkElement CreateIcon(ClipItem item)
    {
        if (item.Kind == ClipKind.Image && _clips.ThumbnailFor(item) is { } thumbnail)
        {
            return new Image
            {
                Source = thumbnail,
                Width = 40,
                Height = 28,
                Stretch = Stretch.UniformToFill,
                Clip = new RectangleGeometry(new Rect(0, 0, 40, 28), 4, 4),
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        return new TextBlock
        {
            Text = item.Kind switch { ClipKind.Link => "", ClipKind.Code => "", _ => "" },
            FontFamily = IconFont,
            FontSize = 15,
            Width = 40,
            TextAlignment = TextAlignment.Center,
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private Button CreateIconButton(string glyph, string name, Action action)
    {
        var button = new Button
        {
            Style = (Style)FindResource("RowIconButton"),
            Content = new TextBlock { Text = glyph, FontFamily = IconFont, FontSize = 12 },
        };
        AutomationProperties.SetName(button, name);
        button.Click += (_, e) =>
        {
            e.Handled = true; // satırın kendi tıklamasına (seçim) gitmesin
            action();
        };
        return button;
    }

    private void Choose(string id)
    {
        Chosen?.Invoke(this, new HistoryChoice(id));
        if (!_pasteOnChoose)
        {
            _ = CopyAsync(id);
        }
    }

    private async Task CopyAsync(string id)
    {
        _copiedTimer.Stop();
        _copiedId = id;
        ShowCopyResult(await _clips.PutAsync(new HistoryChoice(id)));
        Rebuild(id, focusFirst: false);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e) => _clips.ClearUnpinned();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var shot = _shotButtons.FindIndex(b => b.IsKeyboardFocused);
        if (shot >= 0)
        {
            switch (e.Key)
            {
                case Key.Left:
                    Keyboard.Focus(_shotButtons[Math.Max(0, shot - 1)]);
                    e.Handled = true;
                    break;
                case Key.Right:
                    Keyboard.Focus(_shotButtons[Math.Min(_shotButtons.Count - 1, shot + 1)]);
                    e.Handled = true;
                    break;
                case Key.Up when _rows.Count > 0:
                    Keyboard.Focus(_rows[^1]);
                    e.Handled = true;
                    break;
                case Key.Down or Key.Up:
                    e.Handled = true;
                    break;
            }

            return;
        }

        var index = _rows.FindIndex(r => r.IsKeyboardFocused);
        switch (e.Key)
        {
            case Key.Down when index == _rows.Count - 1 && _shotButtons.Count > 0:
                Keyboard.Focus(_shotButtons[0]);
                e.Handled = true;
                break;
            case Key.Down when _rows.Count > 0:
                Keyboard.Focus(_rows[Math.Min(_rows.Count - 1, index + 1)]);
                e.Handled = true;
                break;
            case Key.Up when _rows.Count > 0:
                Keyboard.Focus(_rows[Math.Max(0, index - 1)]);
                e.Handled = true;
                break;
            case Key.Delete when index >= 0:
                var id = (string)_rows[index].Tag;
                _clips.Remove(id);
                if (_rows.Count > 0)
                {
                    Keyboard.Focus(_rows[Math.Min(index, _rows.Count - 1)]);
                }

                e.Handled = true;
                break;
        }
    }

    private string? FocusedId() => _rows.Find(r => r.IsKeyboardFocused)?.Tag as string;

    private static string KindName(ClipKind kind) => kind switch
    {
        ClipKind.Link => UiStrings.Clip_KindLink,
        ClipKind.Code => UiStrings.Clip_KindCode,
        ClipKind.Image => UiStrings.Clip_KindImage,
        _ => UiStrings.Clip_KindText,
    };

    /// <summary>Satırdaki önizleme: görselde boyut, metinde ilk iki dolu satır (en çok 300 karakter).</summary>
    private static string Preview(ClipItem item)
    {
        if (item.Kind == ClipKind.Image)
        {
            return string.Format(UiStrings.Clip_ImageSize, item.Payload.Width, item.Payload.Height);
        }

        var text = item.Payload.Text ?? "";
        if (text.Length > ClipClassifier.MaxInspected)
        {
            text = text[..ClipClassifier.MaxInspected]; // ilk iki dolu satır için 16 MB'ın tamamını bölme
        }

        var lines = text.Replace("\r\n", "\n").Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Take(2)
            .Select(l => item.Kind == ClipKind.Code ? l.TrimEnd() : l.Trim());
        var preview = string.Join("\n", lines);
        return preview.Length > 300 ? preview[..300] : preview;
    }
}
