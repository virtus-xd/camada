using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CamAda.Core.Record;
using CamAda.Media;

namespace CamAda.Record;

/// <summary>
/// Plağın müzik kartı (spec §7.8): 300×196 DIP, 24 köşe, ince açık kenar; arka planı duvar kağıdı camı (kart masaüstünün
/// üstünde durur). İçerik <see cref="NowPlayingCard"/>: şarkı, "Sanatçı · Albüm", beğen, ilerleme, denetimler, bir sıradaki
/// şarkı; parça yoksa "Şu an bir şey çalmıyor" ve "Spotify'ı aç". Kapağı plağın etiketi gösterdiği için kartta kapak yok.
/// </summary>
public sealed class RecordCard : Border
{
    private readonly NowPlayingCard _content;

    public RecordCard(NowPlayingService media)
    {
        Width = RecordLayout.CardWidth;
        MinHeight = RecordLayout.CardHeight; // "Sıradaki" varken içeriğe göre uzar
        VerticalAlignment = VerticalAlignment.Center;
        CornerRadius = new CornerRadius(24);
        BorderThickness = new Thickness(1);
        BorderBrush = (Brush)FindResource("GlassEdgeBrush");
        Background = (Brush)FindResource("SolidTintBrush"); // duvar kağıdı camı bağlanana kadar ve duvar kağıdı yoksa

        TintLayer = new Border { CornerRadius = new CornerRadius(23), IsHitTestVisible = false };
        _content = new NowPlayingCard(media, showCover: false, queueCount: 1)
        {
            Margin = new Thickness(18, 16, 18, 14),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var root = new Grid();
        root.Children.Add(TintLayer);
        root.Children.Add(new Border // üst kenardaki ince parıltı (adadaki gibi)
        {
            CornerRadius = new CornerRadius(23),
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = (Brush)FindResource("GlassHighlightBrush"),
            IsHitTestVisible = false,
        });
        root.Children.Add(_content);
        Child = root;
    }

    /// <summary>Duvar kağıdı camının bulanık görüntünün üstüne koyduğu ton katmanı.</summary>
    public Border TintLayer { get; }

    public ISpotifyExtras? Extras
    {
        get => _content.Extras;
        set => _content.Extras = value;
    }
}
