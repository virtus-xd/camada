using CamAda.Core.Assistant;
using Microsoft.Extensions.Time.Testing;

namespace CamAda.Core.Tests.Assistant;

public class UserConfirmGateTests
{
    private readonly FakeTimeProvider _time = new();

    private UserConfirmGate Make() => new(_time, TimeSpan.FromSeconds(30));

    [Fact]
    public void First_call_asks()
    {
        Assert.Equal(ConfirmStep.Ask, Make().Press());
    }

    [Fact]
    public void Second_call_without_user_turn_waits()
    {
        // Model "onay gerekiyor"u alıp hemen ya da aynı iletide ikinci kez çağırırsa kapanmaz.
        var gate = Make();
        gate.Press();
        Assert.Equal(ConfirmStep.WaitForUser, gate.Press());
    }

    [Fact]
    public void User_turn_then_call_confirms_once()
    {
        var gate = Make();
        gate.Press();
        gate.UserSpoke();
        Assert.Equal(ConfirmStep.Confirmed, gate.Press());
        Assert.Equal(ConfirmStep.Ask, gate.Press());
    }

    [Fact]
    public void User_speech_before_arming_does_not_count()
    {
        var gate = Make();
        gate.UserSpoke();
        gate.Press();
        Assert.Equal(ConfirmStep.WaitForUser, gate.Press());
    }

    [Fact]
    public void Expired_window_asks_again()
    {
        var gate = Make();
        gate.Press();
        gate.UserSpoke();
        _time.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(ConfirmStep.Ask, gate.Press());
    }

    [Fact]
    public void Armed_after_ask_until_window_or_reset()
    {
        var gate = Make();
        Assert.False(gate.Armed);

        gate.Press();
        Assert.True(gate.Armed);
        _time.Advance(TimeSpan.FromSeconds(31));
        Assert.False(gate.Armed);

        gate.Press();
        gate.Reset();
        Assert.False(gate.Armed);
    }

    [Fact]
    public void Reset_forgets_previous_session()
    {
        var gate = Make();
        gate.Press();
        gate.UserSpoke();
        gate.Reset();
        Assert.Equal(ConfirmStep.Ask, gate.Press());
    }
}
