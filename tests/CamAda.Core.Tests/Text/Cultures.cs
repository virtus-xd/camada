using System.Globalization;

namespace CamAda.Core.Tests.Text;

/// <summary>Testlerde metin üreten fonksiyonlara açıkça verilen kültürler.</summary>
internal static class Cultures
{
    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    public static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    public static readonly CultureInfo EnGb = CultureInfo.GetCultureInfo("en-GB");
}
