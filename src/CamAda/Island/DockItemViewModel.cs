using System.Windows;
using System.Windows.Media;
using CamAda.Core.Apps;

namespace CamAda.Island;

/// <summary>Adadaki bir uygulama düğmesinin görünümü. Satır her değişimde yeniden kurulur; bu sınıf değişmezdir.</summary>
public sealed class DockItemViewModel
{
    private DockItemViewModel(DockEntry? entry, ImageSource? icon, ImageSource? badge, bool isMissing,
        IReadOnlyList<DockEntry>? overflow, Thickness margin)
    {
        Entry = entry;
        Icon = icon;
        Badge = badge;
        IsMissing = isMissing;
        Overflow = overflow;
        Margin = margin;
    }

    public static DockItemViewModel ForEntry(DockEntry entry, ImageSource? icon, ImageSource? badge, bool isMissing, bool firstUnpinned) =>
        new(entry, icon, badge, isMissing, null, firstUnpinned ? new Thickness(6, 0, 0, 0) : new Thickness(0));

    public static DockItemViewModel ForOverflow(IReadOnlyList<DockEntry> overflow) =>
        new(null, null, null, false, overflow, new Thickness(0));

    public DockEntry? Entry { get; }

    public ImageSource? Icon { get; }

    /// <summary>Uygulamanın görev çubuğu rozeti (ör. Discord okunmamış).</summary>
    public ImageSource? Badge { get; }

    /// <summary>Sabitlenmiş uygulamanın dosyası artık yok.</summary>
    public bool IsMissing { get; }

    /// <summary>"+N" düğmesiyse altındaki girişler.</summary>
    public IReadOnlyList<DockEntry>? Overflow { get; }

    public Thickness Margin { get; }

    public bool IsOverflow => Overflow is not null;

    public string OverflowText => Overflow is null ? "" : $"+{Overflow.Count}";

    public double IconOpacity => IsMissing ? 0.4 : 1.0;

    public Visibility BadgeVisibility => Badge is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility FirstDotVisibility => Entry?.IsRunning == true ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SecondDotVisibility => Entry?.Windows.Count >= 2 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Dikkat isteyen uygulamada noktalar vurgu renginde.</summary>
    public Brush DotBrush => Entry?.NeedsAttention == true
        ? (Brush)Application.Current.FindResource("AccentBrush")
        : (Brush)Application.Current.FindResource("TextBrush");

    public string AccessibleName
    {
        get
        {
            if (IsOverflow)
            {
                return $"{Overflow!.Count} uygulama daha";
            }

            var name = Entry!.Pinned?.Name ?? Entry.Windows[0].Title;
            var parts = new List<string> { name };
            if (Entry.Windows.Count >= 2)
            {
                parts.Add($"{Entry.Windows.Count} pencere");
            }
            else if (Entry.IsRunning)
            {
                parts.Add("açık");
            }

            if (Entry.NeedsAttention)
            {
                parts.Add("dikkat istiyor");
            }

            return string.Join(", ", parts);
        }
    }
}
