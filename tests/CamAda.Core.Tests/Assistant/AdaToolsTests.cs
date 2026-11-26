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
        Assert.Equal(34, names.Count);
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, n => Assert.Matches(new Regex("^[a-z_]+$"), n));
        Assert.Contains(AdaTools.WhatsAppMesaj, names);
        Assert.Contains(AdaTools.GuncelBilgi, names);
        Assert.Contains(AdaTools.NotYaz, names);
        Assert.Contains(AdaTools.MisafirKipi, names);
        Assert.Contains(AdaTools.HatirlaticiKur, names);
        Assert.Contains(AdaTools.HatirlaticilariListele, names);
        Assert.Contains(AdaTools.HatirlaticiIptal, names);
        Assert.Contains(AdaTools.HatirlaticiErtele, names);
        Assert.Contains(AdaTools.HavaDurumu, names);
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

    [Fact]
    public void Screenshot_is_taken_at_once_not_through_the_tool()
    {
        var description = (string)Declarations().Single(d => (string?)d["name"] == AdaTools.EkranGoruntusu)["description"]!;
        Assert.Contains("hemen", description);
        Assert.Contains("panoya", description);
        Assert.DoesNotContain("aracını açar", description);
    }

    [Fact]
    public void Weather_goes_to_its_own_tool_not_the_search()
    {
        string Description(string name) => (string)Declarations().Single(d => (string?)d["name"] == name)["description"]!;

        Assert.DoesNotContain("hava", Description(AdaTools.GuncelBilgi));
        Assert.Contains("hava", Description(AdaTools.HavaDurumu));
        var reminder = Declarations().Single(d => (string?)d["name"] == AdaTools.HatirlaticiKur)["parameters"]!["properties"]!.AsObject();
        Assert.Equal(["metin", "dakika", "saat", "tarih"], reminder.Select(p => p.Key));
        Assert.Equal("INTEGER", (string?)reminder["dakika"]!["type"]);
    }

    [Fact]
    public void Guest_mode_switch_mentions_crowd_and_name()
    {
        var declaration = Declarations().Single(d => (string?)d["name"] == AdaTools.MisafirKipi);
        var description = (string)declaration["description"]!;
        Assert.Contains("kalabalık", description);
        Assert.Contains("'Ada'", description);
        Assert.Equal("BOOLEAN", (string?)declaration["parameters"]!["properties"]!["acik"]!["type"]);
    }
}
