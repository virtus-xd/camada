using System.Windows;
using System.Windows.Media;
using CamAda.Core.Glass;

namespace CamAda.Glass;

/// <summary>
/// Efektsiz yumuşak gölge: kendi boyutundaki hap şeklinin (köşe = kısa kenarın yarısı; kare boyutta daire) gölgesini
/// <see cref="SoftShadowProfile"/> katmanlarıyla çizer, sınırlarının dışına taşar. DropShadowEffect'in aksine altında ya da
/// üstünde dönen bir şey olduğunda her karede yeniden hesaplanmaz. Gölgesi çizilen öğenin arkasına, aynı konum ve boyutta konur.
/// </summary>
public sealed class SoftShadow : FrameworkElement
{
    private const int LayerCount = 12;

    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(Color), typeof(SoftShadow),
        new FrameworkPropertyMetadata(Color.FromRgb(0x04, 0x06, 0x1A), FrameworkPropertyMetadataOptions.AffectsRender, OnStyleChanged));

    public static readonly DependencyProperty BlurRadiusProperty = DependencyProperty.Register(
        nameof(BlurRadius), typeof(double), typeof(SoftShadow),
        new FrameworkPropertyMetadata(30.0, FrameworkPropertyMetadataOptions.AffectsRender, OnStyleChanged));

    public static readonly DependencyProperty DepthProperty = DependencyProperty.Register(
        nameof(Depth), typeof(double), typeof(SoftShadow),
        new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShadowOpacityProperty = DependencyProperty.Register(
        nameof(ShadowOpacity), typeof(double), typeof(SoftShadow),
        new FrameworkPropertyMetadata(0.45, FrameworkPropertyMetadataOptions.AffectsRender, OnStyleChanged));

    private (double Outset, Brush Brush)[]? _layers;

    public SoftShadow()
    {
        IsHitTestVisible = false;
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public double BlurRadius
    {
        get => (double)GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }

    /// <summary>Gölgenin aşağı kayması (DIP).</summary>
    public double Depth
    {
        get => (double)GetValue(DepthProperty);
        set => SetValue(DepthProperty, value);
    }

    public double ShadowOpacity
    {
        get => (double)GetValue(ShadowOpacityProperty);
        set => SetValue(ShadowOpacityProperty, value);
    }

    private static void OnStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((SoftShadow)d)._layers = null;

    protected override void OnRender(DrawingContext dc)
    {
        var size = RenderSize;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        _layers ??= SoftShadowProfile.Layers(BlurRadius, ShadowOpacity, LayerCount)
            .Select(l => (l.Outset, Brush: Frozen(new SolidColorBrush(Color) { Opacity = l.Alpha })))
            .ToArray();

        foreach (var (outset, brush) in _layers)
        {
            var rect = new Rect(-outset, -outset + Depth, size.Width + 2 * outset, size.Height + 2 * outset);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                continue;
            }

            var radius = Math.Min(rect.Width, rect.Height) / 2;
            dc.DrawRoundedRectangle(brush, null, rect, radius, radius);
        }
    }

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
