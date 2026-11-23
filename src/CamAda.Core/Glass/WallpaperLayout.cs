namespace CamAda.Core.Glass;

public static class WallpaperLayout
{
    /// <summary>
    /// Ekrandaki bir bölgeyi (ekranın sol üstüne göre, ekranla aynı birimde) duvar kağıdı görüntüsünün
    /// piksel koordinatlarına çevirir. Sonuç görüntünün dışına taşabilir (Sığdır ve Ortala'da kenar boşlukları).
    /// </summary>
    public static RectD MapToImage(RectD regionOnScreen, SizeD screen, SizeD image, WallpaperStyle style)
    {
        if (style == WallpaperStyle.Stretch)
        {
            var sx = image.Width / screen.Width;
            var sy = image.Height / screen.Height;
            return new RectD(regionOnScreen.X * sx, regionOnScreen.Y * sy, regionOnScreen.Width * sx, regionOnScreen.Height * sy);
        }

        var scale = style switch
        {
            WallpaperStyle.Fit => Math.Min(screen.Width / image.Width, screen.Height / image.Height),
            WallpaperStyle.Center => 1.0,
            _ => Math.Max(screen.Width / image.Width, screen.Height / image.Height),
        };

        // Ölçeklenmiş görüntünün ekrandaki sol üst köşesi (Doldur'da negatif: taşan kısım kırpılır).
        var placedX = (screen.Width - image.Width * scale) / 2;
        var placedY = (screen.Height - image.Height * scale) / 2;

        return new RectD(
            (regionOnScreen.X - placedX) / scale,
            (regionOnScreen.Y - placedY) / scale,
            regionOnScreen.Width / scale,
            regionOnScreen.Height / scale);
    }
}
