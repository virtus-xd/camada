using System.Xml.Linq;

namespace CamAda.Core.Tests.Text;

/// <summary>
/// Adadaki Ada satırına (≈560 px, 13 pt) sığan İngilizce bildirim ve hata metinleri. Türkçeleri bugünkü haliyle kalır;
/// İngilizce yeni yazıldığı için baştan sığacak uzunlukta tutulur.
/// </summary>
public class IslandTextLengthTests
{
    private const int MaxIslandLine = 60;

    [Fact]
    public void English_ada_notices_and_errors_fit_the_island_row()
    {
        var path = Path.Combine(RepoRoot.Find(), "src", "CamAda.Core", "Text", "UiStrings.resx");
        var tooLong = XDocument.Load(path).Root!.Elements("data")
            .Select(d => (Key: (string)d.Attribute("name")!, Value: (string?)d.Element("value") ?? ""))
            .Where(e => e.Key.StartsWith("Ada_Notice_", StringComparison.Ordinal) || e.Key.StartsWith("Ada_Error_", StringComparison.Ordinal))
            .Where(e => e.Value.Length > MaxIslandLine)
            .Select(e => $"{e.Key} ({e.Value.Length}): {e.Value}")
            .ToList();
        Assert.True(tooLong.Count == 0, "Ada satırına sığmayan İngilizce metin:\n" + string.Join("\n", tooLong));
    }
}
