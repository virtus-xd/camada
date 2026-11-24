using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using CamAda.Core.Apps;
using CamAda.Interop;

namespace CamAda.Panels;

/// <summary>
/// Birden fazla penceresi olan uygulamanın paneli (spec §7.3): canlı önizlemeler ve başlıklar; karta tıklama o pencereyi
/// öne getirir, × pencereyi kapatır, "Yeni pencere" uygulamayı yeniden başlatır. Kartlara sığmayan pencereler altta
/// başlık satırı olarak listelenir.
/// </summary>
public sealed partial class WindowsPanel : UserControl
{
    private const int MaxColumns = 3;

    /// <summary>Çok pencerede panel ekrana sığsın diye en fazla 3 satır kart gösterilir.</summary>
    private const int MaxCards = 9;

    private const double CardWidth = 252; // 236 DIP önizleme + 2 × 8 iç boşluk
    private const double Gap = 10;

    private readonly Action _launchNew;
    private readonly List<DispatcherTimer> _closeWatchers = [];
    private int _windowCount;

    public WindowsPanel(DockEntry entry, Action<RunningWindow> activate, Action<RunningWindow> close, Action launchNew)
    {
        InitializeComponent();
        _launchNew = launchNew;

        AppName.Text = entry.Pinned?.Name ?? Path.GetFileNameWithoutExtension(entry.Windows[0].ProcessName);
        _windowCount = entry.Windows.Count;
        UpdateWindowCount();
        Cards.Width = ContentWidthFor(entry.Windows.Count) + Gap; // her kartın sağındaki boşluk dahil

        foreach (var window in entry.Windows.Take(MaxCards))
        {
            Cards.Children.Add(CreateCard(window, activate, close));
        }

        foreach (var window in entry.Windows.Skip(MaxCards))
        {
            MoreWindows.Children.Add(CreateRow(window, activate));
        }

        MoreWindows.Visibility = MoreWindows.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Unloaded += (_, _) => _closeWatchers.ForEach(t => t.Stop());
    }

    /// <summary>Panelin (çerçeve dahil) genişliği.</summary>
    public static double WidthFor(int windowCount) => ContentWidthFor(windowCount) + 2 * PanelWindow.Inset;

    private static double ContentWidthFor(int windowCount)
    {
        var columns = Math.Clamp(windowCount, 1, MaxColumns);
        return columns * CardWidth + (columns - 1) * Gap;
    }

    private void UpdateWindowCount() => WindowCount.Text = $"{_windowCount} pencere";

    private Button CreateCard(RunningWindow window, Action<RunningWindow> activate, Action<RunningWindow> close)
    {
        var title = new TextBlock
        {
            Text = window.Title,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 6, 0),
        };

        var closeButton = new Button { Style = (Style)FindResource("PanelCloseButton") };
        AutomationProperties.SetName(closeButton, $"{window.Title} penceresini kapat");

        var header = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(closeButton, Dock.Right);
        header.Children.Add(closeButton);
        header.Children.Add(title);

        var body = new StackPanel();
        body.Children.Add(header);
        body.Children.Add(new TaskThumbnail { SourceWindow = window.Handle, Margin = new Thickness(0, 8, 0, 0) });

        var card = new Button
        {
            Style = (Style)FindResource("PanelCardButton"),
            Content = body,
            Width = CardWidth,
            Margin = new Thickness(0, 0, Gap, Gap),
        };
        AutomationProperties.SetName(card, window.Title);

        card.Click += (_, _) => activate(window);
        closeButton.Click += (_, e) =>
        {
            e.Handled = true;
            close(window);
            card.IsEnabled = false; // kapanana kadar soluk; × ikinci kez basılamaz
            card.Opacity = 0.45;
            WatchClose(window.Handle, card);
        };

        return card;
    }

    private Button CreateRow(RunningWindow window, Action<RunningWindow> activate)
    {
        var row = new Button
        {
            Style = (Style)FindResource("PanelRowButton"),
            Content = new TextBlock
            {
                Text = window.Title,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Width = Cards.Width - Gap,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AutomationProperties.SetName(row, window.Title);
        row.Click += (_, _) => activate(window);
        return row;
    }

    /// <summary>
    /// Kart, pencere gerçekten kapanınca kaldırılır. Uygulama kapatmayı reddederse (yönetici penceresi, "Kaydedilsin mi?"
    /// sorusunda İptal) kart <see cref="PendingCloseRule.GiveUpAfter"/> sonra eski hâline döner.
    /// </summary>
    private void WatchClose(nint handle, Button card)
    {
        var started = Stopwatch.GetTimestamp();
        var watcher = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(150) };
        watcher.Tick += (_, _) =>
        {
            switch (PendingCloseRule.Decide(Native.WindowExists(handle), Stopwatch.GetElapsedTime(started)))
            {
                case PendingCloseOutcome.Remove:
                    watcher.Stop();
                    Cards.Children.Remove(card);
                    _windowCount--;
                    UpdateWindowCount();
                    if (Cards.Children.Count == 0 && MoreWindows.Children.Count == 0)
                    {
                        (Window.GetWindow(this) as PanelWindow)?.Dismiss();
                    }

                    break;

                case PendingCloseOutcome.Restore:
                    watcher.Stop();
                    card.IsEnabled = true;
                    card.ClearValue(OpacityProperty);
                    break;
            }
        };

        _closeWatchers.Add(watcher);
        watcher.Start();
    }

    private void NewWindowButton_Click(object sender, RoutedEventArgs e) => _launchNew();
}
