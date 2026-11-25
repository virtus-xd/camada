using CamAda.Core.Assistant;
using Microsoft.Extensions.Time.Testing;

namespace CamAda.Core.Tests.Assistant;

public class SessionRulesTests
{
    private readonly FakeTimeProvider _time = new();

    private SessionRules Make()
    {
        var rules = new SessionRules(_time, TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(6), TimeSpan.FromMinutes(10));
        rules.Started();
        return rules;
    }

    private void Wait(double seconds) => _time.Advance(TimeSpan.FromSeconds(seconds));

    [Fact]
    public void No_speech_ends_quietly_at_6s()
    {
        var rules = Make();
        Wait(5.9);
        Assert.Null(rules.Check());
        Wait(0.2);
        Assert.Equal(SessionEnd.NoSpeech, rules.Check());
    }

    [Fact]
    public void Silence_counts_from_ada_finishing()
    {
        var rules = Make();
        rules.UserSpoke();
        rules.AdaSpeaking();
        Wait(20);
        Assert.Null(rules.Check());
        rules.AdaFinished();
        Wait(7.9);
        Assert.Null(rules.Check());
        Wait(0.2);
        Assert.Equal(SessionEnd.Silence, rules.Check());
    }

    [Fact]
    public void User_speaking_resets_silence()
    {
        var rules = Make();
        rules.UserSpoke();
        rules.AdaFinished();
        Wait(7);
        rules.UserSpoke();
        Wait(7);
        Assert.Null(rules.Check());
    }

    [Fact]
    public void Tool_running_holds_session()
    {
        var rules = Make();
        rules.UserSpoke();
        rules.ToolRunning(true);
        Wait(30);
        Assert.Null(rules.Check());
        rules.ToolRunning(false);
        rules.AdaFinished();
        Wait(8.1);
        Assert.Equal(SessionEnd.Silence, rules.Check());
    }

    [Fact]
    public void Max_duration_wins_while_talking()
    {
        var rules = Make();
        rules.UserSpoke();
        rules.AdaSpeaking();
        Wait(600);
        Assert.Equal(SessionEnd.MaxDuration, rules.Check());
    }
}
