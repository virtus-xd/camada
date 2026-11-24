using CamAda.Core.Theme;

namespace CamAda.Core.Tests.Theme;

public class ThemeRuleTests
{
    [Fact]
    public void Dark_when_apps_use_light_theme_is_zero()
    {
        Assert.True(ThemeRule.IsDark(0));
        Assert.False(ThemeRule.IsDark(1));
        Assert.False(ThemeRule.IsDark(null)); // değer yoksa Windows açık temadadır
    }

    [Fact]
    public void Registry_value_for_mode()
    {
        Assert.Equal(0, ThemeRule.RegistryValue(dark: true));
        Assert.Equal(1, ThemeRule.RegistryValue(dark: false));
    }
}
