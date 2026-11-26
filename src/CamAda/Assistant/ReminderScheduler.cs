using System.Windows.Threading;
using CamAda.Core.Assistant;

namespace CamAda.Assistant;

/// <summary>
/// Hatırlatmaların zamanını duvar saatine göre denetler (2 sn'de bir, açılışta hemen ve uyanınca): uyku ve saat değişikliği
/// kaçmaz. Olaylar arayüz iş parçacığında.
/// </summary>
internal sealed class ReminderScheduler : IDisposable
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(2);
    private readonly ReminderFile _file;
    private readonly DispatcherTimer _timer;

    public ReminderScheduler(ReminderFile file, Dispatcher dispatcher)
    {
        _file = file;
        _timer = new DispatcherTimer(Every, DispatcherPriority.Normal, (_, _) => CheckNow(), dispatcher);
        _timer.Stop();
    }

    /// <summary>Zamanı gelen hatırlatmalar (birlikte dolanlar tek seferde).</summary>
    public event Action<IReadOnlyList<Reminder>>? Due;

    /// <summary>Her denetimde (adadaki geri sayım için).</summary>
    public event Action? Tick;

    public ReminderFile File => _file;

    /// <summary>Denetime başlar; CamAda kapalıyken dolanlar hemen "kaçırılan" olarak gelir.</summary>
    public void Start()
    {
        _timer.Start();
        CheckNow();
    }

    /// <summary>Hemen denetler (uykudan uyanınca).</summary>
    public void CheckNow()
    {
        var due = _file.TakeDue();
        if (due.Count > 0)
        {
            Due?.Invoke(due);
        }

        Tick?.Invoke();
    }

    public void Dispose() => _timer.Stop();
}
