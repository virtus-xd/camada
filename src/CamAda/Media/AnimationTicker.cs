using System.Diagnostics;
using System.Windows.Threading;

namespace CamAda.Media;

/// <summary>
/// Sürekli animasyonların (dönen plaklar, ekolayzır) ortak saati: saniyede 30 kare, abone yokken durur.
/// <see cref="System.Windows.Media.CompositionTarget.Rendering"/> kullanılmaz: ona abone olmak WPF'i ekranın tazeleme
/// hızında (ör. 144 Hz) durmadan çizmeye zorlar; ölçümde dönen bir disk tek çekirdeğin %26'sını yerken 30 kare/sn'lik
/// zamanlayıcıyla %2'ye indi. Tüm aboneler aynı tıkta güncellendiği için saniyede en fazla 30 çizim olur.
/// </summary>
public static class AnimationTicker
{
    public const int FramesPerSecond = 30;

    private static readonly List<Action<TimeSpan>> Subscribers = [];
    private static readonly Stopwatch Clock = new();
    private static DispatcherTimer? _timer;
    private static TimeSpan _lastTick;

    /// <summary>Her karede geçen süreyle çağrılır (arayüz iş parçacığında). Aynı işleyici iki kez eklenmez.</summary>
    public static void Add(Action<TimeSpan> onFrame)
    {
        if (Subscribers.Contains(onFrame))
        {
            return;
        }

        Subscribers.Add(onFrame);
        if (_timer is null)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromSeconds(1.0 / FramesPerSecond) };
            _timer.Tick += (_, _) => Tick();
        }

        if (!_timer.IsEnabled)
        {
            Clock.Restart();
            _lastTick = TimeSpan.Zero;
            _timer.Start();
        }
    }

    public static void Remove(Action<TimeSpan> onFrame)
    {
        Subscribers.Remove(onFrame);
        if (Subscribers.Count == 0)
        {
            _timer?.Stop();
        }
    }

    private static void Tick()
    {
        var now = Clock.Elapsed;
        var elapsed = now - _lastTick;
        _lastTick = now;
        foreach (var subscriber in Subscribers.ToArray())
        {
            subscriber(elapsed);
        }
    }
}
