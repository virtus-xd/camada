namespace CamAda.Core.Assistant;

/// <summary>
/// Adadaki satırın metni: kullanıcının o anki sözü ve Ada'nın o anki yanıtı (Google'ın canlı yazısı). Yeni tur başlayınca
/// eskisi silinir; Ada'nın yeni yanıtı ilk ses parçasında başlar (yazısı sesten sonra gelir).
/// </summary>
public sealed class SessionTranscript
{
    private bool _userDone;
    private bool _adaDone;

    public string User { get; private set; } = "";

    public string Ada { get; private set; } = "";

    public void UserText(string text)
    {
        if (_userDone)
        {
            User = "";
            _userDone = false;
        }

        User += text;
    }

    public void AdaAudio()
    {
        if (_adaDone)
        {
            Ada = "";
            _adaDone = false;
        }
    }

    public void AdaText(string text)
    {
        AdaAudio();
        Ada += text;
    }

    public void TurnComplete()
    {
        _adaDone = true;
        _userDone = true;
    }

    /// <summary>Ada'nın yanıtı kesildi; kullanıcı konuşuyor, onun satırı sürer.</summary>
    public void Interrupted() => _adaDone = true;
}
