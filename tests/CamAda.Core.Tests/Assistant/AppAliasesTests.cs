using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class AppAliasesTests
{
    [Fact]
    public void English_store_name_gets_turkish_alias() => Assert.Contains("Not Defteri", AppAliases.For("Notepad"));

    [Fact]
    public void Lookup_ignores_case() => Assert.Contains("Hesap Makinesi", AppAliases.For("calculator"));

    [Fact]
    public void Unknown_name_has_no_alias() => Assert.Empty(AppAliases.For("Discord"));
}
