using System.Globalization;

namespace CamAda.Core.Text;

/// <summary>Arayüzün de kullandığı dile bağlı biçimler.</summary>
public static class Formats
{
    /// <summary>Yüzde: Türkçede "%80", İngilizcede "80%".</summary>
    public static string Percent(int value, CultureInfo ui) => CoreText.Percent(value, ui);
}
