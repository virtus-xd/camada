namespace CamAda.Core.Assistant;

/// <summary>
/// Ada'nın not kipi (tasarım "Ada ile not alma"): not açılırken, sürerken ve bittikten sonraki 3 sn yalnız not işlevleri çalışır;
/// kullanıcı konuşmaya başlayınca Ada'nın sesi atılır; 4 sn (ilk parçadan önce 8 sn) kullanıcı sesi ya da yazma olmazsa ve
/// yazılmakta olan parça yoksa not biter. Her parça notun alt satırına eklenir; bitişten sonra gelen geç parça da aynı nota
/// eklenir.
/// </summary>
public sealed class NoteSession(TimeProvider time, TimeSpan quietToFinish)
{
    public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(4);

    /// <summary>
    /// İlk parçaya kadar daha uzun beklenir: "Dinliyorum"dan sonra kullanıcının başlaması zaman alır ve Google'ın canlı yazısı
    /// söz bitince gelir (26.11 sahte oda denemesi: 4 sn'de not kullanıcı konuşurken bitti).
    /// </summary>
    public static readonly TimeSpan FirstPartWait = TimeSpan.FromSeconds(8);

    /// <summary>Not bittikten sonra aynı sözden geç gelen bir komut da yürütülmez.</summary>
    public static readonly TimeSpan AfterFinishGrace = TimeSpan.FromSeconds(3);

    /// <summary>Not açıldıktan hemen sonra gelen kullanıcı yazısı komutun kendisidir: "Dinliyorum" kısılmasın.</summary>
    private static readonly TimeSpan MuteGrace = TimeSpan.FromSeconds(1);

    private readonly List<string> _lines = [];
    private bool _started;
    private bool _opening;
    private int _writes;
    private DateTimeOffset _startedAt;
    private DateTimeOffset _lastActivity;
    private DateTimeOffset _finishedAt = DateTimeOffset.MinValue;

    public bool Active { get; private set; }

    public bool Muted { get; private set; }

    public string Text => string.Join("\n", _lines);

    /// <summary>Oturum kuşağı: her <see cref="Reset"/>'te artar; eski oturumdan geç dönen açılış not kipini başlatamaz.</summary>
    public int Generation { get; private set; }

    /// <summary>Not Defteri açılıyor (en çok ~5 sn): bu sırada gelen komutlar da yürütülmez.</summary>
    public void BeginOpening() => _opening = true;

    public void CancelOpening() => _opening = false;

    /// <summary>Yeni not; <paramref name="generation"/> açılış başlarkenki kuşak değilse (oturum değişti) başlamaz.</summary>
    public bool TryStart(int generation)
    {
        _opening = false;
        if (generation != Generation)
        {
            return false;
        }

        _lines.Clear();
        _started = true;
        Active = true;
        Muted = false;
        _writes = 0;
        _startedAt = _lastActivity = time.GetUtcNow();
        return true;
    }

    public void Start() => TryStart(Generation);

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

    /// <summary>Bir parça yazılmaya başladı (karakter karakter, saniyeler sürebilir): bu sırada not bitmez.</summary>
    public void WriteStarted() => _writes++;

    /// <summary>Parça yazıldı (ya da yazılamadı): sessizlik bu andan sayılır.</summary>
    public void WriteFinished()
    {
        if (_writes > 0)
        {
            _writes--;
        }

        _lastActivity = time.GetUtcNow();
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
        Active && _writes == 0 && time.GetUtcNow() - _lastActivity >= (_lines.Count == 0 ? FirstPartWait : quietToFinish);

    public bool Finish()
    {
        var wasActive = Active;
        Active = false;
        Muted = false;
        if (wasActive)
        {
            _finishedAt = time.GetUtcNow();
        }

        return wasActive;
    }

    /// <summary>Oturum bitti: sonraki oturumda not açılmadıkça yazma yok; açılmakta olan not geçersiz.</summary>
    public void Reset()
    {
        Finish();
        Generation++;
        _started = false;
        _opening = false;
        _writes = 0;
        _finishedAt = DateTimeOffset.MinValue;
        _lines.Clear();
    }

    /// <summary>İşlev şu an reddedilsin mi: not açılırken, sürerken ve bittikten sonraki 3 sn yalnız not_yaz/not_bitir çalışır.</summary>
    public bool Refuses(string toolName) =>
        !Allows(toolName) && (Active || _opening || time.GetUtcNow() - _finishedAt < AfterFinishGrace);

    public static bool Allows(string toolName) => toolName is AdaTools.NotYaz or AdaTools.NotBitir;

    /// <summary>Aynı çağrı grubunda bir not işlevi varsa diğer işlevler yürütülmez (notta geçen komut).</summary>
    public static bool RefusedInBatch(IReadOnlyList<string> names, string toolName) =>
        !IsNoteTool(toolName) && names.Any(IsNoteTool);

    private static bool IsNoteTool(string name) => name is AdaTools.NotAl or AdaTools.NotYaz or AdaTools.NotBitir;

    /// <summary>Ada'nın not dosyasının adı ("Not 26.11.2026 01.07.txt"); Not Defteri başlığında bu ad aranır.</summary>
    public static string FileNameFor(DateTime now) =>
        "Not " + now.ToString("dd.MM.yyyy HH.mm", System.Globalization.CultureInfo.InvariantCulture) + ".txt";
}
