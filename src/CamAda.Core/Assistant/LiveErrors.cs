namespace CamAda.Core.Assistant;

/// <summary>Ada'nın kullanıcıya ayrı bir metinle söylediği hata türleri.</summary>
public enum AssistantError
{
    None,
    Network,
    InvalidKey,
    PaymentRequired,
    QuotaExceeded,
    ModelUnavailable,
    Microphone,
    Unknown,
}

/// <summary>Google'ın kapanış açıklamalarını ve HTTP durumlarını hata türüne çevirir.</summary>
public static class LiveErrors
{
    /// <summary>
    /// WebSocket kapanışı: önce açıklama (kota, anahtar, ödeme, model); açıklama bunlardan değilse sunucu tarafı kapanış
    /// kodları (1001 ayrılma, 1006 kopma, 1011 iç hata, 1013/1014 hizmet yok) bağlantı sorunu sayılır. Boş açıklama da.
    /// </summary>
    public static AssistantError FromClose(string? description, int? status = null)
    {
        var error = FromDescription(description);
        return error == AssistantError.Unknown && status is 1001 or 1006 or 1011 or 1013 or 1014 ? AssistantError.Network : error;
    }

    private static AssistantError FromDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return AssistantError.Network;
        }

        var text = description.ToLowerInvariant();
        if (text.Contains("quota"))
        {
            return AssistantError.QuotaExceeded; // kota iletisi "billing details" de içerir: önce bakılır
        }

        if (text.Contains("api key") || text.Contains("api_key"))
        {
            return AssistantError.InvalidKey;
        }

        if (text.Contains("payment") || text.Contains("billing") || text.Contains("prepaid") || text.Contains("balance"))
        {
            return AssistantError.PaymentRequired;
        }

        return text.Contains("not supported for this model") || text.Contains("not found")
            ? AssistantError.ModelUnavailable
            : AssistantError.Unknown;
    }

    public static AssistantError FromHttp(int status, string? body) => status switch
    {
        400 when body?.Contains("API key", StringComparison.OrdinalIgnoreCase) == true => AssistantError.InvalidKey,
        401 or 403 => AssistantError.InvalidKey,
        402 => AssistantError.PaymentRequired,
        429 => AssistantError.QuotaExceeded,
        404 => AssistantError.ModelUnavailable,
        >= 500 => AssistantError.Network,
        _ => AssistantError.Unknown,
    };
}
