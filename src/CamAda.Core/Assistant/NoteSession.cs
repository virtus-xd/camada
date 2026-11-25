namespace CamAda.Core.Assistant;

/// <summary>
/// Ada'nın not kipi (tasarım "Ada ile not alma"): not sürerken yalnız not işlevleri çalışır, kullanıcı konuşmaya başlayınca
/// Ada'nın sesi atılır, 4 sn (ilk parçadan önce 8 sn) kullanıcı sesi ya da yazma olmazsa not biter. Her parça notun alt
/// satırına eklenir; bitişten sonra gelen geç parça da aynı nota eklenir.
/// </summary>
public sealed class NoteSession(TimeProvider time, TimeSpan quietToFinish)
{
    public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(4);

    /// <summary>
    /// İlk parçaya kadar daha uzun beklenir: "Dinliyorum"dan sonra kullanıcının başlaması zaman alır ve Google'ın canlı yazısı
    /// söz bitince gelir (26.11 sahte oda denemesi: 4 sn'de not kullanıcı konuşurken bitti).
    /// </summary>
    public static readonly TimeSpan FirstPartWait = TimeSpan.FromSeconds(8);

    /// <summary>Not açıldıktan hemen sonra gelen kullanıcı yazısı komutun kendisidir: "Dinliyorum" kısılmasın.</summary>
    private static readonly TimeSpan MuteGrace = TimeSpan.FromSeconds(1);

    private readonly List<string> _lines = [];
    private bool _started;
    private DateTimeOffset _startedAt;
    private DateTimeOffset _lastActivity;

    public bool Active { get; private set; }

    public bool Muted { get; private set; }

    public string Text => string.Join("\n", _lines);

    public void Start()
    {
        _lines.Clear();
        _started = true;
        Active = true;
        Muted = false;
        _startedAt = _lastActivity = time.GetUtcNow();
    }

    /// <summary>
    /// Yazılacak metin: ilk parça olduğu gibi, sonrakiler alt satırda; art arda aynı parça bir kez. Bu oturumda not açılmadıysa
    /// null.
    /// </summary>
    public string? Append(string text)
    {
        if (!_started)
        {
            return null;
        }

        var line = text.Trim();
        if (line.Length == 0 || (_lines.Count > 0 && _lines[^1] == line))
        {
            return ""; // boş ya da modelin yeniden gönderdiği aynı parça (26.11 sahte oda denemesi)
        }

        _lastActivity = time.GetUtcNow();
        _lines.Add(line);
        return _lines.Count == 1 ? line : "\n" + line;
    }

    public void UserSpoke()
    {
        if (!Active)
        {
            return;
        }

        var now = time.GetUtcNow();
        _lastActivity = now;
        Muted |= now - _startedAt >= MuteGrace;
    }

    public bool ShouldFinish() =>
        Active && time.GetUtcNow() - _lastActivity >= (_lines.Count == 0 ? FirstPartWait : quietToFinish);

    public bool Finish()
    {
        var wasActive = Active;
        Active = false;
        Muted = false;
        return wasActive;
    }

    /// <summary>Oturum bitti: sonraki oturumda not açılmadıkça yazma yok.</summary>
    public void Reset()
    {
        Finish();
        _started = false;
        _lines.Clear();
    }

    public static bool Allows(string toolName) => toolName is AdaTools.NotYaz or AdaTools.NotBitir;

    /// <summary>Ada'nın not dosyasının adı ("Not 26.11.2026 01.07.txt"); Not Defteri başlığında bu ad aranır.</summary>
    public static string FileNameFor(DateTime now) =>
        "Not " + now.ToString("dd.MM.yyyy HH.mm", System.Globalization.CultureInfo.InvariantCulture) + ".txt";
}
