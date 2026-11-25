namespace CamAda.Core.Assistant;

/// <summary>
/// Live istemcisinin olaylarını ve kapanışını dinleyiciye sırayla verir. Dinleyici bağlanmadan gelenler bekletilir:
/// bağlantı, oturum dinleyicisini bağlamadan hemen önce kopsa da kapanış kaybolmaz. Kapanış bir kez verilir; sonrasında
/// gelen olaylar atılır. Dinleyiciler kilit içinde çağrılır: yalnız iletiyi başka iş parçacığına aktarmalıdır.
/// </summary>
public sealed class LiveDelivery
{
    private readonly object _lock = new();
    private readonly List<LiveEvent> _early = [];
    private Action<LiveEvent>? _received;
    private Action<AssistantError>? _closed;
    private AssistantError? _closedWith;

    public void Post(LiveEvent e)
    {
        lock (_lock)
        {
            if (_closedWith is not null)
            {
                return;
            }

            if (_received is { } received)
            {
                received(e);
            }
            else
            {
                _early.Add(e);
            }
        }
    }

    public void Close(AssistantError error)
    {
        lock (_lock)
        {
            if (_closedWith is not null)
            {
                return;
            }

            _closedWith = error;
            _closed?.Invoke(error);
        }
    }

    public void Attach(Action<LiveEvent> received, Action<AssistantError> closed)
    {
        lock (_lock)
        {
            if (_received is not null)
            {
                throw new InvalidOperationException("Dinleyici zaten bağlı.");
            }

            _received = received;
            _closed = closed;
            foreach (var e in _early)
            {
                received(e);
            }

            _early.Clear();
            if (_closedWith is { } error)
            {
                closed(error);
            }
        }
    }
}
