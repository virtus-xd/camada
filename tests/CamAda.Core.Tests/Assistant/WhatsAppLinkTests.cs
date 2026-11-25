using CamAda.Core.Assistant;
using CamAda.Core.Settings;

namespace CamAda.Core.Tests.Assistant;

public class WhatsAppLinkTests
{
    [Theory]
    [InlineData("0555 111 22 33", "905551112233")]
    [InlineData("+90 555 111 2233", "905551112233")]
    [InlineData("905551112233", "905551112233")]
    [InlineData("5551112233", "905551112233")]
    [InlineData("12", null)]
    public void Phone_normalization(string input, string? expected) => Assert.Equal(expected, WhatsAppLink.NormalizePhone(input));

    [Fact]
    public void Builds_link_with_encoded_text() =>
        Assert.Equal(
            new WhatsAppLinkResult.Link("whatsapp://send?phone=905551112233&text=geliyorum%2C%205%20dk", "Ahmet"),
            WhatsAppLink.Build([new WhatsAppContact("Ahmet", "05551112233")], "ahmet", "geliyorum, 5 dk"));

    [Fact]
    public void Unknown_contact()
    {
        Assert.IsType<WhatsAppLinkResult.NoContact>(
            WhatsAppLink.Build([new WhatsAppContact("Ahmet", "05551112233")], "Mehmet", "x"));
        var ambiguous = Assert.IsType<WhatsAppLinkResult.Ambiguous>(WhatsAppLink.Build(
            [new WhatsAppContact("Ahmet Yılmaz", "05551112233"), new WhatsAppContact("Ahmet Kaya", "05552223344")], "Ahmet", "x"));
        Assert.Equal(["Ahmet Yılmaz", "Ahmet Kaya"], ambiguous.Names);
    }

    [Fact]
    public void Contact_with_bad_phone_is_no_contact() =>
        Assert.IsType<WhatsAppLinkResult.NoContact>(WhatsAppLink.Build([new WhatsAppContact("Ahmet", "12")], "Ahmet", "x"));
}
