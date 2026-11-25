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
    private bool _adaSpeaking;
    private bool _toolRunning;

    public void Started()
    {
        _startedAt = time.GetUtcNow();
        _lastUserSpokeAt = null;
        _adaFinishedAt = null;
        _adaSpeaking = false;
        _toolRunning = false;
    }

    public void UserSpoke() => _lastUserSpokeAt = time.GetUtcNow();

    public void AdaSpeaking() => _adaSpeaking = true;

    public void AdaFinished()
    {
        _adaSpeaking = false;
        _adaFinishedAt = time.GetUtcNow();
    }

    public void ToolRunning(bool running) => _toolRunning = running;

    public SessionEnd? Check()
    {
        var now = time.GetUtcNow();
        if (now - _startedAt >= maxSession)
        {
            return SessionEnd.MaxDuration;
        }

        if (_adaSpeaking || _toolRunning)
        {
            return null;
        }

        if (_lastUserSpokeAt is null && _adaFinishedAt is null)
        {
            return now - _startedAt >= noSpeech ? SessionEnd.NoSpeech : null;
        }

        var reference = Latest(_lastUserSpokeAt, _adaFinishedAt);
        return now - reference >= silence ? SessionEnd.Silence : null;
    }

    private static DateTimeOffset Latest(DateTimeOffset? a, DateTimeOffset? b) =>
        a is { } x && b is { } y ? (x > y ? x : y) : a ?? b ?? DateTimeOffset.MinValue;
}
