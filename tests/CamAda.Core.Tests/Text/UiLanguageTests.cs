using System.Globalization;
using CamAda.Core.Settings;
using CamAda.Core.Text;

namespace CamAda.Core.Tests.Text;

public class UiLanguageTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("camada-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData(null, "tr-TR", "tr-TR")]
    [InlineData(null, "en-US", "en-US")]
    [InlineData(null, "de-DE", "en-US")]
    [InlineData("tr", "en-US", "tr-TR")]
    [InlineData("en", "tr-TR", "en-US")]
    [InlineData("xx", "tr-TR", "tr-TR")]
    [InlineData("", "en-US", "en-US")]
    public void Resolve_picks_setting_then_windows_language(string? setting, string windows, string expected) =>
        Assert.Equal(expected, UiLanguage.Resolve(setting, CultureInfo.GetCultureInfo(windows)).Name);

    [Fact]
    public void IsTurkish_checks_the_language_not_the_region()
    {
        Assert.True(UiLanguage.IsTurkish(CultureInfo.GetCultureInfo("tr-TR")));
        Assert.True(UiLanguage.IsTurkish(CultureInfo.GetCultureInfo("tr")));
        Assert.False(UiLanguage.IsTurkish(CultureInfo.GetCultureInfo("en-GB")));
    }

    [Theory]
    [InlineData(1, "app")]
    [InlineData(0, "apps")]
    [InlineData(2, "apps")]
    public void Plural_picks_one_only_for_exactly_one(int n, string expected) =>
        Assert.Equal(expected, Plural.Pick(n, "app", "apps"));

    [Theory]
    [InlineData("tr", "tr")]
    [InlineData("en", "en")]
    [InlineData("fr", null)]
    [InlineData("  ", null)]
    public void Settings_keep_only_known_languages(string stored, string? expected)
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), $$"""{ "SettingsVersion": 2, "Language": "{{stored}}" }""");
        Assert.Equal(expected, new SettingsStore(_dir).Load().Language);
    }

    [Fact]
    public void Settings_without_language_follow_windows()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "SettingsVersion": 2 }""");
        Assert.Null(new SettingsStore(_dir).Load().Language);
    }
}
