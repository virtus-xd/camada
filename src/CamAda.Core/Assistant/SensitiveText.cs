using System.Globalization;
using System.Text.RegularExpressions;

namespace CamAda.Core.Assistant;

/// <summary>
/// Hafızaya hiç yazılmaması gereken bilgiler: kart numarası (Luhn), Türk IBAN'ı, T.C. kimlik numarası (algoritma) ve
/// şifre/parola gibi sözcükler. Telefon numarası hassas sayılmaz.
/// </summary>
public static partial class SensitiveText
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly string[] Words = ["şifre", "parola", "password", "cvv", "pin kodu", "iban"];

    public static bool Looks(string text)
    {
        // tr-TR'de "IBAN" → "ıban" olur; İngilizce yazılan sözcükler için değişmez küçültmeye de bakılır.
        var lower = text.ToLower(Turkish);
        var invariant = text.ToLowerInvariant();
        if (Words.Any(w => lower.Contains(w, StringComparison.Ordinal) || invariant.Contains(w, StringComparison.Ordinal)))
        {
            return true;
        }

        if (Iban().IsMatch(text.Replace(" ", "", StringComparison.Ordinal)))
        {
            return true;
        }

        foreach (Match match in CardLike().Matches(text))
        {
            var digits = new string(match.Value.Where(char.IsAsciiDigit).ToArray());
            if (digits.Length is >= 13 and <= 19 && Luhn(digits))
            {
                return true;
            }
        }

        return ElevenDigits().Matches(text).Any(m => IsTurkishId(m.Value));
    }

    private static bool Luhn(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var d = digits[digits.Length - 1 - i] - '0';
            if (i % 2 == 1)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }

            sum += d;
        }

        return sum % 10 == 0;
    }

    private static bool IsTurkishId(string s)
    {
        var d = s.Select(c => c - '0').ToArray();
        if (d[0] == 0)
        {
            return false;
        }

        var odd = d[0] + d[2] + d[4] + d[6] + d[8];
        var even = d[1] + d[3] + d[5] + d[7];
        return ((odd * 7 - even) % 10 + 10) % 10 == d[9] && d.Take(10).Sum() % 10 == d[10];
    }

    [GeneratedRegex(@"TR\d{24}", RegexOptions.IgnoreCase)]
    private static partial Regex Iban();

    [GeneratedRegex(@"\d(?:[ -]?\d){12,18}")]
    private static partial Regex CardLike();

    [GeneratedRegex(@"(?<!\d)\d{11}(?!\d)")]
    private static partial Regex ElevenDigits();
}
