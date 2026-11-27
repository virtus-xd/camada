using CamAda.Core.Power;
using static CamAda.Core.Tests.Text.Cultures;

namespace CamAda.Core.Tests.Power;

public class BatteryTextTests
{
    private static readonly TimeSpan ThreeTen = new(3, 10, 0);

    private static BatterySnapshot Snap(int percent, BatteryCharge charge, bool plugged = false, TimeSpan? remaining = null) =>
        new(percent, charge, plugged, remaining);

    [Fact]
    public void Header()
    {
        Assert.Equal("%76 · 3 sa 10 dk", BatteryText.Header(Snap(76, BatteryCharge.Discharging, remaining: ThreeTen), Tr));
        Assert.Equal("%76", BatteryText.Header(Snap(76, BatteryCharge.Discharging), Tr));
        Assert.Equal("Şarj oluyor · %76", BatteryText.Header(Snap(76, BatteryCharge.Charging, plugged: true), Tr));
        Assert.Equal("Tam dolu", BatteryText.Header(Snap(100, BatteryCharge.Idle, plugged: true), Tr));
        Assert.Equal("Prize takılı · %80", BatteryText.Header(Snap(80, BatteryCharge.Idle, plugged: true), Tr));
        Assert.Null(BatteryText.Header(Snap(0, BatteryCharge.NotPresent), Tr));
    }

    [Fact]
    public void Remaining_of_a_day_or_more_is_unknown() =>
        Assert.Equal("%76", BatteryText.Header(Snap(76, BatteryCharge.Discharging, remaining: TimeSpan.FromHours(30)), Tr));

    [Theory]
    [InlineData(3, 10, 0, "3 sa 10 dk")]
    [InlineData(3, 0, 0, "3 sa")]
    [InlineData(0, 45, 0, "45 dk")]
    [InlineData(0, 59, 59, "59 dk")]
    [InlineData(0, 0, 30, "1 dk")]
    public void FormatRemaining(int hours, int minutes, int seconds, string expected) =>
        Assert.Equal(expected, BatteryText.FormatRemaining(new TimeSpan(hours, minutes, seconds), Tr));

    [Fact]
    public void Accessible_name()
    {
        Assert.Equal("Pil %76, 3 sa 10 dk kaldı", BatteryText.AccessibleName(Snap(76, BatteryCharge.Discharging, remaining: ThreeTen), Tr));
        Assert.Equal("Pil %76, şarj oluyor", BatteryText.AccessibleName(Snap(76, BatteryCharge.Charging, plugged: true), Tr));
        Assert.Equal("Pil %100, tam dolu", BatteryText.AccessibleName(Snap(100, BatteryCharge.Idle, plugged: true), Tr));
        Assert.Equal("Pil %80, prize takılı", BatteryText.AccessibleName(Snap(80, BatteryCharge.Idle, plugged: true), Tr));
        Assert.Equal("Pil %76", BatteryText.AccessibleName(Snap(76, BatteryCharge.Discharging), Tr));
    }

    [Fact]
    public void Low_only_when_discharging_at_or_below_20()
    {
        Assert.True(BatteryText.IsLow(Snap(20, BatteryCharge.Discharging)));
        Assert.False(BatteryText.IsLow(Snap(21, BatteryCharge.Discharging)));
        Assert.False(BatteryText.IsLow(Snap(15, BatteryCharge.Charging, plugged: true)));
        Assert.False(BatteryText.IsLow(Snap(15, BatteryCharge.Idle, plugged: true)));
    }

    [Fact]
    public void Icon()
    {
        Assert.Equal(new BatteryIcon(8, false, false), BatteryText.Icon(Snap(76, BatteryCharge.Discharging)));
        Assert.Equal(new BatteryIcon(0, false, true), BatteryText.Icon(Snap(4, BatteryCharge.Discharging)));
        Assert.Equal(new BatteryIcon(2, false, true), BatteryText.Icon(Snap(15, BatteryCharge.Discharging)));
        Assert.Equal(new BatteryIcon(10, true, false, PluggedIn: true), BatteryText.Icon(Snap(100, BatteryCharge.Charging, plugged: true)));
    }

    [Fact]
    public void Icon_marks_plugged_in_battery_that_is_not_charging()
    {
        // Prize takılı ama şarj etmiyor (dolu ya da şarj sınırında): simge prizde olduğunu göstermeli.
        Assert.Equal(new BatteryIcon(8, false, false, PluggedIn: true), BatteryText.Icon(Snap(80, BatteryCharge.Idle, plugged: true)));
        Assert.Equal(new BatteryIcon(10, false, false, PluggedIn: true), BatteryText.Icon(Snap(100, BatteryCharge.Idle, plugged: true)));
        Assert.False(BatteryText.Icon(Snap(80, BatteryCharge.Discharging)).PluggedIn);
    }

    [Fact]
    public void CompactText() => Assert.Equal("%18", BatteryText.CompactText(Snap(18, BatteryCharge.Discharging), Tr));
}
