using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CamAda.Core.Text;

namespace CamAda.Core.Tests.Text;

/// <summary>Her temel (İngilizce) metnin Türkçe karşılığı var, yer tutucuları aynı ve hiçbiri boş değil.</summary>
public partial class ResourceParityTests
{
    public static TheoryData<string> Pairs => new()
    {
        "src/CamAda.Core/Text/CoreStrings",
        "src/CamAda/Text/UiStrings",
    };

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Turkish_file_has_exactly_the_same_keys(string baseName)
    {
        var en = Load(baseName + ".resx");
        var tr = Load(baseName + ".tr.resx");
        Assert.Empty(en.Keys.Except(tr.Keys).Order());
        Assert.Empty(tr.Keys.Except(en.Keys).Order());
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Placeholders_match_and_no_value_is_empty(string baseName)
    {
        var en = Load(baseName + ".resx");
        var tr = Load(baseName + ".tr.resx");
        var problems = new List<string>();
        foreach (var (key, enValue) in en)
        {
            if (!tr.TryGetValue(key, out var trValue))
            {
                continue; // anahtar farkını öbür test yakalar
            }

            if (string.IsNullOrWhiteSpace(enValue) || string.IsNullOrWhiteSpace(trValue))
            {
                problems.Add($"{key}: boş değer");
            }

            if (!Placeholders(enValue).SetEquals(Placeholders(trValue)))
            {
                problems.Add($"{key}: yer tutucular farklı ('{enValue}' / '{trValue}')");
            }
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void Turkish_satellite_assembly_is_loaded()
    {
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        var en = CultureInfo.GetCultureInfo("en-US");
        Assert.Equal("Otomatik (Windows)", CoreStrings.ResourceManager.GetString(nameof(CoreStrings.Language_Automatic), tr));
        Assert.Equal("Automatic (Windows)", CoreStrings.ResourceManager.GetString(nameof(CoreStrings.Language_Automatic), en));
        Assert.Equal("Dil / Language", CoreStrings.ResourceManager.GetString(nameof(CoreStrings.Language_MenuTitle), tr));
    }

    private static Dictionary<string, string> Load(string relativePath)
    {
        var path = Path.Combine(RepoRoot.Find(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");
    }

    private static HashSet<string> Placeholders(string value) =>
        PlaceholderPattern().Matches(value).Select(m => m.Value).ToHashSet();

    [GeneratedRegex(@"\{\d+(:[^}]*)?\}")]
    private static partial Regex PlaceholderPattern();
}
