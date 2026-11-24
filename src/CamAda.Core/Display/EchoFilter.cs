namespace CamAda.Core.Display;

/// <summary>
/// CamAda'nın kendi gönderdiği değerin Windows'tan olay olarak geri gelmesini ayırt eder (ör. parlaklık): süre içinde
/// gönderilmiş aynı değer yankıdır. Windows bir gönderim için birden çok, sırası karışık olay verebildiğinden eşleşme
/// tüketilmez; süre o değerin en son gönderiminden sayılır.
/// </summary>
public sealed class EchoFilter(TimeProvider time, TimeSpan window)
{
    private readonly Dictionary<int, DateTimeOffset> _lastSent = new();

    public void Sent(int value)
    {
        Prune();
        _lastSent[value] = time.GetUtcNow();
    }

    public bool IsEcho(int value)
    {
        Prune();
        return _lastSent.ContainsKey(value);
    }

    private void Prune()
    {
        var now = time.GetUtcNow();
        foreach (var expired in _lastSent.Where(s => now - s.Value > window).Select(s => s.Key).ToList())
        {
            _lastSent.Remove(expired);
        }
    }
}
