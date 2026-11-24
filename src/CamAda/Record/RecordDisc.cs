using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using CamAda.Core.Record;

namespace CamAda.Record;

/// <summary>
/// Plağın tıklanan/odaklanan gövdesi. Ekran okuyuculara "Plak, düğme" olarak görünür ve basılabilir (çal/duraklat); tıklama,
/// Enter/Boşluk ve erişilebilirlik "bas" komutu aynı <see cref="Invoked"/> olayına çıkar. Fare yalnız diskin dairesinde
/// sayılır: içerik isabet testine katılmaz, dönen kare disk görüntüsünün köşeleri tıklamayı ve kartı tetiklemez.
/// </summary>
public sealed class RecordDisc : Grid
{
    public event EventHandler? Invoked;

    public void Invoke() => Invoked?.Invoke(this, EventArgs.Empty);

    protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        if (visualAdded is UIElement child)
        {
            child.IsHitTestVisible = false;
        }
    }

    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        var p = hitTestParameters.HitPoint;
        return DiscHit.Contains(p.X, p.Y, ActualWidth, ActualHeight) ? new PointHitTestResult(this, p) : null;
    }

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
