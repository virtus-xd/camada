using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using CamAda.Interop;
using ManagedShell.Common.Helpers;

namespace CamAda.Glass;

/// <summary>
/// Akrilik cam: Windows'un arka plan bulanıklığı, pencere bölgesiyle hap şekline kırpılır.
/// Bölge kenarları yumuşatılmaz ve gölge bölgenin dışında kaldığı için görünmez.
/// </summary>
public sealed class AcrylicGlass : IGlassBackdrop
{
    private Window? _host;
    private Border? _pill;
    private nint _hwnd;

    public void Attach(Window host, Border pill)
    {
        _host = host;
        _pill = pill;
        _hwnd = new WindowInteropHelper(host).Handle;
        WindowHelper.SetWindowBlur(_hwnd, true);
        _pill.SizeChanged += OnPillSizeChanged;
        UpdateRegion();
    }

    public void Dispose()
    {
        if (_pill is not null)
        {
            _pill.SizeChanged -= OnPillSizeChanged;
        }

        if (_hwnd != 0)
        {
            WindowHelper.SetWindowBlur(_hwnd, false);
            Native.ClearRegion(_hwnd);
        }
    }

    private void OnPillSizeChanged(object sender, SizeChangedEventArgs e) => UpdateRegion();

    private void UpdateRegion()
    {
        if (_host is null || _pill is null || _pill.ActualWidth <= 0)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(_host);
        var topLeft = _pill.TranslatePoint(new Point(0, 0), _host);
        var left = (int)Math.Round(topLeft.X * dpi.DpiScaleX);
        var top = (int)Math.Round(topLeft.Y * dpi.DpiScaleY);
        var right = (int)Math.Round((topLeft.X + _pill.ActualWidth) * dpi.DpiScaleX);
        var bottom = (int)Math.Round((topLeft.Y + _pill.ActualHeight) * dpi.DpiScaleY);
        Native.SetRoundedRegion(_hwnd, left, top, right, bottom, bottom - top);
    }
}
