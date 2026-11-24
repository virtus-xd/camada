using System.Windows;
using System.Windows.Media;

namespace CamAda.Media;

/// <summary>
/// Küçük adadaki ekolayzır (spec §7.1): 4 çubuk, 3×12 DIP, #FFC27A. Çalarken <see cref="AnimationTicker"/>'ın her iki karesinde
/// bir (15 kare/sn) sözde rastgele kıpırdar, duraklayınca alçak ve durgun kalır. Gerçek ses seviyesi değil, süstür;
/// <see cref="CanAnimate"/> false iken saatten çıkar.
/// </summary>
public sealed class EqualizerBars : FrameworkElement
{
    private const int Bars = 4;
    private const double BarWidth = 3;
    private const double Gap = 2;
    private const double BarMaxHeight = 12;
    private const double BarRestHeight = 3;

    private static readonly Brush Fill = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xC2, 0x7A)));

    private readonly double[] _heights = [BarRestHeight, BarRestHeight, BarRestHeight, BarRestHeight];
    private readonly double[] _targets = new double[Bars];
    private readonly Random _random = new(19);
    private readonly Action<TimeSpan> _onFrame;
    private bool _isPlaying;
    private bool _canAnimate;
    private bool _ticking;
    private bool _skipFrame;

    public EqualizerBars()
    {
        Width = Bars * BarWidth + (Bars - 1) * Gap;
        Height = BarMaxHeight;
        IsHitTestVisible = false;
        _onFrame = _ => Step();
        Unloaded += (_, _) => StopTicking();
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            _isPlaying = value;
            Update();
        }
    }

    /// <summary>Ekolayzır ekranda görünüyor mu (küçük ada açık); false iken zamanlayıcı çalışmaz.</summary>
    public bool CanAnimate
    {
        get => _canAnimate;
        set
        {
            _canAnimate = value;
            Update();
        }
    }

    private void Update()
    {
        if (_isPlaying && _canAnimate && SystemParameters.ClientAreaAnimation)
        {
            if (!_ticking)
            {
                _ticking = true;
                AnimationTicker.Add(_onFrame);
            }

            return;
        }

        StopTicking();
        var height = _isPlaying ? BarMaxHeight * 0.5 : BarRestHeight; // animasyonsuz çalarken orta boyda durur
        Array.Fill(_heights, height);
        InvalidateVisual();
    }

    private void StopTicking()
    {
        if (_ticking)
        {
            _ticking = false;
            AnimationTicker.Remove(_onFrame);
        }
    }

    private void Step()
    {
        _skipFrame = !_skipFrame;
        if (_skipFrame)
        {
            return; // 30 kare/sn saatin her iki karesinde bir: 15 kare/sn
        }

        for (var i = 0; i < Bars; i++)
        {
            if (Math.Abs(_heights[i] - _targets[i]) < 1)
            {
                _targets[i] = BarRestHeight + _random.NextDouble() * (BarMaxHeight - BarRestHeight);
            }

            _heights[i] += (_targets[i] - _heights[i]) * 0.5;
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        for (var i = 0; i < Bars; i++)
        {
            var h = Math.Clamp(_heights[i], BarRestHeight, BarMaxHeight);
            dc.DrawRoundedRectangle(Fill, null, new Rect(i * (BarWidth + Gap), BarMaxHeight - h, BarWidth, h), 1.5, 1.5);
        }
    }

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
