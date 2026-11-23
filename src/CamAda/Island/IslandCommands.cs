namespace CamAda.Island;

/// <summary>Adanın sağ tık menüsünün uygulamaya bağlandığı komutlar.</summary>
/// <param name="IsTaskbarHidden">Windows görev çubuğu şu an gizli mi.</param>
/// <param name="SetTaskbarHidden">Ayarı kaydeder ve görev çubuğunu gizler ya da geri getirir.</param>
/// <param name="Exit">Görev çubuğunu geri getirip uygulamadan çıkar.</param>
public sealed record IslandCommands(
    Func<bool> IsTaskbarHidden,
    Action<bool> SetTaskbarHidden,
    Action Exit);
