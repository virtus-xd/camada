using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class AdaInstructionTests
{
    private static readonly DateTime Now = new(2026, 11, 25, 18, 30, 0);

    [Fact]
    public void Instruction_has_date_and_principles()
    {
        var text = AdaInstruction.Build(Now, []);
        Assert.Contains("25 Kasım 2026 Çarşamba", text);
        Assert.Contains("yapamıyorum", text);
        Assert.Contains("emin misin", text);
        Assert.Contains("Ekranı göremezsin", text);
        Assert.Contains("hatirla", text);
        Assert.Contains("guncel_bilgi", text);
    }

    [Fact]
    public void Memory_is_listed()
    {
        var at = new DateTimeOffset(Now);
        Assert.Contains("- kulaklık: Buds2", AdaInstruction.Build(Now, [new MemoryItem("kulaklık", "Buds2", at)]));
        Assert.Contains("(henüz yok)", AdaInstruction.Build(Now, []));
    }
}
