using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class ApiKeyTextTests
{
    [Theory]
    [InlineData("AIzaSyD-abc_123XYZ", true)]
    [InlineData("AIzaSyDıabc", false)]   // Türkçe klavyeden kalan harf: "internet yok" değil "anahtar geçersiz"
    [InlineData("AIza Sy", false)]
    [InlineData("AIzaSy​abc", false)] // görünmez karakter
    [InlineData("", false)]
    public void Well_formed(string key, bool expected) => Assert.Equal(expected, ApiKeyText.IsWellFormed(key));
}
