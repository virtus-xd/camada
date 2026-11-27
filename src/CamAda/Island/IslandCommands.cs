using CamAda.Core.Settings;

namespace CamAda.Island;

/// <summary>Adanın sağ tık menüsünün uygulamaya bağlandığı komutlar.</summary>
/// <param name="IsTaskbarHidden">Windows görev çubuğu şu an gizli mi.</param>
/// <param name="SetTaskbarHidden">Ayarı kaydeder ve görev çubuğunu gizler ya da geri getirir.</param>
/// <param name="Exit">Görev çubuğunu geri getirip uygulamadan çıkar.</param>
/// <param name="GetGlassMode">Kayıtlı cam görünümü.</param>
/// <param name="SetGlassMode">Cam görünümünü ayarlara kaydeder.</param>
/// <param name="IsStartWithWindows">Oturum açılınca başlama Windows'ta şu an açık mı.</param>
/// <param name="SetStartWithWindows">
/// Ayarı kaydeder; paketliyse başlangıç görevini, değilse Run anahtarını uygular. Kullanıcı Windows'ta kapatmışsa
/// başlangıç uygulamaları ayarını açar.
/// </param>
/// <param name="IsRecordHidden">Masaüstü plağı şu an gizli mi.</param>
/// <param name="SetRecordHidden">Ayarı kaydeder ve plağı kapatır ya da yeniden kurar.</param>
/// <param name="IsAssistantEnabled">Sesli asistan Ada açık mı.</param>
/// <param name="SetAssistantEnabled">Ada'yı açar ya da kapatır (anahtar yoksa Ada ayarlarını açar).</param>
/// <param name="OpenAssistantPanel">Ada ayarları panelini açar.</param>
public sealed record IslandCommands(
    Func<bool> IsTaskbarHidden,
    Action<bool> SetTaskbarHidden,
    Action Exit,
    Func<GlassMode> GetGlassMode,
    Action<GlassMode> SetGlassMode,
    Func<bool> IsStartWithWindows,
    Action<bool> SetStartWithWindows,
    Func<bool> IsRecordHidden,
    Action<bool> SetRecordHidden,
    Func<bool> IsAssistantEnabled,
    Action<bool> SetAssistantEnabled,
    Action OpenAssistantPanel);
