using CamAda.Core.Record;

namespace CamAda.Core.Tests.Record;

public class RecordSpinTests
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(10);

    private static void Run(RecordSpin spin, double seconds, bool playing, bool reduceMotion = false, Action? afterEach = null)
    {
        for (var i = 0; i < (int)Math.Round(seconds / Step.TotalSeconds); i++)
        {
            spin.Advance(Step, playing, reduceMotion);
            afterEach?.Invoke();
        }
    }

    [Fact]
    public void Spins_up_halfway_in_half_the_spin_up_time()
    {
        var spin = new RecordSpin();
        Run(spin, 0.4, playing: true);
        Assert.Equal(100, spin.Velocity, 1);
    }

    [Fact]
    public void Reaches_33_rpm_after_spin_up()
    {
        var spin = new RecordSpin();
        Run(spin, 0.8, playing: true);
        Assert.Equal(RecordSpin.PlayingDegreesPerSecond, spin.Velocity, 3);
    }

    [Fact]
    public void Full_speed_turn_takes_1_8_seconds()
    {
        var spin = new RecordSpin();
        Run(spin, 0.8, playing: true);
        var start = spin.Angle;
        Run(spin, 1.8, playing: true);
        var drift = Math.Abs(spin.Angle - start);
        Assert.True(Math.Min(drift, 360 - drift) < 1, $"başlangıç {start}, son {spin.Angle}");
    }

    [Fact]
    public void Pausing_slows_down_to_a_stop_without_reversing()
    {
        var spin = new RecordSpin();
        Run(spin, 1.0, playing: true);
        Run(spin, 1.6, playing: false, afterEach: () => Assert.True(spin.Velocity >= 0));
        Assert.Equal(0, spin.Velocity);
        Assert.False(spin.IsMoving);
    }

    [Fact]
    public void Angle_stays_within_one_turn()
    {
        var spin = new RecordSpin();
        Run(spin, 5, playing: true, afterEach: () => Assert.InRange(spin.Angle, 0, 359.999999));
    }

    [Fact]
    public void Reduce_motion_stops_spinning_at_once()
    {
        var spin = new RecordSpin();
        Run(spin, 1.0, playing: true);
        var angle = spin.Angle;
        spin.Advance(Step, playing: true, reduceMotion: true);
        Assert.Equal(0, spin.Velocity);
        Assert.Equal(angle, spin.Angle);
    }

    // Uyku ya da takılma sonrası tek büyük adım plağı sıçratmasın.
    [Fact]
    public void One_long_step_moves_at_most_100_ms_worth()
    {
        var spin = new RecordSpin();
        Run(spin, 1.0, playing: true);
        var angle = spin.Angle;
        spin.Advance(TimeSpan.FromSeconds(10), playing: true, reduceMotion: false);
        var moved = (spin.Angle - angle + 360) % 360;
        Assert.True(moved <= 20.0001, $"{moved}°");
    }

    // Son inceleme: plak çizilmiyorken (örtülü, ada genişlemiş) hız Advance ile değişmiyordu; duraklatınca tam hızda
    // kalıp örtülme yoklamasını sonsuza dek açık tutuyor, görünür olunca hayalet bir yavaşlama gösteriyordu.
    [Fact]
    public void Settle_while_not_drawn_jumps_to_the_target_speed()
    {
        var spin = new RecordSpin();
        spin.Advance(TimeSpan.FromSeconds(0.1), playing: true, reduceMotion: false);
        spin.Advance(TimeSpan.FromSeconds(0.1), playing: true, reduceMotion: false);
        var angle = spin.Angle;

        spin.Settle(playing: false);
        Assert.Equal(0, spin.Velocity);
        Assert.False(spin.IsMoving);
        Assert.Equal(angle, spin.Angle);

        spin.Settle(playing: true);
        Assert.Equal(RecordSpin.PlayingDegreesPerSecond, spin.Velocity);
    }
}
