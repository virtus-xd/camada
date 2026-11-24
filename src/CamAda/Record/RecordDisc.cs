using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace CamAda.Record;

/// <summary>
/// Plağın tıklanan/odaklanan gövdesi. Ekran okuyuculara "Plak, düğme" olarak görünür ve basılabilir (çal/duraklat); tıklama,
/// Enter/Boşluk ve erişilebilirlik "bas" komutu aynı <see cref="Invoked"/> olayına çıkar.
/// </summary>
public sealed class RecordDisc : Grid
{
    public event EventHandler? Invoked;

    public void Invoke() => Invoked?.Invoke(this, EventArgs.Empty);

    protected override AutomationPeer OnCreateAutomationPeer() => new DiscPeer(this);

    private sealed class DiscPeer(RecordDisc owner) : FrameworkElementAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string GetClassNameCore() => nameof(RecordDisc);

        public override object GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);

        void IInvokeProvider.Invoke() => owner.Invoke();
    }
}
