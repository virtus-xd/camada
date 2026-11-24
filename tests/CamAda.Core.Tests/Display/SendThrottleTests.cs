using CamAda.Core.Display;
using Microsoft.Extensions.Time.Testing;

namespace CamAda.Core.Tests.Display;

public class SendThrottleTests
{
    private readonly FakeTimeProvider _time = new();

    private SendThrottle Create() => new(_time, TimeSpan.FromMilliseconds(100));

    [Fact]
    public void First_value_passes() => Assert.Equal(10, Create().Offer(10));

    [Fact]
    public void Held_values_latest_wins()
    {
        var throttle = Create();
        throttle.Offer(10);
        _time.Advance(TimeSpan.FromMilliseconds(30));
        Assert.Null(throttle.Offer(20));
        Assert.Null(throttle.Offer(30));
        Assert.True(throttle.HasPending);
        _time.Advance(TimeSpan.FromMilliseconds(30));
        Assert.Null(throttle.TakeDue());
        _time.Advance(TimeSpan.FromMilliseconds(40));
        Assert.Equal(30, throttle.TakeDue());
        Assert.False(throttle.HasPending);
    }

    [Fact]
    public void After_interval_offer_passes()
    {
        var throttle = Create();
        throttle.Offer(10);
        _time.Advance(TimeSpan.FromMilliseconds(150));
        Assert.Equal(20, throttle.Offer(20));
        Assert.False(throttle.HasPending);
    }

    [Fact]
    public void TakeDue_without_pending_is_null()
    {
        var throttle = Create();
        throttle.Offer(10);
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(throttle.TakeDue());
    }
}
