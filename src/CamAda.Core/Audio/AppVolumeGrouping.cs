using System.Globalization;

namespace CamAda.Core.Audio;

/// <summary>Varsayılan çıkıştaki bir ses oturumu (Core Audio'dan okunan ham örnek).</summary>
/// <param name="ExeName">Sürecin exe adı, ör. "chrome.exe".</param>
/// <param name="DisplayName">Oturumun ya da exe'nin görünen adı; boş olabilir.</param>
/// <param name="Active">Şu an ses çıkarıyor mu.</param>
/// <param name="IsSystemSounds">Windows sistem sesleri oturumu.</param>
public sealed record SessionSample(
    int ProcessId, string ExeName, string DisplayName, float Volume, bool Muted, bool Active, bool IsSystemSounds);

/// <summary>Ses panelinde bir uygulama satırı: aynı uygulamanın tüm oturumları.</summary>
/// <param name="Key">Satırın kimliği: exe adı (küçük harf).</param>
public sealed record AppVolumeRow(string Key, string Name, IReadOnlyList<int> ProcessIds, float Volume, bool Muted);

/// <summary>
/// "Uygulama bazında ses (o an ses çıkaran uygulamalar)" (spec §7.4): yalnız etkin oturumlar, sistem sesleri hariç; aynı
/// exe'nin oturumları (ör. Chrome'un süreçleri) tek satır; satırın düzeyi en yüksek oturumun, hepsi sessizse sessiz.
/// </summary>
public static class AppVolumeGrouping
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static IReadOnlyList<AppVolumeRow> Group(IEnumerable<SessionSample> sessions) =>
        sessions
            .Where(s => s.Active && !s.IsSystemSounds && !string.IsNullOrWhiteSpace(s.ExeName))
            .GroupBy(s => s.ExeName.ToLowerInvariant())
            .Select(g => new AppVolumeRow(
                g.Key,
                g.Select(s => s.DisplayName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                    ?? Path.GetFileNameWithoutExtension(g.Key),
                g.Select(s => s.ProcessId).Distinct().ToArray(),
                g.Max(s => s.Volume),
                g.All(s => s.Muted)))
            .OrderBy(r => r.Name, StringComparer.Create(Turkish, ignoreCase: true))
            .ToArray();
}
