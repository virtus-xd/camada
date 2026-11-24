using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CamAda.Core.Media;
using CamAda.Core.Record;
using CamAda.Glass;

namespace CamAda.Record;

/// <summary>
/// Plağın gerçekçi çizimi (spec §7.8 "Gerçekçilik"), alttan üste: kapağın baskın renginden hafif hâle, masaüstüne düşen
/// gölge, <b>dönen grup</b> (oluklu disk dokusu, kapak etiketi, etiket kenarında parlak ince halka, mil deliği) ve
/// <b>dönmeyen</b> katmanlar (kama şeklinde yansıma ve hafif gökkuşağı, etiket üstünde yumuşak parlama). Gölge ve hâle efekt
/// değil düz katmanlar: canlı bir Effect, üstünde dönen disk varken her karede yeniden hesaplanıyordu. Kare başına yalnız
/// dönen grubun açısı değişir; dokular boyut ya da DPI değişince bir kez üretilir.
/// </summary>
public sealed class RecordVisual : Grid
{
    private static readonly Brush EmptyLabel = Frozen(new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x22)));

    private readonly SoftShadow _halo = new() { Depth = 0, BlurRadius = 40, ShadowOpacity = 0.35 };
    private readonly SoftShadow _shadow = new() { Depth = 12, BlurRadius = 30, ShadowOpacity = 0.5, Color = Colors.Black };
    private readonly Grid _spinner = new() { RenderTransformOrigin = new Point(0.5, 0.5) };
    private readonly RotateTransform _rotation = new();
    private readonly Image _disc = new() { Stretch = Stretch.Fill };
    private readonly Ellipse _labelBack = new() { Fill = EmptyLabel };
    private readonly Ellipse _labelFront = new() { Fill = EmptyLabel };
    private readonly Ellipse _labelRing = new() { StrokeThickness = 1.5 };
    private readonly Ellipse _hole = new() { Width = 6, Height = 6 };
    private readonly Image _sheen = new() { Stretch = Stretch.Fill, IsHitTestVisible = false };
    private readonly Ellipse _glare = new() { IsHitTestVisible = false };
    private double _diameter;
    private double _textureScale;
    private ImageSource? _cover;
    private bool _coverShown;

    public RecordVisual()
    {
        RenderOptions.SetBitmapScalingMode(_disc, BitmapScalingMode.HighQuality);
        RenderOptions.SetBitmapScalingMode(_sheen, BitmapScalingMode.HighQuality);
        _shadow.RenderTransform = new TranslateTransform(8, 0);
        _halo.IsHitTestVisible = false;
        _shadow.IsHitTestVisible = false;

        _labelRing.Stroke = Frozen(new LinearGradientBrush(
            [new GradientStop(Color.FromArgb(0x99, 255, 255, 255), 0), new GradientStop(Color.FromArgb(0x0D, 255, 255, 255), 0.5),
             new GradientStop(Color.FromArgb(0x66, 255, 255, 255), 1)], 45));
        _hole.Fill = Frozen(new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x10)));
        _hole.Stroke = Frozen(new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)));
        _hole.StrokeThickness = 0.75;
        _glare.Fill = Frozen(new RadialGradientBrush(Color.FromArgb(0x1F, 255, 255, 255), Color.FromArgb(0, 255, 255, 255))
        {
            GradientOrigin = new Point(0.3, 0.25),
            Center = new Point(0.35, 0.3),
            RadiusX = 0.7,
            RadiusY = 0.7,
        });

        _spinner.RenderTransform = _rotation;
        _spinner.Children.Add(_disc);
        _spinner.Children.Add(_labelBack);
        _spinner.Children.Add(_labelFront);
        _spinner.Children.Add(_labelRing);
        _spinner.Children.Add(_hole);

        Children.Add(_halo);
        Children.Add(_shadow);
        Children.Add(_spinner);
        Children.Add(_sheen);
        Children.Add(_glare);

        ShowCover(null, DominantColor.Fallback);
    }

    /// <summary>Dönen grubun açısı (derece).</summary>
    public double Angle
    {
        get => _rotation.Angle;
        set => _rotation.Angle = value;
    }

    /// <summary>Ölçüleri uygular; disk ve yansıma dokuları fiziksel piksel boyutunda (DPI başına bir kez) üretilir.</summary>
    public void Configure(double diameter, double labelDiameter, double dpiScale)
    {
        Width = Height = diameter;
        _shadow.Width = _shadow.Height = diameter;
        _halo.Width = _halo.Height = diameter * 1.12;
        _halo.Margin = new Thickness(-diameter * 0.06);
        foreach (var label in new[] { _labelBack, _labelFront, _labelRing, _glare })
        {
            label.Width = label.Height = labelDiameter;
        }

        if (diameter == _diameter && dpiScale == _textureScale)
        {
            return;
        }

        _diameter = diameter;
        _textureScale = dpiScale;
        var pixels = Math.Max(16, (int)Math.Round(diameter * dpiScale));
        _disc.Source = ToBitmap(DiscTexture.Render(pixels), pixels, dpiScale);
        _sheen.Source = ToBitmap(SheenTexture.Render(pixels), pixels, dpiScale);
    }

    /// <summary>Yeni kapak eskisinin üstünde 0,4 sn'de belirir; hâle rengi 0,6 sn'de değişir ("animasyonları azalt"ta anında).</summary>
    public void ShowCover(ImageSource? cover, Rgb color)
    {
        var animate = SystemParameters.ClientAreaAnimation && _coverShown;
        if (!_coverShown || !ReferenceEquals(cover, _cover))
        {
            _cover = cover;
            _coverShown = true;
            _labelBack.Fill = _labelFront.Fill;
            _labelFront.Fill = cover is null ? EmptyLabel : new ImageBrush(cover) { Stretch = Stretch.UniformToFill };
            _labelFront.BeginAnimation(OpacityProperty,
                animate ? new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.4)) : null);
        }

        var target = Color.FromRgb(color.R, color.G, color.B);
        if (_halo.Color != target)
        {
            _halo.BeginAnimation(SoftShadow.ColorProperty,
                animate ? new ColorAnimation(target, TimeSpan.FromSeconds(0.6)) : null);
            if (!animate)
            {
                _halo.Color = target;
            }
        }
    }

    private static BitmapSource ToBitmap(byte[] pbgra, int size, double dpiScale)
    {
        var bitmap = BitmapSource.Create(size, size, 96 * dpiScale, 96 * dpiScale, PixelFormats.Pbgra32, null, pbgra, size * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
