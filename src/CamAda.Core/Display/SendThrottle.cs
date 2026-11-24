namespace CamAda.Core.Display;

/// <summary>
/// Kaydırıcı sürüklenirken ekrana gönderimi seyrekleştirir: aralıkta en çok bir gönderim; arada gelenlerin yalnız
/// sonuncusu bekletilir ve aralık dolunca <see cref="TakeDue"/> ile verilir, böylece son değer hep gider.
/// </summary>
public sealed class SendThrottle(TimeProvider time, TimeSpan interval)
{
    private DateTimeOffset? _lastSent;
    private int? _pending;

    public TimeSpan Interval => interval;

    public bool HasPending => _pending is not null;

    /// <summary>Hemen gönderilecekse değeri döner; değilse bekletir ve null döner.</summary>
    public int? Offer(int value)
    {
        if (IsDue())
        {
            _pending = null;
            _lastSent = time.GetUtcNow();
            return value;
        }

        _pending = value;
        return null;
    }

    /// <summary>Aralık dolduysa bekleyen son değeri döner.</summary>
    public int? TakeDue()
    {
        if (_pending is not { } value || !IsDue())
        {
            return null;
        }

        _pending = null;
        _lastSent = time.GetUtcNow();
        return value;
    }

    private bool IsDue() => _lastSent is not { } last || time.GetUtcNow() - last >= interval;
}
