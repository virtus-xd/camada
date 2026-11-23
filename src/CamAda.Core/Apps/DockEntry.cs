namespace CamAda.Core.Apps;

/// <summary>Görev çubuğuna eklenebilecek açık bir pencerenin özeti.</summary>
/// <param name="ProcessName">Exe dosya adı (ör. "chrome.exe"); eşleştirme büyük/küçük harf duyarsız.</param>
/// <param name="HasBadge">Uygulama görev çubuğu rozeti koymuş (ör. Discord okunmamış).</param>
public sealed record RunningWindow(
    nint Handle,
    string ProcessName,
    string Title,
    bool IsActive,
    bool IsFlashing,
    bool IsMinimized,
    string? AppUserModelId,
    bool HasBadge);

/// <summary>Adadaki bir uygulama düğmesi: sabitlenmiş uygulama ya da sabitlenmemiş açık uygulama ve pencereleri.</summary>
public sealed record DockEntry(string Key, PinnedApp? Pinned, IReadOnlyList<RunningWindow> Windows)
{
    public bool IsRunning => Windows.Count > 0;

    public bool NeedsAttention => Windows.Any(w => w.IsFlashing);

    public bool HasBadge => Windows.Any(w => w.HasBadge);
}
