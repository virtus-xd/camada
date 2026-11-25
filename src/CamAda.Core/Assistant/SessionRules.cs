namespace CamAda.Core.Assistant;

public enum SessionEnd
{
    /// <summary>Uyandıktan sonra hiç konuşulmadı (yanlış tetiklenme): sessizce kapanır.</summary>
    NoSpeech,

    /// <summary>Ada'nın yanıtından (ya da kullanıcının son sözünden) sonra sessizlik.</summary>
    Silence,

    /// <summary>Oturum en uzun süreyi aştı.</summary>
    MaxDuration,
}

/// <summary>
/// Oturumun ne zaman kapanacağı (tasarım "Uyanma ve oturum"): Ada konuşurken ya da bir işlev sürerken sessizlik sayılmaz;
/// en uzun süre her durumda geçerlidir.
/// </summary>
public sealed class SessionRules(TimeProvider time, TimeSpan silence, TimeSpan noSpeech, TimeSpan maxSession)
{
    private DateTimeOffset _startedAt;
    private DateTimeOffset? _lastUserSpokeAt;
    private DateTimeOffset? _adaFinishedAt;
    private DateTimeOffset? _toolFinishedAt;
    private bool _adaSpeaking;
    private int _toolsRunning;

    public void Started()
    {
        _startedAt = time.GetUtcNow();
        _lastUserSpokeAt = null;
        _adaFinishedAt = null;
        _toolFinishedAt = null;
        _adaSpeaking = false;
        _toolsRunning = 0;
    }

    public void UserSpoke() => _lastUserSpokeAt = time.GetUtcNow();

    public void AdaSpeaking() => _adaSpeaking = true;

    public void AdaFinished()
    {
        _adaSpeaking = false;
        _adaFinishedAt = time.GetUtcNow();
    }

    /// <summary>
    /// Bir işlev grubu başladı ya da bitti (gruplar üst üste gelebilir: sayılır). Bitiş etkinlik sayılır (yavaş işlevden
    /// sonra Ada'nın sonucu söylemesine zaman kalır).
    /// </summary>
    public void ToolRunning(bool running)
    {
        if (running)
        {
            _toolsRunning++;
        }
        else if (_toolsRunning > 0)
        {
            _toolsRunning--;
            _toolFinishedAt = time.GetUtcNow();
        }
    }

    public SessionEnd? Check()
    {
        var now = time.GetUtcNow();
        if (now - _startedAt >= maxSession)
        {
            return SessionEnd.MaxDuration;
        }

        if (_adaSpeaking || _toolsRunning > 0)
        {
            return null;
        }

        if (_lastUserSpokeAt is null && _adaFinishedAt is null && _toolFinishedAt is null)
        {
            return now - _startedAt >= noSpeech ? SessionEnd.NoSpeech : null;
        }

        var reference = Latest(Latest(_lastUserSpokeAt, _adaFinishedAt), _toolFinishedAt);
        return now - reference >= silence ? SessionEnd.Silence : null;
    }

    private static DateTimeOffset Latest(DateTimeOffset? a, DateTimeOffset? b) =>
        a is { } x && b is { } y ? (x > y ? x : y) : a ?? b ?? DateTimeOffset.MinValue;
}
