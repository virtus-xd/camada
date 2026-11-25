using CamAda.Core.Assistant;

namespace CamAda.Assistant;

/// <summary>Ada'nın adada gösterilen hâli.</summary>
public abstract record AdaState
{
    private AdaState()
    {
    }

    public sealed record Idle : AdaState;

    public sealed record Connecting : AdaState;

    /// <summary>Dinliyor; <see cref="UserText"/> kullanıcının o anki sözü (Google'ın canlı yazısı), henüz yoksa null.</summary>
    public sealed record Listening(string? UserText) : AdaState;

    public sealed record Speaking(string AdaText) : AdaState;

    /// <summary>4 sn gösterilip <see cref="Idle"/>'a döner.</summary>
    public sealed record Failed(AssistantError Error, string Message) : AdaState;

    /// <summary>Hatanın adada görünen metni; <paramref name="midSession"/> oturum sürerken kopmalar için.</summary>
    public static string MessageFor(AssistantError error, bool midSession) => error switch
    {
        AssistantError.Network => midSession ? "Bağlantı koptu" : "Ada'ya ulaşılamıyor — internet yok",
        AssistantError.InvalidKey => "Gemini anahtarı geçersiz — Ada ayarlarından yenile",
        AssistantError.PaymentRequired => "Gemini hesabında ödeme gerekiyor",
        AssistantError.QuotaExceeded => "Gemini kullanım sınırı doldu",
        AssistantError.ModelUnavailable => "Ada'nın modeli şu an kullanılamıyor",
        AssistantError.Microphone => "Mikrofon bulunamadı",
        _ => "Ada'da bir sorun oldu",
    };
}
