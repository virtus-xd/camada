namespace CamAda.Core.Island;

/// <summary>
/// Fareyle genişleme niyeti: imleç kısa bir süre üstünde kalınca genişler, ayrıldıktan biraz sonra küçülür.
/// Böylece imleç adanın üstünden hızla geçerken (ör. Chrome sekmelerine giderken) ada açılmaz.
/// Bir panel açıkken küçülmez; panel kapanırken imleç dışarıdaysa hemen küçülür.
/// </summary>
public sealed class HoverIntent : IDisposable
{
    private readonly TimeProvider _time;
    private readonly TimeSpan _enterDelay;
    private readonly TimeSpan _leaveDelay;
    private readonly object _gate = new();
    private ITimer? _pending;
    private bool _pointerInside;
    private bool _panelOpen;
    private bool _expanded;

    public HoverIntent(TimeProvider time, TimeSpan enterDelay, TimeSpan leaveDelay)
    {
        _time = time;
        _enterDelay = enterDelay;
        _leaveDelay = leaveDelay;
    }

    public bool IsExpanded
    {
        get { lock (_gate) { return _expanded; } }
    }

    /// <summary>Genişleme durumu değişince tetiklenir. Zamanlayıcı iş parçacığından gelebilir.</summary>
    public event EventHandler<bool>? ExpandedChanged;

    public void PointerEntered()
    {
        lock (_gate)
        {
            _pointerInside = true;
            CancelPending();
            if (!_expanded)
            {
                Schedule(_enterDelay, expand: true);
            }
        }
    }

    public void PointerExited()
    {
        lock (_gate)
        {
            _pointerInside = false;
            CancelPending();
            if (_expanded && !_panelOpen)
            {
                Schedule(_leaveDelay, expand: false);
            }
        }
    }

    public void SetPanelOpen(bool open)
    {
        var collapsed = false;
        lock (_gate)
        {
            _panelOpen = open;
            if (open)
            {
                CancelPending();
            }
            else if (!_pointerInside && _expanded)
            {
                CancelPending();
                _expanded = false;
                collapsed = true;
            }
        }

        if (collapsed)
        {
            ExpandedChanged?.Invoke(this, false);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            CancelPending();
        }
    }

    private void Schedule(TimeSpan delay, bool expand) =>
        _pending = _time.CreateTimer(_ => Fire(expand), null, delay, Timeout.InfiniteTimeSpan);

    private void Fire(bool expand)
    {
        lock (_gate)
        {
            // Gecikmiş ya da artık geçersiz bir zamanlayıcı durumu bozmasın.
            var stillWanted = expand ? _pointerInside : !_pointerInside && !_panelOpen;
            if (_expanded == expand || !stillWanted)
            {
                return;
            }

            _expanded = expand;
            CancelPending();
        }

        ExpandedChanged?.Invoke(this, expand);
    }

    private void CancelPending()
    {
        _pending?.Dispose();
        _pending = null;
    }
}
