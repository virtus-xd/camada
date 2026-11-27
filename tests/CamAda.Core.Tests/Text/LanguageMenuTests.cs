using CamAda.Core.Text;
using static CamAda.Core.Tests.Text.Cultures;

namespace CamAda.Core.Tests.Text;

public class LanguageMenuTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData(null, "tr", false)]
    [InlineData("tr", "tr", true)]
    [InlineData("tr", null, false)]
    [InlineData("en", "en", true)]
    [InlineData("en", "tr", false)]
    public void Only_the_current_choice_is_checked(string? current, string? option, bool expected) =>
        Assert.Equal(expected, LanguageMenu.IsChecked(current, option));

    [Theory]
    [InlineData(null, null, false)]
    [InlineData(null, "en", true)]
    [InlineData("tr", "tr", false)]
    [InlineData("tr", "en", true)]
    [InlineData("en", null, true)]
    public void Restart_only_when_the_choice_changes(string? current, string? chosen, bool expected) =>
        Assert.Equal(expected, LanguageMenu.NeedsRestart(current, chosen));

    [Fact]
    public void Options_are_automatic_turkish_english_with_labels_in_both_languages()
    {
        Assert.Equal([null, "tr", "en"], LanguageMenu.Options.Select(o => o.Setting));
        Assert.Equal(["Automatic (Windows)", "Türkçe", "English"], LanguageMenu.Options.Select(o => o.Label(En)));
        Assert.Equal(["Otomatik (Windows)", "Türkçe", "English"], LanguageMenu.Options.Select(o => o.Label(Tr)));
        Assert.Equal("Dil / Language", LanguageMenu.Title(En));
        Assert.Equal("Dil / Language", LanguageMenu.Title(Tr));
    }
}
