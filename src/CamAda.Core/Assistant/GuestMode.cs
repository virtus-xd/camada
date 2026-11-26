namespace CamAda.Core.Assistant;

/// <summary>
/// Misafir kipi (tasarım "Misafir kipi"): açıkken Ada yalnız "Ada" diye başlayan sözleri dinler. Oturumda başka bir ses
/// duyulunca kendiliğinden açılır ve son başka sesten <c>autoDuration</c> sonra biter; oturumlar arasında taşınır. Kullanıcı
/// sesle açtıysa kapatana kadar sürer. Kapatma kendiliğinden açılmış kipi de kapatır; yeni bir başka ses yeniden açabilir.
/// </summary>
public sealed class GuestMode(TimeProvider time, TimeSpan autoDuration)
{
    private DateTimeOffset? _lastOtherAt;

    public bool IsManual { get; private set; }

    public bool IsOn => IsManual || (_lastOtherAt is { } at && time.GetUtcNow() - at < autoDuration);

    /// <summary>Başka bir ses duyuldu: süre yenilenir. Kip kapalıyken açtıysa true (ton bir kez çalsın diye).</summary>
    public bool OtherVoiceHeard()
    {
        var wasOn = IsOn;
        _lastOtherAt = time.GetUtcNow();
        return !wasOn;
    }

    /// <summary>Kullanıcı sesle açtı ya da kapattı; <see cref="IsOn"/> değiştiyse true.</summary>
    public bool SetManual(bool on)
    {
        var wasOn = IsOn;
        IsManual = on;
        if (!on)
        {
            _lastOtherAt = null;
        }

        return IsOn != wasOn;
    }
}
