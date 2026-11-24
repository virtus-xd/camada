using CamAda.Core.Display;
using Microsoft.Extensions.Time.Testing;

namespace CamAda.Core.Tests.Display;

public class EchoFilterTests
{
    private readonly FakeTimeProvider _time = new();

    private EchoFilter Create() => new(_time, TimeSpan.FromSeconds(1.5));

    // Windows her WmiSetBrightness için gecikmeli ikinci bir olay da gönderiyor, sırası karışık (24.11 ölçümü:
    // 10, 40, 90 gönderilince olaylar 10, 40, 90, 10, 90). Süre içindeki kopyaların hepsi yankıdır.
    [Fact]
    public void Matching_recent_send_stays_echo_within_window()
    {
        var filter = Create();
        filter.Sent(40);
        _time.Advance(TimeSpan.FromSeconds(0.5));
        Assert.True(filter.IsEcho(40));
        Assert.True(filter.IsEcho(40));
    }

    [Fact]
    public void Late_duplicate_of_an_earlier_send_is_echo()
    {
        var filter = Create();
        filter.Sent(10);
        filter.Sent(40);
        filter.Sent(90);
        Assert.True(filter.IsEcho(90));
        _time.Advance(TimeSpan.FromMilliseconds(300));
        Assert.True(filter.IsEcho(10));
        Assert.True(filter.IsEcho(90));
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
    public void Window_counts_from_the_latest_send_of_a_value()
    {
        var filter = Create();
        filter.Sent(30);
        _time.Advance(TimeSpan.FromSeconds(1));
        filter.Sent(30);
        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(filter.IsEcho(30));
    }
}
