using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class NameMatchTests
{
    private static readonly string[] Devices = ["Hoparlör (Realtek(R) Audio)", "Kulaklıklar (Buds2)"];

    [Fact]
    public void Exact_beats_prefix() =>
        Assert.Equal(new NameMatchResult.Found(1), NameMatch.Best("Steam", ["Steam VR", "Steam"]));

    [Fact]
    public void Word_start_finds_device() =>
        Assert.Equal(new NameMatchResult.Found(1), NameMatch.Best("Buds", Devices));

    [Fact]
    public void Prefix_finds_plural() =>
        Assert.Equal(new NameMatchResult.Found(1), NameMatch.Best("kulaklık", Devices));

    [Fact]
    public void Turkish_case()
    {
        Assert.Equal(new NameMatchResult.Found(0), NameMatch.Best("DİSCORD", ["Discord"]));
        Assert.IsType<NameMatchResult.NotFound>(NameMatch.Best("ıtunes", ["iTunes"]));
    }

    [Fact]
    public void Ambiguous_when_many_match()
    {
        string[] sonar =
        [
            "SteelSeries Sonar - Gaming", "SteelSeries Sonar - Chat", "SteelSeries Sonar - Media", "SteelSeries Sonar - Microphone",
        ];
        Assert.Equal([0, 1, 2, 3], Assert.IsType<NameMatchResult.Ambiguous>(NameMatch.Best("Sonar", sonar)).Indexes);
    }

    [Fact]
    public void Same_name_twice_is_not_ambiguous() =>
        Assert.Equal(new NameMatchResult.Found(0), NameMatch.Best("Discord", ["Discord", "Discord"]));

    [Fact]
    public void Not_found() => Assert.IsType<NameMatchResult.NotFound>(NameMatch.Best("Photoshop", Devices));

    [Fact]
    public void All_words_match_across_separators() =>
        Assert.Equal(new NameMatchResult.Found(0),
            NameMatch.Best("sonar media", ["SteelSeries Sonar - Media", "SteelSeries Sonar - Microphone"]));

    [Fact]
    public void All_words_stage_needs_every_word() =>
        Assert.IsType<NameMatchResult.NotFound>(NameMatch.Best("sonar kulaklık", ["SteelSeries Sonar - Media"]));
}
