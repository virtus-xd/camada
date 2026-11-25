namespace CamAda.Core.Clipboard;

/// <summary>
/// Pano geçmişi (tasarım "Geçmiş"): sabitlenmemiş en fazla 30 öğe en yenisi üstte; sabitlenenler ayrı listede, sınırsız.
/// Aynı içerik yeniden gelirse yeni satır açılmaz, mevcut satır en üste çıkar. Sabitlenenlerin her değişikliği
/// <see cref="PinnedVersion"/>'ı artırır (çağıran diske yazmak için izler).
/// </summary>
public sealed class ClipboardHistory
{
    public const int MaxRecent = 30;
    public const long MaxItemBytes = 16L * 1024 * 1024;

    private readonly List<ClipItem> _pinned;
    private readonly List<ClipItem> _recent = new();

    public ClipboardHistory(IEnumerable<ClipItem> pinned)
    {
        _pinned = pinned.Select(p => p.Pinned ? p : p with { Pinned = true }).ToList();
    }

    /// <summary>Sabitlenenler, en son sabitlenen üstte.</summary>
    public IReadOnlyList<ClipItem> Pinned => _pinned;

    /// <summary>Sabitlenmemişler, en yeni üstte.</summary>
    public IReadOnlyList<ClipItem> Recent => _recent;

    public int PinnedVersion { get; private set; }

    /// <summary>Yeni kopyayı ekler; boş metin ya da 16 MB'tan büyük öğe için null.</summary>
    public ClipItem? Add(ClipPayload payload, DateTimeOffset now)
    {
        if (payload.Size > MaxItemBytes || (!payload.IsImage && string.IsNullOrWhiteSpace(payload.Text)))
        {
            return null;
        }

        var key = payload.Key;
        var pinnedIndex = _pinned.FindIndex(i => i.Payload.Key == key);
        if (pinnedIndex >= 0)
        {
            var updated = _pinned[pinnedIndex] with { Payload = payload, CopiedAt = now };
            _pinned[pinnedIndex] = updated;
            PinnedVersion++;
            return updated;
        }

        var existing = _recent.FindIndex(i => i.Payload.Key == key);
        var item = existing >= 0
            ? _recent[existing] with { Payload = payload, CopiedAt = now }
            : new ClipItem(Guid.NewGuid().ToString("N"), KindOf(payload), payload, now, Pinned: false);
        if (existing >= 0)
        {
            _recent.RemoveAt(existing);
        }

        _recent.Insert(0, item);
        if (_recent.Count > MaxRecent)
        {
            _recent.RemoveRange(MaxRecent, _recent.Count - MaxRecent);
        }

        return item;
    }

    /// <summary>Aynı içerik (sabitli ya da değil) geçmişte var mı.</summary>
    public bool Contains(ClipPayload payload)
    {
        var key = payload.Key;
        return _pinned.Exists(i => i.Payload.Key == key) || _recent.Exists(i => i.Payload.Key == key);
    }

    public ClipItem? Find(string id) => _pinned.Find(i => i.Id == id) ?? _recent.Find(i => i.Id == id);

    /// <summary>CamAda'nın kendi yeniden kopyalaması: sabitlenmemiş öğeyi en üste alır.</summary>
    public void Touch(string id, DateTimeOffset now)
    {
        var index = _recent.FindIndex(i => i.Id == id);
        if (index < 0)
        {
            return;
        }

        var item = _recent[index] with { CopiedAt = now };
        _recent.RemoveAt(index);
        _recent.Insert(0, item);
    }

    public void Pin(string id)
    {
        var index = _recent.FindIndex(i => i.Id == id);
        if (index < 0)
        {
            return;
        }

        var item = _recent[index] with { Pinned = true };
        _recent.RemoveAt(index);
        _pinned.Insert(0, item);
        PinnedVersion++;
    }

    public void Unpin(string id)
    {
        var index = _pinned.FindIndex(i => i.Id == id);
        if (index < 0)
        {
            return;
        }

        var item = _pinned[index] with { Pinned = false };
        _pinned.RemoveAt(index);
        _recent.Insert(0, item);
        if (_recent.Count > MaxRecent)
        {
            _recent.RemoveRange(MaxRecent, _recent.Count - MaxRecent);
        }

        PinnedVersion++;
    }

    public void Remove(string id)
    {
        if (_pinned.RemoveAll(i => i.Id == id) > 0)
        {
            PinnedVersion++;
        }

        _recent.RemoveAll(i => i.Id == id);
    }

    public void ClearUnpinned() => _recent.Clear();

    private static ClipKind KindOf(ClipPayload payload) =>
        payload.IsImage ? ClipKind.Image : ClipClassifier.Classify(payload.Text!);
}
