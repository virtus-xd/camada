using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CamAda.Core.Assistant;

/// <summary>Bir argümanın değeri ya da kullanıcıya/modele dönecek hata metni.</summary>
public readonly record struct ArgResult<T>(T? Value, string? Error)
{
    public static ArgResult<T> Ok(T value) => new(value, null);

    public static ArgResult<T> Missing(string key) => new(default, $"eksik bilgi: {key}");

    public static ArgResult<T> Invalid(string key) => new(default, $"geçersiz değer: {key}");
}

/// <summary>
/// Modelden gelen argümanları okur. Model sayıyı metin ("40") ya da ondalık (40.0) gönderebilir; hepsi kabul edilir.
/// Hiçbir yol hata atmaz, hata metni döner.
/// </summary>
public static class ToolArgs
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static ArgResult<int> Int(JsonObject args, string key, int min, int max)
    {
        if (!args.TryGetPropertyValue(key, out var node) || node is null)
        {
            return ArgResult<int>.Missing(key);
        }

        double? number = node.GetValueKind() switch
        {
            JsonValueKind.Number => node.GetValue<double>(),
            JsonValueKind.String when double.TryParse(node.GetValue<string>().Trim().TrimStart('%'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
        return number is { } n && !double.IsNaN(n)
            ? ArgResult<int>.Ok((int)Math.Clamp(Math.Round(n, MidpointRounding.AwayFromZero), min, max))
            : ArgResult<int>.Invalid(key);
    }

    public static ArgResult<bool> Bool(JsonObject args, string key)
    {
        if (!args.TryGetPropertyValue(key, out var node) || node is null)
        {
            return ArgResult<bool>.Missing(key);
        }

        return node.GetValueKind() switch
        {
            JsonValueKind.True => ArgResult<bool>.Ok(true),
            JsonValueKind.False => ArgResult<bool>.Ok(false),
            JsonValueKind.String => node.GetValue<string>().Trim().ToLower(Turkish) switch
            {
                "true" or "evet" or "açık" or "ac" or "aç" => ArgResult<bool>.Ok(true),
                "false" or "hayır" or "kapalı" or "kapat" => ArgResult<bool>.Ok(false),
                _ => ArgResult<bool>.Invalid(key),
            },
            _ => ArgResult<bool>.Invalid(key),
        };
    }

    public static ArgResult<string> Text(JsonObject args, string key)
    {
        if (!args.TryGetPropertyValue(key, out var node) || node is null)
        {
            return ArgResult<string>.Missing(key);
        }

        var text = node.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : node.ToJsonString();
        return string.IsNullOrWhiteSpace(text) ? ArgResult<string>.Missing(key) : ArgResult<string>.Ok(text.Trim());
    }

    /// <summary>
    /// Modelin verdiği adresi tamamlar: yalnız http/https; şemasız ad "https://ad.com" olur. Başka şemalar (file:,
    /// javascript:) ve boşluk içeren metin reddedilir.
    /// </summary>
    public static string? NormalizeUrl(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || trimmed.Any(char.IsWhiteSpace))
        {
            return null;
        }

        if (trimmed.Contains("://", StringComparison.Ordinal))
        {
            return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? trimmed : null;
        }

        if (trimmed.Contains(':'))
        {
            return null;
        }

        var host = trimmed.ToLowerInvariant();
        return "https://" + (host.Contains('.') ? host : host + ".com");
    }

    public static string GoogleSearchUrl(string query) => "https://www.google.com/search?q=" + Uri.EscapeDataString(query.Trim());
}
