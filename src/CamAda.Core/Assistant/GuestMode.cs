namespace CamAda.Core.Assistant;

/// <summary>
/// Misafir kipi (tasarım "Misafir kipi"): açıkken Ada yalnız "Ada" diye başlayan sözleri dinler. Oturumda başka bir ses
/// <c>confirmWithin</c> içinde iki kez duyulunca kendiliğinden açılır (tek bir yanlış ret kullanıcıyı 2 dk "Ada" demeye
/// zorlamasın; son inceleme) ve son başka sesten <c>autoDuration</c> sonra biter; oturumlar arasında taşınır. Kullanıcı sesle
/// açtıysa kapatana kadar sürer. Kapatma kendiliğinden açılmış kipi de kapatır; yeni başka sesler yeniden açabilir.
/// </summary>
public sealed class GuestMode(TimeProvider time, TimeSpan autoDuration, TimeSpan confirmWithin)
{
    private DateTimeOffset? _lastOtherAt;
    private DateTimeOffset? _pendingAt;

    public bool IsManual { get; private set; }

    public bool IsOn => IsManual || (_lastOtherAt is { } at && time.GetUtcNow() - at < autoDuration);

    /// <summary>
    /// Başka bir ses duyuldu. Kip açıksa süre yenilenir; kapalıysa ilk ses beklemeye alınır, ikincisi kipi açar. Kip kapalıyken
    /// açtıysa true (ton bir kez çalsın diye).
    /// </summary>
    public bool OtherVoiceHeard()
    {
        var now = time.GetUtcNow();
        if (IsOn)
        {
            _lastOtherAt = now;
            return false;
        }

        if (_pendingAt is { } pending && now - pending <= confirmWithin)
        {
            _pendingAt = null;
            _lastOtherAt = now;
            return true;
        }

        _pendingAt = now;
        return false;
    }

    /// <summary>Kullanıcı sesle açtı ya da kapattı; <see cref="IsOn"/> değiştiyse true.</summary>
    public bool SetManual(bool on)
    {
        var wasOn = IsOn;
        IsManual = on;
        if (!on)
        {
            _lastOtherAt = null;
            _pendingAt = null;
        }

        return IsOn != wasOn;
    }
}
