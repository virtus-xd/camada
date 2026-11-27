using CamAda.Core.Calendar;
using static CamAda.Core.Tests.Text.Cultures;

namespace CamAda.Core.Tests.Calendar;

public class MonthGridTests
{
    private static readonly DateOnly Today = new(2026, 11, 24);

    [Fact]
    public void November_2026_grid_starts_on_monday_october_26()
    {
        var days = MonthGrid.Build(2026, 11, Today);

        Assert.Equal(42, days.Count);
        Assert.Equal(new DateOnly(2026, 10, 26), days[0].Date);
        Assert.False(days[0].InMonth);
        Assert.Equal(new DateOnly(2026, 11, 1), days[6].Date);
        Assert.True(days[6].InMonth);
        Assert.Equal(new DateOnly(2026, 12, 6), days[41].Date);
        Assert.False(days[41].InMonth);

        var today = Assert.Single(days, d => d.IsToday);
        Assert.Same(days[29], today);
        Assert.Equal(Today, today.Date);
    }

    [Fact]
    public void Month_starting_on_monday_has_no_leading_days()
    {
        var first = MonthGrid.Build(2026, 6, Today)[0];

        Assert.Equal(new DateOnly(2026, 6, 1), first.Date);
        Assert.True(first.InMonth);
    }

    [Fact]
    public void Today_outside_the_grid_is_not_marked() =>
        Assert.DoesNotContain(MonthGrid.Build(2027, 1, Today), d => d.IsToday);

    [Fact]
    public void Shift_wraps_years()
    {
        Assert.Equal((2027, 1), MonthGrid.Shift(2026, 12, 1));
        Assert.Equal((2026, 12), MonthGrid.Shift(2027, 1, -1));
        Assert.Equal((2025, 10), MonthGrid.Shift(2026, 11, -13));
    }

    [Fact]
    public void Titles_and_headers_are_turkish()
    {
        Assert.Equal("Kasım 2026", MonthGrid.Title(2026, 11, Tr));
        Assert.Equal(["Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz"], MonthGrid.WeekdayHeaders(Tr));
    }
}
