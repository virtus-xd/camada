using CamAda.Core.Power;
using Microsoft.Extensions.Time.Testing;

namespace CamAda.Core.Tests.Power;

public class ConfirmGateTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void First_press_arms_without_confirming()
    {
        var gate = new ConfirmGate(_time, TimeSpan.FromSeconds(3));
        Assert.False(gate.Press());
        Assert.True(gate.IsArmed);
    }

    [Fact]
    public void Second_press_within_the_window_confirms_and_resets()
    {
        var gate = new ConfirmGate(_time, TimeSpan.FromSeconds(3));
        gate.Press();
        _time.Advance(TimeSpan.FromSeconds(2));
        Assert.True(gate.Press());
        Assert.False(gate.IsArmed);
    }

    [Fact]
    public void Second_press_after_the_window_arms_again()
    {
        var gate = new ConfirmGate(_time, TimeSpan.FromSeconds(3));
        gate.Press();
        _time.Advance(TimeSpan.FromSeconds(3.1));
        Assert.False(gate.IsArmed);
        Assert.False(gate.Press());
        Assert.True(gate.IsArmed);
    }
}
