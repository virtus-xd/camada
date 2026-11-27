using CamAda.Core.Search;
using static CamAda.Core.Tests.Text.Cultures;

namespace CamAda.Core.Tests.Search;

public class SearchRankerTests
{
    private static readonly IReadOnlyList<SearchItem> Items =
    [
        new("Steam", SearchKind.App, @"C:\Steam\steam.exe"),
        new("Visual Studio Code", SearchKind.App, @"C:\Code\code.exe"),
        new("Işık Ayarlayıcı", SearchKind.App, @"C:\isik.exe"),
        new("indirilenler", SearchKind.File, @"C:\Users\u\Downloads"),
        new("Notlar", SearchKind.App, @"C:\notlar.exe"),
        .. SettingsPages.For(Tr),
    ];

    [Fact]
    public void Prefix_of_the_name_ranks_first()
    {
        var results = SearchRanker.Rank("ste", Items, Tr);
        Assert.Equal("Steam", results[0].Name);
    }

    [Fact]
    public void Upper_case_query_matches_and_name_start_beats_word_start()
    {
        var names = SearchRanker.Rank("AYAR", Items, Tr).Select(r => r.Name).ToList();
        Assert.Contains("Ayarlar", names);
        Assert.Contains("Ses ayarları", names);
        Assert.True(names.IndexOf("Ayarlar") < names.IndexOf("Ses ayarları"));
    }

    [Fact]
    public void Dotless_i_query_finds_capital_I_name_in_turkish()
    {
        var results = SearchRanker.Rank("ıs", Items, Tr);
        Assert.Equal("Işık Ayarlayıcı", results[0].Name);
    }

    [Fact]
    public void Query_typed_without_turkish_letters_still_finds_turkish_name()
    {
        var results = SearchRanker.Rank("isik", Items, Tr);
        Assert.Equal("Işık Ayarlayıcı", results[0].Name);
    }

    [Fact]
    public void Dotted_capital_I_query_finds_lower_case_i_name_in_turkish()
    {
        var results = SearchRanker.Rank("İndir", Items, Tr);
        Assert.Equal("indirilenler", results[0].Name);
    }

    [Fact]
    public void No_match_leaves_only_the_web_row()
    {
        var results = SearchRanker.Rank("zzz", Items, Tr);
        var web = Assert.Single(results);
        Assert.Equal(SearchKind.Web, web.Kind);
        Assert.Equal("Web'de ara: “zzz”", web.Name);
    }

    [Fact]
    public void Empty_query_returns_nothing()
    {
        Assert.Empty(SearchRanker.Rank("", Items, Tr));
        Assert.Empty(SearchRanker.Rank("   ", Items, Tr));
    }

    [Fact]
    public void On_equal_score_app_comes_before_setting()
    {
        IReadOnlyList<SearchItem> items =
        [
            new("Bluetooth ayarı", SearchKind.Setting, "ms-settings:bluetooth"),
            new("Bluetooth aracı", SearchKind.App, @"C:\bt.exe"),
        ];
        var results = SearchRanker.Rank("blue", items, Tr);
        Assert.Equal(SearchKind.App, results[0].Kind);
        Assert.Equal(SearchKind.Setting, results[1].Kind);
    }

    [Fact]
    public void Limit_is_respected_and_web_row_is_appended_after_it()
    {
        var items = Enumerable.Range(0, 20).Select(i => new SearchItem($"Uygulama {i}", SearchKind.App, $@"C:\u{i}.exe")).ToList();
        var results = SearchRanker.Rank("uyg", items, Tr, limit: 4);
        Assert.Equal(5, results.Count);
        Assert.All(results.Take(4), r => Assert.Equal(SearchKind.App, r.Kind));
        Assert.Equal(SearchKind.Web, results[4].Kind);
    }

    [Fact]
    public void Web_target_escapes_the_query()
    {
        var web = SearchRanker.Rank("ses ayar", Items, Tr).Last();
        Assert.Equal("https://www.google.com/search?q=ses%20ayar", web.Target);
    }

    [Fact]
    public void Settings_pages_use_ms_settings_targets()
    {
        Assert.Contains(SettingsPages.For(Tr), s => s is { Name: "Ses ayarları", Target: "ms-settings:sound", Kind: SearchKind.Setting });
        Assert.Contains(SettingsPages.For(Tr), s => s is { Name: "Ayarlar", Target: "ms-settings:" });
        Assert.Equal(14, SettingsPages.For(Tr).Count);
    }
}
