using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class SensitiveTextTests
{
    [Theory]
    [InlineData("kartım 4111 1111 1111 1111")]
    [InlineData("TR33 0006 1005 1978 6457 8413 26")]
    [InlineData("kimlik no 10000000146")]
    [InlineData("şifrem 1234")]
    [InlineData("parola: abc")]
    [InlineData("cvv 123")]
    [InlineData("IBAN'ım şu")]
    public void Sensitive(string text) => Assert.True(SensitiveText.Looks(text));

    [Theory]
    [InlineData("kulaklık deyince Buds2")]
    [InlineData("Ankara'da oturuyor")]
    [InlineData("0555 111 22 33")]
    [InlineData("4111 1111 1111 1112")]
    public void Not_sensitive(string text) => Assert.False(SensitiveText.Looks(text));
}
