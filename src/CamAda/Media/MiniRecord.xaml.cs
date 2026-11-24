using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CamAda.Core.Record;

namespace CamAda.Media;

/// <summary>
/// Adadaki küçük dönen plak (spec §7.1, §7.2): etiketi çalan şarkının kapağı. Dönüş <see cref="RecordSpin"/> fiziğiyle,
/// <see cref="AnimationTicker"/> (30 kare/sn) üzerinden ve yalnız <see cref="CanAnimate"/> iken ve (çalarken ya da yavaşlarken)
/// sürer; durunca saatten çıkar, boşta işlemci harcamaz.
/// </summary>
public sealed partial class MiniRecord : UserControl
{
    private readonly RecordSpin _spin = new();
    private readonly Action<TimeSpan> _onFrame;
    private ImageSource? _cover;
    private bool _coverShown;
    private bool _playing;
    private bool _canAnimate;
    private bool _rendering;

    public MiniRecord()
    {
        InitializeComponent();
        _onFrame = OnFrame;
        Diameter = 24;
        Unloaded += (_, _) => StopRendering();
    }

    public double Diameter
    {
        get => Width;
        set
        {
            Width = Height = value;
            Label.Width = Label.Height = value * 0.5;
            GrooveOuter.Margin = new Thickness(value * 0.08);
            GrooveInner.Margin = new Thickness(value * 0.17);
            Hole.Width = Hole.Height = Math.Max(1.5, value * 0.07);
        }
    }

    /// <summary>
    /// Plağın ekranda gerçekten göründüğü an (ör. küçük ada açıkken). Saydamlıkla gizlenen içerik WPF için hâlâ "görünür"
    /// olduğundan bunu sahibi söyler; false iken kare animasyonu çalışmaz ve açı donar.
    /// </summary>
    public bool CanAnimate
    {
        get => _canAnimate;
        set
        {
            _canAnimate = value;
            UpdateRendering();
        }
    }

    /// <summary>Etiketi (kapak yoksa düz koyu) ve dönme hedefini günceller.</summary>
    public void Show(ImageSource? cover, bool playing)
    {
        if (!_coverShown || !ReferenceEquals(cover, _cover))
        {
            _cover = cover;
            _coverShown = true;
            Label.Fill = cover is null
                ? new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x22))
                : new ImageBrush(cover) { Stretch = Stretch.UniformToFill };
        }

        _playing = playing;
        UpdateRendering();
    }

    private void UpdateRendering()
    {
        var wanted = _canAnimate && SystemParameters.ClientAreaAnimation && (_playing || _spin.IsMoving);
        if (wanted && !_rendering)
        {
            _rendering = true;
            AnimationTicker.Add(_onFrame);
        }
        else if (!wanted)
        {
            StopRendering();
        }
    }

    private void StopRendering()
    {
        if (_rendering)
        {
            _rendering = false;
            AnimationTicker.Remove(_onFrame);
        }
    }

    private void OnFrame(TimeSpan elapsed)
    {
        _spin.Advance(elapsed, _playing, reduceMotion: !SystemParameters.ClientAreaAnimation);
        Spin.Angle = _spin.Angle;

        if (!_playing && !_spin.IsMoving)
        {
            StopRendering();
        }
    }
}
