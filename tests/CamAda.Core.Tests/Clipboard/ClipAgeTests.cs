using CamAda.Core.Clipboard;

namespace CamAda.Core.Tests.Clipboard;

public class ClipAgeTests
{
    private static readonly TimeSpan Turkey = TimeSpan.FromHours(3);
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 15, 0, 0, Turkey);

    [Fact]
    public void Under_a_minute_is_now() => Assert.Equal("şimdi", ClipAge.Format(Now.AddSeconds(-30), Now));

    [Fact]
    public void Minutes()
    {
        Assert.Equal("2 dk", ClipAge.Format(Now.AddMinutes(-2), Now));
        Assert.Equal("59 dk", ClipAge.Format(Now.AddMinutes(-59), Now));
    }

    [Fact]
    public void Hours_on_the_same_day()
    {
        Assert.Equal("3 sa", ClipAge.Format(Now.AddHours(-3), Now));
        Assert.Equal("14 sa", ClipAge.Format(new DateTimeOffset(2026, 11, 24, 1, 0, 0, Turkey), Now));
    }

    [Fact]
    public void Previous_day_is_yesterday() =>
        Assert.Equal("dün", ClipAge.Format(new DateTimeOffset(2026, 11, 23, 23, 0, 0, Turkey), Now));

    [Fact]
    public void Older_shows_day_and_short_month() =>
        Assert.Equal("12 Kas", ClipAge.Format(new DateTimeOffset(2026, 11, 12, 10, 0, 0, Turkey), Now));
}
