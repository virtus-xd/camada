namespace CamAda.Core.Record;

/// <summary>
/// Plağın dönüşü (spec §7.8): çalarken 33⅓ dev/dk; çalmaya başlayınca hızlanarak, duraklayınca yavaşlayarak; hiçbir şey
/// çalmıyorken durgun. "Animasyonları azalt" açıksa hiç dönmez. Ada mini plağı ve masaüstü plağı aynı fiziği kullanır.
/// </summary>
public sealed class RecordSpin
{
    /// <summary>33⅓ dev/dk = tur başına 1,8 sn.</summary>
    public const double PlayingDegreesPerSecond = 200;

    public static readonly TimeSpan SpinUp = TimeSpan.FromSeconds(0.8);
    public static readonly TimeSpan SpinDown = TimeSpan.FromSeconds(1.6);

    /// <summary>Uyku ya da kare takılması sonrası tek adımın etkisi bununla sınırlanır (plak sıçramasın).</summary>
    public static readonly TimeSpan MaxStep = TimeSpan.FromMilliseconds(100);

    /// <summary>[0, 360) derece.</summary>
    public double Angle { get; private set; }

    /// <summary>°/sn, hiçbir zaman negatif değil.</summary>
    public double Velocity { get; private set; }

    public bool IsMoving => Velocity > 0;

    public void Advance(TimeSpan elapsed, bool playing, bool reduceMotion)
    {
        if (reduceMotion)
        {
            Velocity = 0;
            return;
        }

        var dt = Math.Clamp(elapsed.TotalSeconds, 0, MaxStep.TotalSeconds);
        if (dt == 0)
        {
            return;
        }

        var start = Velocity;
        var target = playing ? PlayingDegreesPerSecond : 0;
        var rate = PlayingDegreesPerSecond / (playing ? SpinUp : SpinDown).TotalSeconds;
        var end = start < target ? Math.Min(target, start + rate * dt) : Math.Max(target, start - rate * dt);

        // Adım içindeki ortalama hız (doğrusal ivme) açıyı ilerletir.
        Angle = ((Angle + (start + end) / 2 * dt) % 360 + 360) % 360;
        Velocity = end;
    }
}
