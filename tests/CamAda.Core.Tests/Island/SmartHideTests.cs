using CamAda.Core.Glass;
using CamAda.Core.Island;

namespace CamAda.Core.Tests.Island;

public class SmartHideTests
{
    // Adanın ekranda kapladığı alan (küçük hap + gölge payı), %125 ölçekte, 1920 px genişlikte ekranın üst ortası:
    // hap 400×40 DIP üstten 8 DIP; gölge yanlarda 30, altta 38 DIP → DIP (690, 0, 460, 86) → px (862.5, 0, 575, 107.5).
    private static readonly RectD Footprint = new(862.5, 0, 575, 107.5);

    private static WindowSample Window(double x, double y, double w, double h,
        bool visible = true, bool cloaked = false, bool ignorable = false,
        bool hasTitle = true, bool isDesktop = false, bool isOwn = false) =>
        new(new RectD(x, y, w, h), visible, cloaked, ignorable, hasTitle, isDesktop, isOwn);

    private static readonly WindowSample Desktop = Window(0, 0, 1920, 1080, isDesktop: true);

    [Fact]
    public void Only_desktop_is_not_occluded() =>
        Assert.False(OcclusionRule.IsOccluded([Desktop], Footprint));

    [Fact]
    public void Maximized_window_occludes() =>
        Assert.True(OcclusionRule.IsOccluded([Window(-8, -8, 1936, 1036), Desktop], Footprint));

    [Fact]
    public void Small_window_away_from_island_does_not_occlude() =>
        Assert.False(OcclusionRule.IsOccluded([Window(100, 300, 800, 500), Desktop], Footprint));

    [Fact]
    public void Title_bar_just_below_the_pill_center_line_occludes() =>
        // Üst kenarı 37 px (≈30 DIP) aşağıda; hapın ve gölgesinin altında kalıyor.
        Assert.True(OcclusionRule.IsOccluded([Window(600, 37, 900, 600), Desktop], Footprint));

    [Fact]
    public void Window_ending_left_of_center_but_under_the_pill_occludes() =>
        Assert.True(OcclusionRule.IsOccluded([Window(200, 0, 750, 500), Desktop], Footprint));

    [Fact]
    public void Window_just_below_the_shadow_does_not_occlude() =>
        Assert.False(OcclusionRule.IsOccluded([Window(600, 110, 900, 600), Desktop], Footprint));

    [Fact]
    public void Ignorable_window_over_island_is_ignored_even_above_a_real_one() =>
        Assert.False(OcclusionRule.IsOccluded([Window(900, 0, 200, 60, ignorable: true), Desktop], Footprint));

    [Fact]
    public void Cloaked_window_is_ignored() =>
        Assert.False(OcclusionRule.IsOccluded([Window(0, 0, 1920, 1080, cloaked: true), Desktop], Footprint));

    [Fact]
    public void Untitled_window_is_ignored() =>
        Assert.False(OcclusionRule.IsOccluded([Window(0, 0, 1920, 1080, hasTitle: false), Desktop], Footprint));

    [Fact]
    public void Own_windows_are_ignored() =>
        Assert.False(OcclusionRule.IsOccluded([Window(397, 0, 1125, 200, isOwn: true), Desktop], Footprint));

    [Fact]
    public void Invisible_window_is_ignored() =>
        Assert.False(OcclusionRule.IsOccluded([Window(0, 0, 1920, 1080, visible: false), Desktop], Footprint));

    [Fact]
    public void No_windows_means_not_occluded() =>
        Assert.False(OcclusionRule.IsOccluded([], Footprint));

    [Theory]
    [InlineData(true, true, true, IslandPresence.Hidden)]
    [InlineData(true, false, false, IslandPresence.Hidden)]
    [InlineData(false, true, false, IslandPresence.Tucked)]
    [InlineData(false, true, true, IslandPresence.Shown)]
    [InlineData(false, false, false, IslandPresence.Shown)]
    public void Presence_rule(bool fullScreen, bool occluded, bool expanded, IslandPresence expected) =>
        Assert.Equal(expected, IslandPresenceRule.Decide(fullScreen, occluded, expanded));

    // Üst kenar tetikleyicisi: ekran üstü 0, ada penceresi x 397..1522.
    [Theory]
    [InlineData(960, 0, true)]
    [InlineData(960, 1, true)]
    [InlineData(960, 2, false)]
    [InlineData(300, 0, false)]
    [InlineData(960, -1, false)]    // ana ekranın üstündeki bir monitörde
    [InlineData(960, -500, false)]
    public void Top_edge_trigger(int x, int y, bool expected) =>
        Assert.Equal(expected, RevealZone.IsAtTopEdge(x, y, windowLeft: 397, windowRight: 1522, screenTop: 0));
}
