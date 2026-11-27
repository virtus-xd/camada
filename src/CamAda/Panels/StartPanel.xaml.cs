using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CamAda.Core.Power;
using CamAda.Core.Search;
using CamAda.Interop;
using ManagedShell.Common.Helpers;

namespace CamAda.Panels;

/// <summary>Başlat panelinde sabitlenmiş bir uygulama.</summary>
public sealed record StartTile(string Name, ImageSource? Icon, Action Open);

/// <summary>
/// Başlat ve ara paneli: yazmaya hazır arama kutusu, sabitlenmişler, son açılanlar, tüm uygulamalar ve güç işlemleri.
/// Arama kutusunda ↑/↓ listedeki seçimi taşır (boşken son açılanlar), Enter açar; Tab düğmeler arasında döner,
/// Enter/Boşluk odaktaki düğmeye basar; Esc kapatır. Açılan her şeyden sonra panel kapanır.
/// </summary>
public sealed partial class StartPanel : UserControl
{
    public const double PanelWidth = 640;

    private const int RecentCount = 3;
    private static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(3);

    private readonly AppIndex _index;
    private readonly ConfirmGate _restartGate = new(TimeProvider.System, ConfirmWindow);
    private readonly ConfirmGate _shutdownGate = new(TimeProvider.System, ConfirmWindow);

    public StartPanel(AppIndex index, IReadOnlyList<StartTile> pinned)
    {
        InitializeComponent();
        _index = index;

        foreach (var tile in pinned.Append(new StartTile("Ayarlar", null, () => Open(SettingsPages.For(System.Globalization.CultureInfo.CurrentUICulture)[^1]))))
        {
            PinnedGrid.Children.Add(CreateTile(tile));
        }

        RecentList.ItemsSource = _index.RecentFiles(RecentCount).Select(item => new SearchRow(item, _index)).ToList();
        Loaded += (_, _) => Dispatcher.BeginInvoke(() => Keyboard.Focus(Query), DispatcherPriority.Input);
    }

    /// <summary>Arama kutusundayken ↑/↓ ile gezilen liste: sonuçlar, tüm uygulamalar ya da boşken son açılanlar.</summary>
    private ListBox ActiveList =>
        ResultsList.Visibility == Visibility.Visible ? ResultsList
        : AllAppsView.Visibility == Visibility.Visible ? AllAppsList
        : RecentList;

    /// <summary>Odak bir düğmedeyken (Tab ile gidilmiş) yazılan harf arama kutusuna gider.</summary>
    private void Panel_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (Query.IsKeyboardFocused || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0]))
        {
            return;
        }

        e.Handled = true;
        Keyboard.Focus(Query);
        Query.AppendText(e.Text);
        Query.CaretIndex = Query.Text.Length;
    }

    private Button CreateTile(StartTile tile)
    {
        var content = new StackPanel();
        var icon = new Grid { Width = 32, Height = 32, HorizontalAlignment = HorizontalAlignment.Center };
        icon.Children.Add(tile.Icon is null
            ? new TextBlock
            {
                Text = "", // dişli
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            }
            : new Image { Source = tile.Icon });
        content.Children.Add(icon);
        content.Children.Add(new TextBlock
        {
            Text = tile.Name,
            FontSize = 11.5,
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        var button = new Button
        {
            Style = (Style)FindResource("PanelCardButton"),
            Content = content,
            Margin = new Thickness(0, 0, 6, 0),
            Padding = new Thickness(4, 10, 4, 8),
        };
        AutomationProperties.SetName(button, tile.Name);
        button.Click += (_, _) =>
        {
            tile.Open();
            ClosePanel();
        };
        return button;
    }

    private async void Query_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = Query.Text;
        Placeholder.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (string.IsNullOrWhiteSpace(query))
        {
            Show(HomeView);
            return;
        }

        var items = await _index.GetItemsAsync();
        if (Query.Text != query)
        {
            return; // bu arada yeni bir harf yazıldı
        }

        ResultsList.ItemsSource = SearchRanker.Rank(query, items, System.Globalization.CultureInfo.CurrentUICulture).Select(item => new SearchRow(item, _index)).ToList();
        ResultsList.SelectedIndex = 0;
        Show(ResultsList);
    }

    private void Query_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ActiveList is not { Items.Count: > 0 } list)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                list.SelectedIndex = Math.Min(list.SelectedIndex + 1, list.Items.Count - 1);
                list.ScrollIntoView(list.SelectedItem);
                e.Handled = true;
                break;
            case Key.Up:
                list.SelectedIndex = Math.Max(list.SelectedIndex - 1, 0);
                list.ScrollIntoView(list.SelectedItem);
                e.Handled = true;
                break;
            case Key.Enter when list.SelectedItem is SearchRow row:
                Open(row.Item);
                ClosePanel();
                e.Handled = true;
                break;
        }
    }

    private void List_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox list
            && ItemsControl.ContainerFromElement(list, (DependencyObject)e.OriginalSource) is ListBoxItem { DataContext: SearchRow row })
        {
            Open(row.Item);
            ClosePanel();
        }
    }

    private async void AllAppsButton_Click(object sender, RoutedEventArgs e)
    {
        var items = await _index.GetItemsAsync();
        AllAppsList.ItemsSource = items
            .Where(i => i.Kind == SearchKind.App)
            .OrderBy(i => i.Name, StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true))
            .Select(item => new SearchRow(item, _index))
            .ToList();
        AllAppsList.SelectedIndex = 0;
        Show(AllAppsView);
        Keyboard.Focus(Query);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        Query.Clear();
        Show(HomeView);
        Keyboard.Focus(Query);
    }

    private void Show(FrameworkElement view)
    {
        HomeView.Visibility = view == HomeView ? Visibility.Visible : Visibility.Collapsed;
        ResultsList.Visibility = view == ResultsList ? Visibility.Visible : Visibility.Collapsed;
        AllAppsView.Visibility = view == AllAppsView ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        ClosePanel();
        Native.LockWorkStation();
    }

    private void SleepButton_Click(object sender, RoutedEventArgs e)
    {
        ClosePanel();
        PowerHelper.Sleep();
    }

    private void RestartButton_Click(object sender, RoutedEventArgs e) =>
        Confirm(RestartButton, _restartGate, "Yeniden başlat", PowerHelper.Reboot);

    private void ShutdownButton_Click(object sender, RoutedEventArgs e) =>
        Confirm(ShutdownButton, _shutdownGate, "Kapat", PowerHelper.Shutdown);

    /// <summary>İlk tıklamada düğme "Emin misin?" olur; 3 sn içinde ikinci tıklama işlemi yapar, yoksa düğme geri döner.</summary>
    private void Confirm(Button button, ConfirmGate gate, string label, Action action)
    {
        if (gate.Press())
        {
            ClosePanel();
            action();
            return;
        }

        button.Content = "Emin misin?";
        button.Foreground = (Brush)FindResource("AccentBrush");
        var revert = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = ConfirmWindow + TimeSpan.FromMilliseconds(50) };
        revert.Tick += (_, _) =>
        {
            revert.Stop();
            if (!gate.IsArmed)
            {
                button.Content = label;
                button.ClearValue(ForegroundProperty);
            }
        };
        revert.Start();
    }

    private static void Open(SearchItem item) => ItemLauncher.Open(item);

    private void ClosePanel() => (Window.GetWindow(this) as PanelWindow)?.Dismiss();

    /// <summary>Liste satırı: simge tembel yüklenir (sanallaştırmayla yalnız görünen satırlar).</summary>
    public sealed class SearchRow(SearchItem item, AppIndex index)
    {
        private ImageSource? _icon;
        private bool _iconLoaded;

        public SearchItem Item { get; } = item;

        public string Name => Item.Name;

        public ImageSource? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _iconLoaded = true;
                    _icon = index.IconFor(Item);
                }

                return _icon;
            }
        }

        /// <summary>Simgesi olmayan türler için Segoe Fluent Icons karakteri.</summary>
        public string Glyph => Item.Kind switch
        {
            SearchKind.Setting => "",
            SearchKind.Web => "",
            _ when Icon is null => "",
            _ => string.Empty,
        };

        public string KindText => Item.Kind switch
        {
            SearchKind.App => "Uygulama",
            SearchKind.Setting => "Ayar",
            SearchKind.File => "Dosya",
            _ => "Web",
        };

        /// <summary>Ekran okuyucular liste satırının adını buradan okur.</summary>
        public override string ToString() => $"{Name}, {KindText}";
    }
}
