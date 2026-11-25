using CamAda.Core.Settings;

namespace CamAda.Core.Assistant;

public abstract record WhatsAppLinkResult
{
    private WhatsAppLinkResult()
    {
    }

    public sealed record Link(string Uri, string ContactName) : WhatsAppLinkResult;

    public sealed record Ambiguous(IReadOnlyList<string> Names) : WhatsAppLinkResult;

    public sealed record NoContact : WhatsAppLinkResult;
}

/// <summary>
/// WhatsApp masaüstünde sohbeti mesaj hazır yazılmış olarak açan bağlantı (<c>whatsapp://send</c>). Gönderme
/// kullanıcıdadır; kişiler ayar dosyasındaki rehberden gelir.
/// </summary>
public static class WhatsAppLink
{
    /// <summary>
    /// Yalnız rakamlar; "0" ile başlayan 11 hane ve "5" ile başlayan 10 hane Türkiye numarası sayılıp "90" eklenir.
    /// 10 haneden kısa numara geçersizdir.
    /// </summary>
    public static string? NormalizePhone(string phone)
    {
        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        return digits.Length switch
        {
            11 when digits[0] == '0' => "90" + digits[1..],
            10 when digits[0] == '5' => "90" + digits,
            >= 10 => digits,
            _ => null,
        };
    }

    public static WhatsAppLinkResult Build(IReadOnlyList<WhatsAppContact> contacts, string person, string message)
    {
        switch (NameMatch.Best(person, contacts.Select(c => c.Name).ToList()))
        {
            case NameMatchResult.Found found:
                var contact = contacts[found.Index];
                return NormalizePhone(contact.Phone) is { } phone
                    ? new WhatsAppLinkResult.Link($"whatsapp://send?phone={phone}&text={Uri.EscapeDataString(message)}", contact.Name)
                    : new WhatsAppLinkResult.NoContact();
            case NameMatchResult.Ambiguous ambiguous:
                return new WhatsAppLinkResult.Ambiguous(ambiguous.Indexes.Select(i => contacts[i].Name).ToList());
            default:
                return new WhatsAppLinkResult.NoContact();
        }
    }
}
