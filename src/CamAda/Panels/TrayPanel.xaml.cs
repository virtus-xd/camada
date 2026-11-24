using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CamAda.Interop;
using ManagedShell.Common.Helpers;
using ManagedShell.Interop;
using ManagedShell.WindowsTray;

namespace CamAda.Panels;

/// <summary>
/// "Arka planda çalışanlar" paneli: bildirim alanı simgeleri 4 sütunlu ızgarada. Fare olayları simgenin uygulamasına
/// iletilir; böylece sol tık uygulamayı açar, sağ tık uygulamanın kendi menüsünü gösterir.
/// </summary>
public sealed partial class TrayPanel : UserControl
{
    public const double PanelWidth = 380;

    private readonly ListCollectionView _icons;

    public TrayPanel(NotificationArea area)
    {
        InitializeComponent();

        _icons = new ListCollectionView(area.TrayIcons)
        {
            Filter = item => item is NotifyIcon { IsHidden: false },
            IsLiveFiltering = true,
        };
        _icons.LiveFilteringProperties.Add(nameof(NotifyIcon.IsHidden));
        ((System.Collections.Specialized.INotifyCollectionChanged)_icons).CollectionChanged += OnIconsChanged;
        Icons.ItemsSource = _icons;
        UpdateEmptyText();

        // Görünüm tepsinin kalıcı koleksiyonuna bağlıdır; ayrılmazsa kapanan her panel bellekte kalır ve tepsi
        // değişikliklerini işlemeye devam eder.
        Unloaded += (_, _) =>
        {
            ((System.Collections.Specialized.INotifyCollectionChanged)_icons).CollectionChanged -= OnIconsChanged;
            Icons.ItemsSource = null;
            _icons.DetachFromSourceCollection();
        };
    }

    private void OnIconsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        UpdateEmptyText();

    private void UpdateEmptyText() =>
        EmptyText.Visibility = _icons.IsEmpty ? Visibility.Visible : Visibility.Collapsed;

    private static NotifyIcon? IconOf(object sender) => (sender as FrameworkElement)?.DataContext as NotifyIcon;

    private void Icon_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement cell || IconOf(sender) is not { } icon)
        {
            return;
        }

        // Uygulamanın açılır penceresini simgenin yanına koyabilmesi için simgenin ekran dikdörtgeni (Shell_NotifyIconGetRect).
        // RetroBar gibi: Left/Top ekran konumu, Right/Bottom genişlik/yükseklik (fiziksel piksel).
        var dpi = VisualTreeHelper.GetDpi(cell);
        var location = cell.PointToScreen(new Point(0, 0));
        icon.Placement = new NativeMethods.Rect
        {
            Left = (int)location.X,
            Top = (int)location.Y,
            Right = (int)(cell.ActualWidth * dpi.DpiScaleX),
            Bottom = (int)(cell.ActualHeight * dpi.DpiScaleY),
        };
        icon.IconMouseEnter(MouseHelper.GetCursorPositionParam());
    }

    private void Icon_MouseLeave(object sender, MouseEventArgs e) =>
        IconOf(sender)?.IconMouseLeave(MouseHelper.GetCursorPositionParam());

    private void Icon_MouseMove(object sender, MouseEventArgs e) =>
        IconOf(sender)?.IconMouseMove(MouseHelper.GetCursorPositionParam());

    private void Icon_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        IconOf(sender)?.IconMouseDown(e.ChangedButton, MouseHelper.GetCursorPositionParam(), Native.DoubleClickTime);
    }

    private void Icon_MouseUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        IconOf(sender)?.IconMouseUp(e.ChangedButton, MouseHelper.GetCursorPositionParam(), Native.DoubleClickTime);
    }
}

/// <summary>Tepsi simgesinin ipucu metni birden çok satır olabilir; ızgarada yalnız ilk satır gösterilir.</summary>
public sealed class FirstLineConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as string)?.Split('\n', 2)[0].Trim() ?? string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
