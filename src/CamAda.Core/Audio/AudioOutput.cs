namespace CamAda.Core.Audio;

/// <summary>Etkin bir ses çıkış cihazı (Core Audio uç noktası).</summary>
/// <param name="Id">Uç nokta kimliği (varsayılan yapmak için kullanılır).</param>
/// <param name="Name">Windows'taki görünen adı, ör. "Hoparlör (Realtek(R) Audio)".</param>
/// <param name="IsDefault">Şu an varsayılan çıkış mı.</param>
public sealed record AudioOutput(string Id, string Name, bool IsDefault);
