namespace CamAda.Core.Assistant;

public enum BargeInAction
{
    None,

    /// <summary>Ada'yı hemen sustur (kalan sesi tut).</summary>
    Hold,

    /// <summary>Yanlış alarm (öksürük, kapı): tutulan sesten devam et.</summary>
    Resume,
}

/// <summary>
/// Yerel söz kesme (yankı engelleyiciyle): Ada konuşurken mikrofon art arda yüksek gelirse Ada hemen susar ama kalan sesi
/// atılmaz. Google söz kesmeyi onaylarsa (<see cref="Interrupted"/>) ses atılır; onaylamazsa ortalık
/// <c>quietToResume</c> kadar sessizleşince (ya da en geç <c>maxHold</c>) Ada kaldığı yerden sürer. Uzun gürültüde yanıt
/// bölük pörçük olmasın diye o yanıtın kalanında yerel kesme kapanır (Google'ın kendi kesmesi çalışmaya devam eder).
/// </summary>
public sealed class BargeInGate(TimeProvider time, double threshold, int loudChunks, TimeSpan quietToResume, TimeSpan maxHold)
{
    private int _loud;
    private bool _suppressed;
    private DateTimeOffset _heldAt;
    private DateTimeOffset _lastLoudAt;

    public bool Holding { get; private set; }

    /// <param name="rms">Mikrofon parçasının (~100 ms) RMS'i.</param>
    /// <param name="adaTalking">Hoparlörde modelin sesi var (tutuluyor olsa da).</param>
    public BargeInAction OnMic(double rms, bool adaTalking)
    {
        if (!adaTalking)
        {
            // Yanıt bitti ya da ses başka yoldan atıldı: sonraki yanıt yeniden kesilebilir.
            var wasHolding = Holding;
            Holding = false;
            _suppressed = false;
            _loud = 0;
            return wasHolding ? BargeInAction.Resume : BargeInAction.None;
        }

        var now = time.GetUtcNow();
        var loud = rms > threshold;
        if (Holding)
        {
            if (loud)
            {
                _lastLoudAt = now;
            }

            if (now - _lastLoudAt < quietToResume && now - _heldAt < maxHold)
            {
                return BargeInAction.None;
            }

            Holding = false;
            _suppressed = true;
            _loud = 0;
            return BargeInAction.Resume;
        }

        if (_suppressed || !loud)
        {
            _loud = 0;
            return BargeInAction.None;
        }

        if (++_loud < loudChunks)
        {
            return BargeInAction.None;
        }

        Holding = true;
        _loud = 0;
        _heldAt = _lastLoudAt = now;
        return BargeInAction.Hold;
    }

    /// <summary>Google söz kesmeyi bildirdi (kalan ses atıldı).</summary>
    public void Interrupted()
    {
        Holding = false;
        _suppressed = false;
        _loud = 0;
    }
}
