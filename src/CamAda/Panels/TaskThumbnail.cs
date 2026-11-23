using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CamAda.Interop;

namespace CamAda.Panels;

/// <summary>
/// Bir pencerenin canlı önizlemesi (RetroBar'ın TaskThumbnail yaklaşımı): DWM küçük resmi panel penceresine kaydedilir,
/// bu öğenin yerine kaynak oranı korunarak çizilir. DWM resmi WPF'in üstünde durduğu için konumu, görünürlüğü ve
/// saydamlığı her karede öğeye eşitlenir (yalnız değişince DWM'ye gönderilir).
/// </summary>
public sealed class TaskThumbnail : FrameworkElement
{
    private nint _source;
    private nint _thumbnail;
    private Window? _host;
    private Native.ThumbRect _lastRect;
    private byte _lastOpacity;
    private bool _lastVisible;

    public TaskThumbnail()
    {
        Width = 236;
        Height = 140;
        Loaded += (_, _) => Register();
        Unloaded += (_, _) => Unregister();
    }

    public nint SourceWindow
    {
        get => _source;
        set
        {
            _source = value;
            if (IsLoaded)
            {
                Unregister();
                Register();
            }
        }
    }

    private void Register()
    {
        _host = Window.GetWindow(this);
        if (_host is null || _source == 0)
        {
            return;
        }

        var hostHandle = new WindowInteropHelper(_host).Handle;
        if (hostHandle == 0 || Native.DwmRegisterThumbnail(hostHandle, _source, out _thumbnail) != 0)
        {
            _thumbnail = 0; // kaynak pencere kapanmış ya da DWM reddetti: kart başlıkla kalır
            return;
        }

        CompositionTarget.Rendering += OnRendering;
        Update(force: true);
    }

    private void Unregister()
    {
        CompositionTarget.Rendering -= OnRendering;
        if (_thumbnail != 0)
        {
            Native.DwmUnregisterThumbnail(_thumbnail);
            _thumbnail = 0;
        }
    }

    private void OnRendering(object? sender, EventArgs e) => Update(force: false);

    private void Update(bool force)
    {
        if (_thumbnail == 0 || _host is null || !_host.IsAncestorOf(this))
        {
            return;
        }

        if (Native.DwmQueryThumbnailSourceSize(_thumbnail, out var size) != 0 || size.X <= 0 || size.Y <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var origin = TransformToAncestor(_host).Transform(new Point(0, 0));
        var boxWidth = ActualWidth * dpi.DpiScaleX;
        var boxHeight = ActualHeight * dpi.DpiScaleY;

        // Kaynak oranını koru, küçük pencereleri büyütme.
        var scale = Math.Min(1, Math.Min(boxWidth / size.X, boxHeight / size.Y));
        var width = size.X * scale;
        var height = size.Y * scale;
        var left = origin.X * dpi.DpiScaleX + (boxWidth - width) / 2;
        var top = origin.Y * dpi.DpiScaleY + (boxHeight - height) / 2;

        var rect = new Native.ThumbRect
        {
            Left = (int)Math.Round(left),
            Top = (int)Math.Round(top),
            Right = (int)Math.Round(left + width),
            Bottom = (int)Math.Round(top + height),
        };
        var opacity = (byte)Math.Round(255 * EffectiveOpacity());
        var visible = IsVisible;

        if (!force && rect.Equals(_lastRect) && opacity == _lastOpacity && visible == _lastVisible)
        {
            return;
        }

        _lastRect = rect;
        _lastOpacity = opacity;
        _lastVisible = visible;

        var properties = new Native.DwmThumbnailProperties
        {
            Flags = Native.DWM_TNP_RECTDESTINATION | Native.DWM_TNP_OPACITY | Native.DWM_TNP_VISIBLE
                | Native.DWM_TNP_SOURCECLIENTAREAONLY,
            Destination = rect,
            Opacity = opacity,
            Visible = visible ? 1 : 0,
            SourceClientAreaOnly = 0,
        };
        Native.DwmUpdateThumbnailProperties(_thumbnail, in properties);
    }

    /// <summary>Panelin açılış solması önizlemeye de yansısın diye ata öğelerin saydamlıklarının çarpımı.</summary>
    private double EffectiveOpacity()
    {
        var opacity = 1.0;
        for (DependencyObject? node = this; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is UIElement element)
            {
                opacity *= element.Opacity;
            }
        }

        return opacity;
    }
}
