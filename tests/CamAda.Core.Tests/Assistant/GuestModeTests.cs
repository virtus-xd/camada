using CamAda.Core.Assistant;
using Microsoft.Extensions.Time.Testing;

namespace CamAda.Core.Tests.Assistant;

public class GuestModeTests
{
    private readonly FakeTimeProvider _time = new();

    private GuestMode Make() => new(_time, TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(10));

    private void Wait(double seconds) => _time.Advance(TimeSpan.FromSeconds(seconds));

    /// <summary>İki başka ses kararı (10 sn içinde): kip açık.</summary>
    private static void Confirm(GuestMode guest)
    {
        guest.OtherVoiceHeard();
        guest.OtherVoiceHeard();
    }

    [Fact]
    public void Starts_off() => Assert.False(Make().IsOn);

    [Fact]
    public void Single_other_voice_does_not_turn_on()
    {
        var guest = Make();

        Assert.False(guest.OtherVoiceHeard());
        Assert.False(guest.IsOn);
    }

    [Fact]
    public void Second_other_voice_within_ten_seconds_turns_on_once()
    {
        var guest = Make();
        guest.OtherVoiceHeard();
        Wait(9);

        Assert.True(guest.OtherVoiceHeard());
        Assert.False(guest.OtherVoiceHeard());
        Assert.True(guest.IsOn);
        Assert.False(guest.IsManual);
    }

    [Fact]
    public void Isolated_other_voices_do_not_turn_on()
    {
        var guest = Make();
        guest.OtherVoiceHeard();
        Wait(11);

        Assert.False(guest.OtherVoiceHeard());
        Assert.False(guest.IsOn);
        Wait(5);
        Assert.True(guest.OtherVoiceHeard());
    }

    [Fact]
    public void Auto_ends_two_minutes_after_last_other()
    {
        var guest = Make();
        Confirm(guest);
        Wait(60);
        guest.OtherVoiceHeard();

        Wait(119.9);
        Assert.True(guest.IsOn);
        Wait(0.2);
        Assert.False(guest.IsOn);
    }

    [Fact]
    public void Turns_on_again_after_expiry()
    {
        var guest = Make();
        Confirm(guest);
        Wait(121);

        Assert.False(guest.OtherVoiceHeard());
        Assert.True(guest.OtherVoiceHeard());
    }

    [Fact]
    public void Manual_on_has_no_expiry()
    {
        var guest = Make();

        Assert.True(guest.SetManual(true));
        Wait(3600);
        Assert.True(guest.IsOn);
        Assert.True(guest.IsManual);
    }

    [Fact]
    public void Manual_off_clears_auto_and_auto_can_return()
    {
        var guest = Make();
        Confirm(guest);

        Assert.True(guest.SetManual(false));
        Assert.False(guest.IsOn);
        Assert.False(guest.OtherVoiceHeard());
        Assert.True(guest.OtherVoiceHeard());
        Assert.True(guest.IsOn);
    }

    [Fact]
    public void Manual_on_while_auto_on_keeps_it_on()
    {
        var guest = Make();
        Confirm(guest);

        Assert.False(guest.SetManual(true));
        Wait(600);
        Assert.True(guest.IsOn);
    }

    [Fact]
    public void Other_voice_while_manual_does_not_report_turning_on()
    {
        var guest = Make();
        guest.SetManual(true);

        Assert.False(guest.OtherVoiceHeard());
        Assert.False(guest.OtherVoiceHeard());
    }

    [Fact]
    public void Same_manual_state_reports_no_change() => Assert.False(Make().SetManual(false));
}
