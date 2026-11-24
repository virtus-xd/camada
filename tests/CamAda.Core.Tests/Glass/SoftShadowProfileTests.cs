using CamAda.Core.Glass;

namespace CamAda.Core.Tests.Glass;

public class SoftShadowProfileTests
{
    private const double Blur = 30;
    private const double Opacity = 0.45;

    private static readonly IReadOnlyList<ShadowLayer> Layers = SoftShadowProfile.Layers(Blur, Opacity, layerCount: 12);

    /// <summary>Kenardan <paramref name="distance"/> DIP dışarıdaki noktada iç içe katmanların toplam saydamlığı.</summary>
    private static double AlphaAt(double distance) =>
        1 - Layers.Where(l => l.Outset >= distance).Aggregate(1.0, (keep, l) => keep * (1 - l.Alpha));

    [Fact]
    public void Layers_go_from_outermost_to_innermost()
    {
        Assert.Equal(12, Layers.Count);
        for (var i = 1; i < Layers.Count; i++)
        {
            Assert.True(Layers[i].Outset < Layers[i - 1].Outset);
        }
    }

    [Fact]
    public void Shadow_fades_out_at_blur_radius() =>
        Assert.True(AlphaAt(Blur) < 0.01, $"{AlphaAt(Blur)}");

    [Fact]
    public void Shadow_is_half_strength_at_the_edge() =>
        Assert.InRange(AlphaAt(0), Opacity * 0.4, Opacity * 0.6);

    [Fact]
    public void Shadow_reaches_full_strength_inside() =>
        Assert.InRange(AlphaAt(-Blur / 2), Opacity * 0.95, Opacity * 1.001);

    [Fact]
    public void Alpha_never_decreases_inwards()
    {
        for (var d = Blur; d > -Blur; d -= 1)
        {
            Assert.True(AlphaAt(d - 1) >= AlphaAt(d) - 1e-9, $"{d}");
        }
    }

    [Fact]
    public void Every_layer_alpha_is_valid() =>
        Assert.All(Layers, l => Assert.InRange(l.Alpha, 0, 1));
}
