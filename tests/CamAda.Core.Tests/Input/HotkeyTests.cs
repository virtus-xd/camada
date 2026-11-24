using CamAda.Core.Input;

namespace CamAda.Core.Tests.Input;

public class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+Shift+M", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x4D)]
    [InlineData(" ctrl + shift + m ", HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x4D)]
    [InlineData("Alt+F13", HotkeyModifiers.Alt, 0x7C)]
    [InlineData("Win+Shift+7", HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x37)]
    public void Parses_modifiers_and_key(string text, HotkeyModifiers modifiers, int virtualKey) =>
        Assert.Equal(new Hotkey(modifiers, virtualKey), Hotkey.TryParse(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Shift")]
    [InlineData("M")]
    [InlineData("Ctrl+Shift+M+K")]
    [InlineData("Ctrl+Foo")]
    [InlineData("Ctrl+Ctrl+M")]
    public void Rejects_invalid(string? text) => Assert.Null(Hotkey.TryParse(text));

    [Fact]
    public void ToString_is_canonical() => Assert.Equal("Ctrl+Shift+M", Hotkey.TryParse("shift+ctrl+m")!.ToString());

    [Fact]
    public void Win_comes_first() => Assert.Equal("Win+Alt+V", Hotkey.TryParse("alt+win+v")!.ToString());
}
