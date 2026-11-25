namespace CamAda.Core.Clipboard;

/// <summary>Panoya gelen görselle ne yapılacağı.</summary>
public enum ClipImageDecision
{
    /// <summary>Sıradan görsel: geçmişe ekle.</summary>
    AddToHistory,

    /// <summary>Az önce klasöre düşen ekran görüntüsünün panodaki kopyası: şeritte zaten var, ekleme.</summary>
    Drop,

    /// <summary>Ekran alıntısı aracından geldi: dosyası gelecek mi diye kısa süre beklet.</summary>
    Hold,
}

/// <summary>Yeni ekran görüntüsü dosyasının sahiplendiği pano görseli: bekleyen mi, geçmişe girmiş mi.</summary>
public sealed record ScreenshotClaim(string Id, bool InHistory);

/// <summary>
/// Ekran görüntüsünün tek kayıt olması (tasarım "Ekran görüntüleri"): ekran alıntısı aracı aynı görüntüyü hem panoya koyar
/// hem klasöre kaydeder; sıra belli değildir. 3 sn içinde aynı boyuttaki pano görseli ile dosya aynı ekran görüntüsü
/// sayılır: dosya önce geldiyse pano görseli atılır, sonra geldiyse görsel bekletmeden ya da geçmişten çıkarılır. Dosyası
/// hiç gelmeyen araç görseli 3 sn sonra "yalnız panoya giden ekran görüntüsü" olur.
/// </summary>
public sealed class ScreenshotTracker(TimeProvider time)
{
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(3);

    private readonly List<Seen> _files = new();
    private readonly List<Seen> _pending = new();
    private readonly List<Seen> _added = new();

    public bool HasPending => _pending.Count > 0;

    /// <summary>Yeni ekran görüntüsü sayılan dosyanın en büyük yaşı (yazımı süren dosya için pay dahil).</summary>
    public static readonly TimeSpan NewFileAge = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Klasörde beliren dosya yeni çekilmiş mi: klasöre taşınan ya da yeniden adlandırılan eski dosya oluşturulma zamanını
    /// korur ve "yeni ekran görüntüsü" sayılmaz.
    /// </summary>
    public static bool IsNewFile(DateTime createdUtc, DateTimeOffset now) => now.UtcDateTime - createdUtc <= NewFileAge;

    public ClipImageDecision OnClipImage(string pendingId, int width, int height, bool fromScreenshotTool)
    {
        Prune();
        var file = _files.FindIndex(f => f.Width == width && f.Height == height);
        if (file >= 0)
        {
            _files.RemoveAt(file);
            return ClipImageDecision.Drop;
        }

        if (fromScreenshotTool)
        {
            _pending.Add(new Seen(pendingId, width, height, time.GetUtcNow()));
            return ClipImageDecision.Hold;
        }

        return ClipImageDecision.AddToHistory;
    }

    /// <summary>Geçmişe eklenen görseli hatırlar (dosyası az sonra gelirse geçmişten çıkarılsın diye).</summary>
    public void AddedToHistory(string itemId, int width, int height) =>
        _added.Add(new Seen(itemId, width, height, time.GetUtcNow()));

    /// <summary>Yeni ekran görüntüsü dosyası: aynı ekran görüntüsünün pano görselini sahiplenir (yoksa null).</summary>
    public ScreenshotClaim? OnFile(int width, int height)
    {
        Prune();
        var pending = _pending.FindIndex(p => p.Width == width && p.Height == height);
        if (pending >= 0)
        {
            var id = _pending[pending].Id;
            _pending.RemoveAt(pending);
            return new ScreenshotClaim(id, InHistory: false);
        }

        var added = _added.FindIndex(a => a.Width == width && a.Height == height);
        if (added >= 0)
        {
            var id = _added[added].Id;
            _added.RemoveAt(added);
            return new ScreenshotClaim(id, InHistory: true);
        }

        _files.Add(new Seen("", width, height, time.GetUtcNow()));
        return null;
    }

    /// <summary>Dosyası 3 sn içinde gelmeyen araç görselleri (yalnız panoya giden ekran görüntüleri).</summary>
    public IReadOnlyList<string> TakeExpired()
    {
        var now = time.GetUtcNow();
        var expired = _pending.Where(p => now - p.At > Window).Select(p => p.Id).ToList();
        _pending.RemoveAll(p => now - p.At > Window);
        return expired;
    }

    private void Prune()
    {
        var now = time.GetUtcNow();
        _files.RemoveAll(f => now - f.At > Window);
        _added.RemoveAll(a => now - a.At > Window);
    }

    private sealed record Seen(string Id, int Width, int Height, DateTimeOffset At);
}
