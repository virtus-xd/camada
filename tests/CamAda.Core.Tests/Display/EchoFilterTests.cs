using CamAda.Core.Display;
using Microsoft.Extensions.Time.Testing;

namespace CamAda.Core.Tests.Display;

public class EchoFilterTests
{
    private readonly FakeTimeProvider _time = new();

    private EchoFilter Create() => new(_time, TimeSpan.FromSeconds(1.5));

    [Fact]
    public void Matching_recent_send_is_echo_once()
    {
        var filter = Create();
        filter.Sent(40);
        _time.Advance(TimeSpan.FromSeconds(0.5));
        Assert.True(filter.IsEcho(40));
        Assert.False(filter.IsEcho(40));
    }

    [Fact]
    public void Different_value_is_not_echo()
    {
        var filter = Create();
        filter.Sent(40);
        Assert.False(filter.IsEcho(55));
    }

    [Fact]
    public void Old_send_expires()
    {
        var filter = Create();
        filter.Sent(40);
        _time.Advance(TimeSpan.FromSeconds(2));
        Assert.False(filter.IsEcho(40));
    }

    [Fact]
    public void Each_send_matches_once()
    {
        var filter = Create();
        filter.Sent(30);
        filter.Sent(40);
        Assert.True(filter.IsEcho(30));
        Assert.True(filter.IsEcho(40));
        Assert.False(filter.IsEcho(30));
    }
}
