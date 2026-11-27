using CamAda.Core.Assistant;
using CamAda.Core.Text;

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
        AssistantError.Network => midSession ? UiStrings.Ada_Error_ConnectionLost : UiStrings.Ada_Error_NoInternet,
        AssistantError.InvalidKey => UiStrings.Ada_Error_InvalidKey,
        AssistantError.PaymentRequired => UiStrings.Ada_Error_Payment,
        AssistantError.QuotaExceeded => UiStrings.Ada_Error_Quota,
        AssistantError.ModelUnavailable => UiStrings.Ada_Error_ModelUnavailable,
        AssistantError.Microphone => UiStrings.Mic_NotFound,
        _ => UiStrings.Ada_Error_Unknown,
    };
}
