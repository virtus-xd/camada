using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CamAda.Core.Glass;
using CamAda.Core.Island;
using CamAda.Interop;
using ManagedShell.AppBar;

namespace CamAda.Island;

/// <summary>
/// Akıllı gizlenme (spec §7.6): adanın kapladığı alana bir pencere girip girmediğini 250 ms'de bir yoklar;
/// örtülüyken imleci 50 ms'de bir yoklayıp üst kenarda ya da hapın üstündeyken <see cref="HoverIntent"/>'i besler.
/// </summary>
public sealed class SmartHide : IDisposable
{
    private const double ShadowSide = 30;
    private const double ShadowBelow = 38;

    private readonly Window _island;
    private readonly HoverIntent _hover;
    private readonly Func<(int X, int Y)?> _pinnedPosition;
    private readonly Func<Border> _pill;
    private readonly DispatcherTimer _occlusionTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _cursorTimer = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly int _ownProcessId = Environment.ProcessId;
    private bool _cursorInside;

    public SmartHide(Window island, HoverIntent hover, Func<(int X, int Y)?> pinnedPosition, Func<Border> pill)
    {
        _island = island;
        _hover = hover;
        _pinnedPosition = pinnedPosition;
        _pill = pill;
        _occlusionTimer.Tick += (_, _) => CheckOcclusion();
        _cursorTimer.Tick += (_, _) => PollCursor();
    }

    /// <summary>Adanın kapladığı alana şu an bir pencere giriyor mu.</summary>
    public bool IsOccluded { get; private set; }

    /// <summary>Örtülme durumu değişince tetiklenir (UI iş parçacığında).</summary>
    public event EventHandler? StateChanged;

    public void Start()
    {
        _occlusionTimer.Start();
        CheckOcclusion();
    }

    public void Dispose()
    {
        _occlusionTimer.Stop();
        _cursorTimer.Stop();
    }

    private void CheckOcclusion()
    {
        if (_pinnedPosition() is not { } pinned)
        {
            return;
        }

        // Adanın ekranda kapladığı alan: küçük hap + gölge payı.
        var dpi = VisualTreeHelper.GetDpi(_island);
        var pillLeft = (IslandLayout.WindowWidth - IslandLayout.Compact.Width) / 2;
        var footprint = new RectD(
            pinned.X + (pillLeft - ShadowSide) * dpi.DpiScaleX,
            pinned.Y,
            (IslandLayout.Compact.Width + 2 * ShadowSide) * dpi.DpiScaleX,
            (IslandLayout.TopMargin + IslandLayout.Compact.Height + ShadowBelow) * dpi.DpiScaleY);
        var occluded = OcclusionRule.IsOccluded(Native.SampleTopLevelWindows(_ownProcessId), footprint);

        if (occluded == IsOccluded)
        {
            return;
        }

        IsOccluded = occluded;
        if (IsOccluded)
        {
            _cursorInside = false;
            _cursorTimer.Start();
        }
        else
        {
            _cursorTimer.Stop();
            if (_cursorInside && !_pill().IsMouseOver)
            {
                _hover.PointerExited();
            }

            _cursorInside = false;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Örtülüyken imleci yoklar: üst kenarda (orta 900 DIP) ya da görünür hapın içindeyse "içeride".</summary>
    private void PollCursor()
    {
        if (_pinnedPosition() is not { } pinned)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(_island);
        var screenTop = AppBarScreen.FromPrimaryScreen().Bounds.Top;
        var (x, y) = Native.GetCursorPosition();
        var zoneLeft = pinned.X + (IslandLayout.WindowWidth - IslandLayout.RevealZoneWidth) / 2 * dpi.DpiScaleX;
        var zoneRight = zoneLeft + IslandLayout.RevealZoneWidth * dpi.DpiScaleX;
        var atTopEdge = RevealZone.IsAtTopEdge(x, y, zoneLeft, zoneRight, screenTop);

        var pill = _pill();
        var overPill = false;
        if (pill.IsHitTestVisible && pill.ActualWidth > 0)
        {
            var topLeft = pill.PointToScreen(new Point(0, 0));
            const double margin = 4;
            overPill = x >= topLeft.X - margin && x <= topLeft.X + pill.ActualWidth * dpi.DpiScaleX + margin
                    && y >= topLeft.Y - margin && y <= topLeft.Y + pill.ActualHeight * dpi.DpiScaleY + margin;
        }

        var inside = atTopEdge || overPill;
        if (inside == _cursorInside)
        {
            return;
        }

        _cursorInside = inside;
        if (inside)
        {
            _hover.PointerEntered();
        }
        else
        {
            _hover.PointerExited();
        }
    }
}
