namespace CamAda.Core.Display;

/// <summary>
/// CamAda'nın kendi gönderdiği değerin Windows'tan olay olarak geri gelmesini ayırt eder (ör. parlaklık): süre içinde
/// gönderilmiş aynı değer yankıdır; her gönderim bir kez eşleşir.
/// </summary>
public sealed class EchoFilter(TimeProvider time, TimeSpan window)
{
    private readonly List<(int Value, DateTimeOffset At)> _sent = [];

    public void Sent(int value)
    {
        Prune();
        _sent.Add((value, time.GetUtcNow()));
    }

    public bool IsEcho(int value)
    {
        Prune();
        var index = _sent.FindIndex(s => s.Value == value);
        if (index < 0)
        {
            return false;
        }

        _sent.RemoveAt(index);
        return true;
    }

    private void Prune()
    {
        var now = time.GetUtcNow();
        _sent.RemoveAll(s => now - s.At > window);
    }
}
