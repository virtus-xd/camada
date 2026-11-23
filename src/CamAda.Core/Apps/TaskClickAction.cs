namespace CamAda.Core.Apps;

public enum ClickKind
{
    /// <summary>Açık penceresi yok: uygulamayı başlat.</summary>
    Launch,

    /// <summary>Tek penceresi önde değil ya da küçültülmüş: öne getir.</summary>
    Activate,

    /// <summary>Tek penceresi zaten önde: küçült.</summary>
    Minimize,

    /// <summary>Birden fazla penceresi var: önizleme panelini aç.</summary>
    ShowWindows,
}

public readonly record struct ClickDecision(ClickKind Kind, RunningWindow? Window);

/// <summary>Adadaki bir uygulama düğmesine tıklanınca ne olacağına karar verir (spec §7.3).</summary>
public static class TaskClickAction
{
    public static ClickDecision Decide(DockEntry entry) => entry.Windows.Count switch
    {
        0 => new ClickDecision(ClickKind.Launch, null),
        1 when entry.Windows[0] is { IsActive: true, IsMinimized: false } => new ClickDecision(ClickKind.Minimize, entry.Windows[0]),
        1 => new ClickDecision(ClickKind.Activate, entry.Windows[0]),
        _ => new ClickDecision(ClickKind.ShowWindows, null),
    };
}
