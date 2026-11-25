using CamAda.Core.Assistant;

namespace CamAda.Assistant;

/// <summary>Ada'nın bağlantı ya da hizmet hatası; <see cref="Error"/> kullanıcıya gösterilecek metni belirler.</summary>
public sealed class AssistantException(AssistantError error, string message) : Exception(message)
{
    public AssistantError Error { get; } = error;
}
