using System.Windows;

namespace CamAda;

/// <summary>
/// WPF'te <see cref="System.Windows.Controls.Image"/> da ilk ölçümünde <see cref="Window.DpiChanged"/> ile aynı yönlendirilmiş
/// olayı yükseltir ve olay pencereye kadar kabarır. Adadaki her yeni uygulama simgesi bu yüzden pencerenin DpiChanged
/// dinleyicilerini tetikliyordu (her seferinde duvar kağıdı camı yeniden bulanıklaştırılıyordu). Dinleyiciler yalnız
/// pencerenin kendi, gerçek DPI değişimine tepki vermeli.
/// </summary>
public static class WindowDpi
{
    public static bool IsRealChange(Window window, DpiChangedEventArgs e) =>
        ReferenceEquals(e.OriginalSource, window) && e.OldDpi.PixelsPerDip != e.NewDpi.PixelsPerDip;
}
