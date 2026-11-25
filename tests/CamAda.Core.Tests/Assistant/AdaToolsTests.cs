using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class AdaToolsTests
{
    private static IReadOnlyList<JsonObject> Declarations() => AdaTools.Declarations().Select(d => d!.AsObject()).ToList();

    [Fact]
    public void Names_are_unique_snake_case()
    {
        var names = Declarations().Select(d => (string)d["name"]!).ToList();
        Assert.Equal(28, names.Count);
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, n => Assert.Matches(new Regex("^[a-z_]+$"), n));
        Assert.Contains(AdaTools.WhatsAppMesaj, names);
        Assert.Contains(AdaTools.GuncelBilgi, names);
        Assert.Contains(AdaTools.NotYaz, names);
    }

    [Fact]
    public void Every_declaration_has_description_and_required()
    {
        foreach (var d in Declarations())
        {
            Assert.False(string.IsNullOrWhiteSpace((string?)d["description"]), (string?)d["name"]);
            if (d["parameters"] is JsonObject parameters)
            {
                Assert.Equal("OBJECT", (string?)parameters["type"]);
                var properties = parameters["properties"]!.AsObject().Select(p => p.Key).ToList();
                var required = parameters["required"]!.AsArray().Select(r => (string)r!).ToList();
                Assert.NotEmpty(properties);
                Assert.All(required, r => Assert.Contains(r, properties));
                Assert.All(properties, p => Assert.False(string.IsNullOrWhiteSpace((string?)parameters["properties"]![p]!["type"])));
            }
        }
    }

    [Fact]
    public void Lookup_and_browser_search_are_scoped()
    {
        string Description(string name) => (string)Declarations().Single(d => (string?)d["name"] == name)["description"]!;
        Assert.Contains("YALNIZ zamanla değişen", Description(AdaTools.GuncelBilgi));
        Assert.Contains("ASLA", Description(AdaTools.TarayicidaGoogleAc));
    }
}
