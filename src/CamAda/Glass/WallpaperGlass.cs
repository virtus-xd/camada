using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CamAda.Core.Glass;
using ManagedShell.AppBar;

namespace CamAda.Glass;

/// <summary>
/// Duvar kağıdı camı: ada penceresinin arkasındaki duvar kağıdı kesitini bulanıklaştırıp hapın arka planı yapar.
/// Akıllı gizlenme sayesinde ada tam görünürken altında pencere olmaz, arkasında duvar kağıdı vardır; bir pencerenin
/// üstünde açıldığında IslandWindow tonu koyulaştırır. Kenarlar kusursuz yumuşak kalır.
/// </summary>
public sealed class WallpaperGlass : IGlassBackdrop
{
    private const double BlurDip = 26;
    private const double Saturation = 1.7;

    private readonly WallpaperSource _source;
    private readonly Border _tintLayer;
    private Window? _host;
    private Border? _pill;
    private ImageBrush? _brush;

    public WallpaperGlass(WallpaperSource source, Border tintLayer)
    {
        _source = source;
        _tintLayer = tintLayer;
    }

    public void Attach(Window host, Border pill)
    {
        _host = host;
        _pill = pill;
        _pill.SizeChanged += OnPillSizeChanged;
        _source.Changed += OnSourceChanged;
        _host.DpiChanged += OnHostDpiChanged;
        _host.LocationChanged += OnSourceChanged;
        Regenerate();
    }

    public void Dispose()
    {
        if (_pill is not null)
        {
            _pill.SizeChanged -= OnPillSizeChanged;
            _pill.Background = (Brush)Application.Current.FindResource("IslandTintBrush");
        }

        if (_host is not null)
        {
            _host.DpiChanged -= OnHostDpiChanged;
            _host.LocationChanged -= OnSourceChanged;
        }

        _source.Changed -= OnSourceChanged;
        _tintLayer.Background = Brushes.Transparent;
    }

    private void OnPillSizeChanged(object sender, SizeChangedEventArgs e) => UpdateViewbox();

    private void OnSourceChanged(object? sender, EventArgs e) => Regenerate();

    private void OnHostDpiChanged(object sender, DpiChangedEventArgs e) => Regenerate();

    private void Regenerate()
    {
        if (_host is null || _pill is null || PresentationSource.FromVisual(_host) is null)
        {
            return;
        }

        var image = _source.LoadImage();
        if (image is null)
        {
            // Düz renk duvar kağıdı ya da okunamadı: düz cama düş.
            _pill.Background = (Brush)Application.Current.FindResource("SolidTintBrush");
            _tintLayer.Background = Brushes.Transparent;
            _brush = null;
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(_host);
        var origin = _host.PointToScreen(new Point(0, 0));                  // fiziksel piksel
        var screen = AppBarScreen.FromPrimaryScreen().Bounds;                // fiziksel piksel
        var width = (int)Math.Round(_host.Width * dpi.DpiScaleX);
        var height = (int)Math.Round(_host.Height * dpi.DpiScaleY);
        var radius = (int)Math.Round(BlurDip * dpi.DpiScaleX);
        var pad = radius * 3;                                                // kenarda bulanıklık için pay
        var paddedWidth = width + 2 * pad;
        var paddedHeight = height + 2 * pad;

        var region = new RectD(origin.X - screen.Left - pad, origin.Y - screen.Top - pad, paddedWidth, paddedHeight);
        var map = WallpaperLayout.MapToImage(
            region,
            new SizeD(screen.Width, screen.Height),
            new SizeD(image.PixelWidth, image.PixelHeight),
            _source.ReadStyle());

        // Kesiti cihaz pikselinde çiz (görüntü dışı siyah).
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var scaleX = paddedWidth / map.Width;
            var scaleY = paddedHeight / map.Height;
            dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, paddedWidth, paddedHeight));
            dc.DrawImage(image, new Rect(-map.X * scaleX, -map.Y * scaleY, image.PixelWidth * scaleX, image.PixelHeight * scaleY));
        }

        var target = new RenderTargetBitmap(paddedWidth, paddedHeight, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var stride = paddedWidth * 4;
        var pixels = new byte[stride * paddedHeight];
        target.CopyPixels(pixels, stride, 0);

        GlassImageProcessor.Process(pixels, paddedWidth, paddedHeight, radius, Saturation);

        var cropped = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            Buffer.BlockCopy(pixels, ((y + pad) * paddedWidth + pad) * 4, cropped, y * width * 4, width * 4);
        }

        // DPI'yi pencereninkine eşitle: bitmap'in DIP boyutu pencereyle aynı olur, Viewbox DIP cinsinden çalışır.
        var bitmap = BitmapSource.Create(width, height, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY,
            PixelFormats.Pbgra32, null, cropped, width * 4);
        bitmap.Freeze();

        _brush = new ImageBrush(bitmap) { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill };
        UpdateViewbox();
        _pill.Background = _brush;
        _tintLayer.Background = (Brush)Application.Current.FindResource("IslandTintBrush");
    }

    /// <summary>Hap genişlerken arka planın kaymaması için fırçanın baktığı alanı hapın penceredeki yerine eşitler.</summary>
    private void UpdateViewbox()
    {
        if (_brush is null || _pill is null || _host is null || _pill.ActualWidth <= 0)
        {
            return;
        }

        var topLeft = _pill.TranslatePoint(new Point(0, 0), _host);
        _brush.Viewbox = new Rect(topLeft.X, topLeft.Y, _pill.ActualWidth, _pill.ActualHeight);
    }
}
