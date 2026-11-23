namespace CamAda.Core.Safety;

public static class WatchdogDecision
{
    /// <summary>Görev çubuğunu biz gizlediysek ve çıkış temiz değilse geri getirilmeli.</summary>
    public static bool ShouldRestore(TaskbarSafetyState? state) =>
        state is { HiddenByUs: true, CleanShutdown: false };
}
