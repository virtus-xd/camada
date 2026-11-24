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
}
