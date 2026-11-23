using CamAda.Core.Glass;
using CamAda.Core.Island;

namespace CamAda.Core.Tests.Island;

public class SmartHideTests
{
    // Yoklama noktası: ekranın üst ortası (1920 px genişlik, %125 ölçekte üstten 25 px).
    private const double ProbeX = 960;
    private const double ProbeY = 25;

    private static WindowSample Window(double x, double y, double w, double h,
        bool visible = true, bool cloaked = false, bool ignorable = false,
        bool hasTitle = true, bool isDesktop = false, bool isOwn = false) =>
        new(new RectD(x, y, w, h), visible, cloaked, ignorable, hasTitle, isDesktop, isOwn);

    private static readonly WindowSample Desktop = Window(0, 0, 1920, 1080, isDesktop: true);

    [Fact]
    public void Only_desktop_is_not_occluded() =>
        Assert.False(OcclusionRule.IsOccluded([Desktop], ProbeX, ProbeY));

    [Fact]
    public void Maximized_window_occludes() =>
        Assert.True(OcclusionRule.IsOccluded([Window(-8, -8, 1936, 1036), Desktop], ProbeX, ProbeY));

    [Fact]
    public void Small_window_away_from_probe_does_not_occlude() =>
        Assert.False(OcclusionRule.IsOccluded([Window(100, 300, 800, 500), Desktop], ProbeX, ProbeY));

    [Fact]
    public void Tool_window_over_probe_is_ignored() =>
        Assert.False(OcclusionRule.IsOccluded([Window(900, 0, 200, 60, ignorable: true), Desktop], ProbeX, ProbeY));

    [Fact]
    public void Cloaked_window_is_ignored() =>
        Assert.False(OcclusionRule.IsOccluded([Window(0, 0, 1920, 1080, cloaked: true), Desktop], ProbeX, ProbeY));

    [Fact]
    public void Untitled_window_is_ignored() =>
        Assert.False(OcclusionRule.IsOccluded([Window(0, 0, 1920, 1080, hasTitle: false), Desktop], ProbeX, ProbeY));

    [Fact]
    public void Own_windows_are_ignored() =>
        Assert.False(OcclusionRule.IsOccluded([Window(397, 0, 1125, 200, isOwn: true), Desktop], ProbeX, ProbeY));

    [Fact]
    public void Invisible_window_is_ignored() =>
        Assert.False(OcclusionRule.IsOccluded([Window(0, 0, 1920, 1080, visible: false), Desktop], ProbeX, ProbeY));

    [Fact]
    public void No_windows_means_not_occluded() =>
        Assert.False(OcclusionRule.IsOccluded([], ProbeX, ProbeY));

    [Theory]
    [InlineData(true, true, true, IslandPresence.Hidden)]
    [InlineData(true, false, false, IslandPresence.Hidden)]
    [InlineData(false, true, false, IslandPresence.Tucked)]
    [InlineData(false, true, true, IslandPresence.Shown)]
    [InlineData(false, false, false, IslandPresence.Shown)]
    public void Presence_rule(bool fullScreen, bool occluded, bool expanded, IslandPresence expected) =>
        Assert.Equal(expected, IslandPresenceRule.Decide(fullScreen, occluded, expanded));
}
