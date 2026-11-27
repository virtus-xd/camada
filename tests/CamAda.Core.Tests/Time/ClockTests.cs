using CamAda.Core.Time;
using static CamAda.Core.Tests.Text.Cultures;

namespace CamAda.Core.Tests.Time;

public class ClockTests
{
    [Fact]
    public void Time_is_24h_with_leading_zero() =>
        Assert.Equal("09:05", ClockFormatter.Time(new DateTime(2026, 11, 23, 9, 5, 0), Tr, Tr));

    [Fact]
    public void Date_is_turkish_short_day_and_month() =>
        Assert.Equal("Pzt, 23 Kasım", ClockFormatter.Date(new DateTime(2026, 11, 23, 14, 32, 0), Tr));

    [Fact]
    public void Delay_to_next_minute_mid_minute() =>
        Assert.Equal(TimeSpan.FromSeconds(39.5),
            MinuteTicker.DelayToNextMinute(new DateTime(2026, 11, 23, 14, 32, 20, 500)));

    [Fact]
    public void Delay_on_exact_minute_is_full_minute() =>
        Assert.Equal(TimeSpan.FromMinutes(1),
            MinuteTicker.DelayToNextMinute(new DateTime(2026, 11, 23, 14, 33, 0)));
}
