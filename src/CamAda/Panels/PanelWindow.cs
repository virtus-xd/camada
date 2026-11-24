using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using CamAda.Interop;

namespace CamAda.Panels;

/// <summary>
/// Adanın altında açılan tüm panellerin penceresi: üstten 62 DIP, yatayda ortalı, 24 DIP köşeli düz cam.
/// Açılınca etkinleşir (klavye için); odağı kaybedince ya da Esc ile kapanır.
/// </summary>
public class PanelWindow : Window
{
    /// <summary>Panelin görünen üst kenarının ekranın üstüne uzaklığı (DIP).</summary>
    public const double TopFromScreen = 62;

    /// <summary>Panel kenarlığı + iç boşluk; içerik genişliği = panel genişliği − 2 × Inset.</summary>
    public const double Inset = 17;

    private const double ShadowPad = 24;

    private readonly Border _frame;
    private readonly TranslateTransform _slide = new();
    private bool _closing;
    private OutsideClickHook? _outsideClick;

    public PanelWindow(FrameworkElement content, double width)
    {
        Title = "CamAda panel";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Height;
        UseLayoutRounding = true;
        Width = width + 2 * ShadowPad;
        FontFamily = (FontFamily)FindResource("Manrope");
        Foreground = (Brush)FindResource("TextBrush");

        _frame = new Border
        {
            Margin = new Thickness(ShadowPad),
            CornerRadius = new CornerRadius(24),
            Background = (Brush)FindResource("PanelBackgroundBrush"),
            BorderBrush = (Brush)FindResource("GlassEdgeBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(Inset - 1),
            Child = content,
            RenderTransform = _slide,
            Effect = new DropShadowEffect
            {
                BlurRadius = 30,
                ShadowDepth = 8,
                Direction = 270,
                Color = Color.FromRgb(0x04, 0x06, 0x1A),
                Opacity = 0.45,
            },
        };
        Content = _frame;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                if (content is not IPanelEscape escape || !escape.HandleEscape())
                {
                    Dismiss();
                }
            }
        };
        Activated += (_, _) => StopWatchingOutsideClicks(); // panele geri dönüldü: normal odak kuralı geçerli
        Deactivated += (_, _) => OnDeactivated();
        Closed += (_, _) =>
        {
            StopWatchingOutsideClicks();
            PanelClosed?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>
    /// Odak, imleç paneldeyken başka bir pencereye geçerse (ör. tepsi uygulamasının sağ tık menüsü) panel açık kalır;
    /// o durumda panel dışına yapılan ilk tıklamada kapanır.
    /// </summary>
    public bool KeepOpenWhilePointerInside { get; init; }

    public event EventHandler? PanelOpened;

    public event EventHandler? PanelClosed;

    /// <summary>Paneli adanın altında ortalayıp gösterir ve etkinleştirir; 0,22 sn aşağı kayarak belirir.</summary>
    public void ShowUnder(Window island)
    {
        Left = island.Left + (island.ActualWidth - Width) / 2;
        Top = island.Top + TopFromScreen - ShadowPad;

        if (SystemParameters.ClientAreaAnimation)
        {
            var duration = TimeSpan.FromSeconds(0.22);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            _slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-10, 0, duration) { EasingFunction = ease });
            _frame.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        }

        Show();
        Activate();
        PanelOpened?.Invoke(this, EventArgs.Empty);
    }

    private void OnDeactivated()
    {
        if (KeepOpenWhilePointerInside && IsPointerInside())
        {
            // Odak panelden çıktı ama imleç hâlâ panelde (ör. uygulamanın menüsü): açık kal, dış tıklamayı kancayla bekle.
            _outsideClick ??= new OutsideClickHook(IsInside, () => Dispatcher.BeginInvoke(Dismiss));
            return;
        }

        Dismiss();
    }

    private void StopWatchingOutsideClicks()
    {
        _outsideClick?.Dispose();
        _outsideClick = null;
    }

    private bool IsPointerInside()
    {
        var (x, y) = Native.GetCursorPosition();
        return IsInside(x, y);
    }

    /// <summary>Ekran noktası (fiziksel piksel) panelin görünen çerçevesinin içinde mi.</summary>
    private bool IsInside(int x, int y)
    {
        if (PresentationSource.FromVisual(_frame) is null)
        {
            return false;
        }

        var dpi = VisualTreeHelper.GetDpi(_frame);
        var topLeft = _frame.PointToScreen(new Point(0, 0));
        return x >= topLeft.X && y >= topLeft.Y
            && x < topLeft.X + _frame.ActualWidth * dpi.DpiScaleX
            && y < topLeft.Y + _frame.ActualHeight * dpi.DpiScaleY;
    }

    /// <summary>Paneli bir kez kapatır (Esc, odak kaybı ve dışarıdan istekler aynı anda gelebilir).</summary>
    public void Dismiss()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        Close();
    }
}
