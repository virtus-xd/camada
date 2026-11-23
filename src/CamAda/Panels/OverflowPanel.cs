using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CamAda.Core.Apps;

namespace CamAda.Panels;

/// <summary>"+N" düğmesinin paneli: satıra sığmayan uygulamaların ad + simge listesi; seçilen girişe düğmeyle aynı karar uygulanır.</summary>
public sealed class OverflowPanel : UserControl
{
    public const double PanelWidth = 300;

    public OverflowPanel(IReadOnlyList<DockEntry> entries, Func<DockEntry, ImageSource?> iconFor, Action<DockEntry> choose)
    {
        var list = new StackPanel();
        foreach (var entry in entries)
        {
            var name = entry.Pinned?.Name ?? entry.Windows.FirstOrDefault()?.Title
                ?? Path.GetFileNameWithoutExtension(entry.Key);

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new Image
            {
                Source = iconFor(entry),
                Width = 22,
                Height = 22,
                Margin = new Thickness(0, 0, 10, 0),
            });
            row.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = PanelWidth - 2 * PanelWindow.Inset - 60,
            });

            var button = new Button
            {
                Style = (Style)FindResource("PanelRowButton"),
                Content = row,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            AutomationProperties.SetName(button, name);
            button.Click += (_, _) => choose(entry);
            list.Children.Add(button);
        }

        Content = list;
    }
}
