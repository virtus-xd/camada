namespace CamAda.Core.Assistant;

/// <summary>Kapatma isteğinin hangi adımda olduğu.</summary>
public enum ConfirmStep
{
    /// <summary>İlk istek: kullanıcıya sorulmalı.</summary>
    Ask,

    /// <summary>Soruldu ama kullanıcı henüz konuşmadı (model kendi kendine yeniden çağırdı): yapılmaz.</summary>
    WaitForUser,

    /// <summary>Sorudan sonra kullanıcı konuştu ve süre içinde yeniden istendi: yapılır.</summary>
    Confirmed,
}

/// <summary>
/// Ada'nın iki adımlı kapatma koruması (tasarım: "modelin davranışından bağımsız"): ikinci çağrı yalnız ilk çağrıdan sonra
/// kullanıcı konuştuysa ve süre dolmadıysa geçer. Böylece model aynı iletide iki kez ya da "onay gerekiyor"u alır almaz
/// yeniden çağırsa da bilgisayar kapanmaz. Her oturumun başında sıfırlanır.
/// </summary>
public sealed class UserConfirmGate(TimeProvider time, TimeSpan window)
{
    private DateTimeOffset? _armedAt;
    private bool _userSpoke;

    /// <summary>Soru soruldu ve süresi dolmadı (misafir kipinde yanıt "Ada" demeden geçsin diye).</summary>
    public bool Armed => _armedAt is { } at && time.GetUtcNow() - at <= window;

    public ConfirmStep Press()
    {
        var now = time.GetUtcNow();
        if (_armedAt is not { } armedAt || now - armedAt > window)
        {
            _armedAt = now;
            _userSpoke = false;
            return ConfirmStep.Ask;
        }

        if (!_userSpoke)
        {
            return ConfirmStep.WaitForUser;
        }

        Reset();
        return ConfirmStep.Confirmed;
    }

    /// <summary>Kullanıcının sözü (canlı yazı) geldi; yalnız soru sorulduktan sonrakiler sayılır.</summary>
    public void UserSpoke()
    {
        if (_armedAt is not null)
        {
            _userSpoke = true;
        }
    }

    public void Reset()
    {
        _armedAt = null;
        _userSpoke = false;
    }
}
