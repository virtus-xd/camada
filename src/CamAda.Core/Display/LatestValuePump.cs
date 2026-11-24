namespace CamAda.Core.Display;

/// <summary>
/// Tek yazıcı: aynı anda yalnız bir yazım koşar; yazım sürerken gelen değerlerin yalnız sonuncusu bekletilir ve o yazım
/// bitince yazılır. Böylece yavaş bir eski yazım hiçbir zaman sona kalmaz (ör. parlaklık; son inceleme bulgusu).
/// Yazıcının hatası pompayı durdurmaz; hatayı yazıcı kendisi ele alır.
/// </summary>
public sealed class LatestValuePump(Func<int, Task> write)
{
    private readonly object _gate = new();
    private int? _pending;
    private Task _running = Task.CompletedTask;
    private bool _busy;

    public void Post(int value)
    {
        lock (_gate)
        {
            _pending = value;
            if (_busy)
            {
                return;
            }

            _busy = true;
            _running = Task.Run(DrainAsync);
        }
    }

    /// <summary>Bekleyen ve süren yazımlar bitince tamamlanır.</summary>
    public Task WhenIdle()
    {
        lock (_gate)
        {
            return _running;
        }
    }

    private async Task DrainAsync()
    {
        while (true)
        {
            int value;
            lock (_gate)
            {
                if (_pending is not { } next)
                {
                    _busy = false;
                    return;
                }

                value = next;
                _pending = null;
            }

            try
            {
                await write(value);
            }
            catch (Exception)
            {
                // Yazıcı kendi hatasını günlüğe yazar; sonraki değer yine denenir.
            }
        }
    }
}
