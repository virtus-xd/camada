using System.Globalization;

namespace CamAda.Core.Text;

/// <summary>Sağ tık menüsündeki "Dil / Language" alt menüsü: seçenekler, işaret ve yeniden başlatma kararı.</summary>
public static class LanguageMenu
{
    /// <summary>Ayar değeri (null = Windows'un diline uy) ve menüdeki adı.</summary>
    public sealed record Option(string? Setting, string LabelKey)
    {
        public string Label(CultureInfo ui) => CoreText.Get(LabelKey, ui);
    }

    public static IReadOnlyList<Option> Options { get; } =
    [
        new(null, nameof(CoreStrings.Language_Automatic)),
        new("tr", nameof(CoreStrings.Language_Turkish)),
        new("en", nameof(CoreStrings.Language_English)),
    ];

    /// <summary>Başlık her dilde iki dilli: yanlış dili seçen geri dönebilsin.</summary>
    public static string Title(CultureInfo ui) => CoreText.Get(nameof(CoreStrings.Language_MenuTitle), ui);

    public static bool IsChecked(string? current, string? option) => current == option;

    public static bool NeedsRestart(string? current, string? chosen) => current != chosen;
}
