using System.Windows;
using System.Windows.Controls;
using CamAda.Media;

namespace CamAda.Panels;

/// <summary>Genişlemiş adadaki müzik bölümünün paneli (spec §7.4 "Müzik"): kapaklı çalan şarkı kartı ve üç sıradaki şarkı.</summary>
public static class MusicPanel
{
    public const double Width = 360;

    /// <summary>footer: Spotify'a bağlanma görünümü (Görev 8); yoksa null.</summary>
    public static FrameworkElement Create(NowPlayingService media, ISpotifyExtras? extras, FrameworkElement? footer)
    {
        var content = new StackPanel();
        content.Children.Add(new NowPlayingCard(media, showCover: true, queueCount: 3) { Extras = extras, MinHeight = 150 });
        if (footer is not null)
        {
            footer.Margin = new Thickness(0, 14, 0, 0);
            content.Children.Add(footer);
        }

        return content;
    }
}
