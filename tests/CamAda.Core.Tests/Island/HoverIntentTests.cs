using CamAda.Core.Island;
using Microsoft.Extensions.Time.Testing;

namespace CamAda.Core.Tests.Island;

public class HoverIntentTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly List<bool> _events = new();

    private HoverIntent Make()
    {
        var h = new HoverIntent(_time, TimeSpan.FromMilliseconds(120), TimeSpan.FromMilliseconds(350));
        h.ExpandedChanged += (_, v) => _events.Add(v);
        return h;
    }

    private void Ms(int ms) => _time.Advance(TimeSpan.FromMilliseconds(ms));

    [Fact]
    public void Expands_exactly_after_enter_delay()
    {
        var h = Make(); h.PointerEntered();
        Ms(119); Assert.False(h.IsExpanded);
        Ms(1); Assert.True(h.IsExpanded);
        Assert.Equal(new[] { true }, _events);
    }

    [Fact]
    public void Leaving_before_enter_delay_cancels()
    {
        var h = Make(); h.PointerEntered(); Ms(80); h.PointerExited(); Ms(1000);
        Assert.False(h.IsExpanded); Assert.Empty(_events);
    }

    [Fact]
    public void Collapses_exactly_after_leave_delay()
    {
        var h = Make(); h.PointerEntered(); Ms(120); h.PointerExited();
        Ms(349); Assert.True(h.IsExpanded);
        Ms(1); Assert.False(h.IsExpanded);
        Assert.Equal(new[] { true, false }, _events);
    }

    [Fact]
    public void Reentering_within_leave_delay_keeps_expanded_without_events()
    {
        var h = Make(); h.PointerEntered(); Ms(120); h.PointerExited(); Ms(200); h.PointerEntered(); Ms(1000);
        Assert.True(h.IsExpanded); Assert.Equal(new[] { true }, _events);
    }

    [Fact]
    public void Open_panel_holds_expanded_and_closing_it_outside_collapses_immediately()
    {
        var h = Make(); h.PointerEntered(); Ms(120); h.SetPanelOpen(true); h.PointerExited(); Ms(5000);
        Assert.True(h.IsExpanded);
        h.SetPanelOpen(false);
        Assert.False(h.IsExpanded);
    }

    [Fact]
    public void ExpandNow_expands_immediately_and_closing_panel_outside_collapses()
    {
        var h = Make();
        h.ExpandNow();
        Assert.True(h.IsExpanded);
        Assert.Equal(new[] { true }, _events);

        h.SetPanelOpen(true); Ms(5000);
        Assert.True(h.IsExpanded);
        h.SetPanelOpen(false);
        Assert.False(h.IsExpanded);
        Assert.Equal(new[] { true, false }, _events);
    }

    [Fact]
    public void ExpandNow_when_already_expanded_raises_nothing()
    {
        var h = Make(); h.PointerEntered(); Ms(120);
        h.ExpandNow();
        Assert.Equal(new[] { true }, _events);
    }

    [Fact]
    public void Suppressed_pointer_does_not_expand_until_released()
    {
        var h = Make();
        h.Suppress(true);
        h.PointerEntered(); Ms(1000);
        Assert.False(h.IsExpanded);

        h.Suppress(false);
        Ms(119); Assert.False(h.IsExpanded);
        Ms(1); Assert.True(h.IsExpanded);
    }

    [Fact]
    public void Suppress_release_with_pointer_outside_does_nothing()
    {
        var h = Make();
        h.Suppress(true);
        h.PointerEntered(); h.PointerExited();
        h.Suppress(false); Ms(5000);
        Assert.False(h.IsExpanded);
        Assert.Empty(_events);
    }

    [Fact]
    public void Closing_panel_while_pointer_inside_stays_expanded()
    {
        var h = Make(); h.PointerEntered(); Ms(120); h.SetPanelOpen(true); h.SetPanelOpen(false); Ms(5000);
        Assert.True(h.IsExpanded);
    }
}
