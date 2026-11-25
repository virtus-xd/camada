using CamAda.Core.Clipboard;

namespace CamAda.Core.Tests.Clipboard;

public class ClipClassifierTests
{
    [Theory]
    [InlineData("https://github.com/virtus-xd", ClipKind.Link)]
    [InlineData("  http://a.b/c?d=1  ", ClipKind.Link)]
    [InlineData("bkz. https://x.com", ClipKind.Text)]
    [InlineData("https://a.com\nhttps://b.com", ClipKind.Text)]
    [InlineData("public void Foo()\n{\n    Bar();\n}", ClipKind.Code)]
    [InlineData("dotnet test CamAda.slnx --nologo", ClipKind.Code)]
    [InlineData("git status", ClipKind.Code)]
    [InlineData("x => x * 2;", ClipKind.Code)]
    [InlineData("toplantı yarın 14:00'te", ClipKind.Text)]
    [InlineData("Merhaba,\nyarın görüşürüz.\nSevgiler", ClipKind.Text)]
    public void Classifies_text(string text, ClipKind expected) => Assert.Equal(expected, ClipClassifier.Classify(text));

    [Theory]
    [InlineData("if (x) { y(); }", ClipKind.Code)]
    [InlineData("küme {a, b} ve {c}", ClipKind.Code)]
    [InlineData("kapanmayan { parantez", ClipKind.Text)]
    [InlineData("ters } sıra {", ClipKind.Text)]
    public void Braces_on_one_line_mean_code_only_when_closed(string text, ClipKind expected) =>
        Assert.Equal(expected, ClipClassifier.Classify(text));

    [Fact]
    public void Long_single_line_with_unclosed_braces_is_classified_quickly()
    {
        var text = new string('{', 400_000);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var kind = ClipClassifier.Classify(text);
        watch.Stop();

        Assert.Equal(ClipKind.Text, kind);
        Assert.True(watch.ElapsedMilliseconds < 500, $"{watch.ElapsedMilliseconds} ms sürdü");
    }

    [Fact]
    public void Very_long_text_is_judged_by_its_beginning()
    {
        // 16 KB'tan sonrası bakılmaz: başı düz metin olan dev metin, sonunda kod satırları olsa da metindir.
        var prose = string.Concat(Enumerable.Repeat("Merhaba dünya, bu bir paragraf.\n", 1000));
        var code = string.Concat(Enumerable.Repeat("    Foo();\n", 5000));
        Assert.Equal(ClipKind.Text, ClipClassifier.Classify(prose + code));
    }
}
