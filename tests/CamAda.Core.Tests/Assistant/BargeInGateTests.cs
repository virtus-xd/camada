using CamAda.Core.Assistant;
using Microsoft.Extensions.Time.Testing;

namespace CamAda.Core.Tests.Assistant;

public class BargeInGateTests
{
    private const double Loud = 0.1;
    private const double Quiet = 0.005;
    private readonly FakeTimeProvider _time = new();

    private BargeInGate Make() =>
        new(_time, threshold: 0.04, loudChunks: 2, quietToResume: TimeSpan.FromMilliseconds(700), maxHold: TimeSpan.FromSeconds(3));

    /// <summary>Bir mikrofon parçası (100 ms) verir.</summary>
    private BargeInAction Chunk(BargeInGate gate, double rms, bool adaTalking = true)
    {
        _time.Advance(TimeSpan.FromMilliseconds(100));
        return gate.OnMic(rms, adaTalking);
    }

    [Fact]
    public void Cough_pauses_then_resumes_the_answer()
    {
        var gate = Make();
        Assert.Equal(BargeInAction.None, Chunk(gate, Loud));
        Assert.Equal(BargeInAction.Hold, Chunk(gate, Loud)); // iki yüksek parça: Ada hemen susar
        for (var i = 0; i < 6; i++)
        {
            Assert.Equal(BargeInAction.None, Chunk(gate, Quiet));
        }

        Assert.Equal(BargeInAction.Resume, Chunk(gate, Quiet)); // Google "kesildi" demedi: kalan yanıt sürer
    }

    [Fact]
    public void Speaking_user_keeps_ada_paused_until_quiet()
    {
        var gate = Make();
        Chunk(gate, Loud);
        Chunk(gate, Loud);
        for (var i = 0; i < 15; i++)
        {
            Assert.Equal(BargeInAction.None, i % 4 == 3 ? Chunk(gate, Quiet) : Chunk(gate, Loud));
        }

        Assert.True(gate.Holding);
    }

    [Fact]
    public void Long_noise_resumes_after_max_hold_and_does_not_pause_again()
    {
        var gate = Make();
        Chunk(gate, Loud);
        Chunk(gate, Loud);
        var actions = Enumerable.Range(0, 40).Select(_ => Chunk(gate, Loud)).ToList();

        Assert.Equal(BargeInAction.Resume, actions[29]); // 3 sn
        Assert.All(actions.Skip(30), a => Assert.Equal(BargeInAction.None, a)); // bu yanıtta yerel kesme kapalı
    }

    [Fact]
    public void Interrupt_resets_and_next_answer_can_pause_again()
    {
        var gate = Make();
        Chunk(gate, Loud);
        Chunk(gate, Loud);
        gate.Interrupted(); // Google söz kesmeyi onayladı; kalan ses atıldı

        Assert.False(gate.Holding);
        Chunk(gate, Quiet, adaTalking: false);
        Chunk(gate, Loud);
        Assert.Equal(BargeInAction.Hold, Chunk(gate, Loud));
    }

    [Fact]
    public void Suppression_ends_when_ada_stops_talking()
    {
        var gate = Make();
        Chunk(gate, Loud);
        Chunk(gate, Loud);
        for (var i = 0; i < 7; i++)
        {
            Chunk(gate, Quiet);
        }

        Chunk(gate, Quiet, adaTalking: false); // yanıt bitti
        Chunk(gate, Loud);
        Assert.Equal(BargeInAction.Hold, Chunk(gate, Loud));
    }

    [Fact]
    public void Silent_ada_never_pauses()
    {
        var gate = Make();
        Assert.All(Enumerable.Range(0, 5).Select(_ => Chunk(gate, Loud, adaTalking: false)), a => Assert.Equal(BargeInAction.None, a));
    }
}
