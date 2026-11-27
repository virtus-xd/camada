namespace CamAda.Core.Search;

public enum SearchKind
{
    App,
    Setting,
    File,
    Web,
}

/// <summary>Başlat panelinde aranabilen bir öğe.</summary>
/// <param name="Target">Exe/.lnk yolu, "shell:AppsFolder\&lt;AUMID&gt;", "ms-settings:..." ya da URL.</param>
/// <param name="Aliases">Görünmeyen ek arama adları (ör. ayar sayfasının öbür dildeki adı).</param>
public sealed record SearchItem(string Name, SearchKind Kind, string Target, IReadOnlyList<string>? Aliases = null);
