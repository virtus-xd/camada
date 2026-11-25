using System.Globalization;
using System.Text.Json;

namespace CamAda.Core.Assistant;

public abstract record MemoryChange
{
    private MemoryChange()
    {
    }

    /// <summary>Yeni madde eklendi; hafıza doluysa en eski güncellenen madde çıkarıldı.</summary>
    public sealed record Saved(MemoryItem? Evicted) : MemoryChange;

    public sealed record Updated : MemoryChange;

    public sealed record Rejected(string Reason) : MemoryChange;
}

/// <summary>
/// Ada'nın hafızası (tasarım "Hafıza"): en çok 50 madde, madde başı en çok 200 karakter, konu adı en çok 60 karakter. Aynı konu (tr-TR, büyük/küçük
/// harf yok sayılır) güncellenir; hassas bilgi reddedilir. Saklama şifreli dosyada (uygulamadaki <c>MemoryFile</c>).
/// </summary>
public sealed class MemoryBook
{
    public const int MaxItems = 50;
    public const int MaxFactLength = 200;
    public const int MaxTopicLength = 60;

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private readonly List<MemoryItem> _items = new();

    public IReadOnlyList<MemoryItem> Items => _items;

    public MemoryChange Remember(string topic, string fact, DateTimeOffset now)
    {
        topic = topic.Trim();
        fact = fact.Trim();
        if (topic.Length == 0 || fact.Length == 0)
        {
            return new MemoryChange.Rejected("eksik bilgi");
        }

        if (fact.Length > MaxFactLength)
        {
            return new MemoryChange.Rejected("çok uzun");
        }

        if (topic.Length > MaxTopicLength)
        {
            return new MemoryChange.Rejected("konu adı çok uzun");
        }

        if (SensitiveText.Looks(topic + " " + fact))
        {
            return new MemoryChange.Rejected("hassas bilgi");
        }

        var index = IndexOf(topic);
        if (index >= 0)
        {
            _items[index] = _items[index] with { Fact = fact, UpdatedAt = now };
            return new MemoryChange.Updated();
        }

        MemoryItem? evicted = null;
        if (_items.Count >= MaxItems)
        {
            evicted = _items.MinBy(i => i.UpdatedAt);
            _items.Remove(evicted!);
        }

        _items.Add(new MemoryItem(topic, fact, now));
        return new MemoryChange.Saved(evicted);
    }

    public bool Forget(string topic)
    {
        var index = IndexOf(topic.Trim());
        if (index < 0)
        {
            return false;
        }

        _items.RemoveAt(index);
        return true;
    }

    public void Clear() => _items.Clear();

    public string ToJson() => JsonSerializer.Serialize(_items);

    /// <summary><see cref="ToJson"/> ile alınmış duruma döner (dosyaya yazılamayan değişiklik geri alınır).</summary>
    public void RestoreFrom(string json)
    {
        var saved = FromJson(json);
        _items.Clear();
        _items.AddRange(saved._items);
    }

    /// <summary>Kayıtlı hafızayı okur; bozuksa <see cref="FormatException"/>.</summary>
    public static MemoryBook FromJson(string json)
    {
        try
        {
            var book = new MemoryBook();
            var items = JsonSerializer.Deserialize<List<MemoryItem>>(json) ?? [];
            book._items.AddRange(items.Where(i => i is not null && !string.IsNullOrWhiteSpace(i.Topic)).Take(MaxItems));
            return book;
        }
        catch (JsonException ex)
        {
            throw new FormatException("Hafıza dosyası okunamadı.", ex);
        }
    }

    private int IndexOf(string topic)
    {
        var key = topic.ToLower(Turkish);
        return _items.FindIndex(i => i.Topic.ToLower(Turkish) == key);
    }
}
