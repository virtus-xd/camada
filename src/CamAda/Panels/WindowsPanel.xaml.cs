using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CamAda.Core.Apps;

namespace CamAda.Panels;

/// <summary>
/// Birden fazla penceresi olan uygulamanın paneli (spec §7.3): canlı önizlemeler ve başlıklar; karta tıklama o pencereyi
/// öne getirir, × pencereyi kapatır, "Yeni pencere" uygulamayı yeniden başlatır.
/// </summary>
public sealed partial class WindowsPanel : UserControl
{
    private const int MaxColumns = 3;

    /// <summary>Çok pencerede panel ekrana sığsın diye en fazla 3 satır kart gösterilir.</summary>
    private const int MaxCards = 9;

    private const double CardWidth = 252; // 236 DIP önizleme + 2 × 8 iç boşluk
    private const double Gap = 10;

    private readonly Action _launchNew;

    public WindowsPanel(DockEntry entry, Action<RunningWindow> activate, Action<RunningWindow> close, Action launchNew)
    {
        InitializeComponent();
        _launchNew = launchNew;

        AppName.Text = entry.Pinned?.Name ?? Path.GetFileNameWithoutExtension(entry.Windows[0].ProcessName);
        WindowCount.Text = $"{entry.Windows.Count} pencere";
        Cards.Width = ContentWidthFor(entry.Windows.Count) + Gap; // her kartın sağındaki boşluk dahil

        foreach (var window in entry.Windows.Take(MaxCards))
        {
            Cards.Children.Add(CreateCard(window, activate, close));
        }
    }

    /// <summary>Panelin (çerçeve dahil) genişliği.</summary>
    public static double WidthFor(int windowCount) => ContentWidthFor(windowCount) + 2 * PanelWindow.Inset;

    private static double ContentWidthFor(int windowCount)
    {
        var columns = Math.Clamp(windowCount, 1, MaxColumns);
        return columns * CardWidth + (columns - 1) * Gap;
    }

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
            Cards.Children.Remove(card);
            if (Cards.Children.Count == 0)
            {
                (Window.GetWindow(this) as PanelWindow)?.Dismiss();
            }
        };

        return card;
    }

    private void NewWindowButton_Click(object sender, RoutedEventArgs e) => _launchNew();
}
