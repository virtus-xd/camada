namespace CamAda.Core.Glass;

/// <summary>Duvar kağıdının ekrana yerleşimi. Döşe ve Yay desteklenmez, Doldur gibi davranır.</summary>
public enum WallpaperStyle
{
    Fill,
    Fit,
    Stretch,
    Center,
}

public static class WallpaperStyleParser
{
    /// <summary>
    /// HKCU\Control Panel\Desktop altındaki WallpaperStyle ve TileWallpaper değerlerini çevirir:
    /// "10" Doldur, "6" Sığdır, "2" Uzat, "0"+"0" Ortala. Döşe ("0"+"1"), Yay ("22") ve bilinmeyenler Doldur sayılır.
    /// </summary>
    public static WallpaperStyle Parse(string? wallpaperStyle, string? tileWallpaper) =>
        (wallpaperStyle?.Trim(), tileWallpaper?.Trim()) switch
        {
            ("6", _) => WallpaperStyle.Fit,
            ("2", _) => WallpaperStyle.Stretch,
            ("0", "0") => WallpaperStyle.Center,
            _ => WallpaperStyle.Fill,
        };
}
