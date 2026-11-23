using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CamAda.Island;

/// <summary>Hap şekli: köşe yarıçapı her zaman yüksekliğin yarısı (40 → 20, 46 → 23).</summary>
public sealed class HalfHeightRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is double height && height > 0 ? new CornerRadius(height / 2) : new CornerRadius(0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
