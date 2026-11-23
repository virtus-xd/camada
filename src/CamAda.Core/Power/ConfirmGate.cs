namespace CamAda.Core.Power;

/// <summary>
/// Kapat ve Yeniden başlat için tek adımlık onay: ilk basış silahlandırır ("Emin misin?"), süre içindeki ikinci basış
/// işlemi onaylar. Süre dolunca kapı kendiliğinden silahsız sayılır.
/// </summary>
public sealed class ConfirmGate
{
    private readonly TimeProvider _time;
    private readonly TimeSpan _window;
    private DateTimeOffset? _armedAt;

    public ConfirmGate(TimeProvider time, TimeSpan window)
    {
        _time = time;
        _window = window;
    }

    public bool IsArmed => _armedAt is { } armedAt && _time.GetUtcNow() - armedAt <= _window;

    /// <summary>İlk basışta false (silahlanır); süre içinde ikinci basışta true döner ve sıfırlanır.</summary>
    public bool Press()
    {
        if (IsArmed)
        {
            _armedAt = null;
            return true;
        }

        _armedAt = _time.GetUtcNow();
        return false;
    }
}
