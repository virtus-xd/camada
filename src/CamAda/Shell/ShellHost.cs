using ManagedShell;

namespace CamAda.Shell;

/// <summary>ManagedShell'in yaşam döngüsü: görev (pencere) ve bildirim alanı (tepsi) servisleri.</summary>
public sealed class ShellHost : IDisposable
{
    private bool _disposed;

    /// <param name="hostTray">
    /// Bildirim alanını ada barındırsın mı. Yalnız Windows görev çubuğu gizliyken açılır: barındırırken Explorer'ın
    /// tepsisi yeni simgeleri almaz, görünür görev çubuğu eksik simgeler gösterirdi.
    /// </param>
    public ShellHost(bool hostTray)
    {
        var config = ShellManager.DefaultShellConfig;
        config.EnableTrayService = hostTray;
        config.AutoStartTrayService = hostTray;
        config.PinnedNotifyIcons = []; // hiçbir simge sabit değil: hepsi "Arka planda çalışanlar" panelinde
        config.EnableTasksService = true; // FullScreenHelper tam ekranı algılamak için TasksService ister.
        config.TaskIconSize = ManagedShell.Common.Enums.IconSize.Large; // adadaki 26 DIP simgeler %125'te keskin kalsın
        Manager = new ShellManager(config);
    }

    public ShellManager Manager { get; }

    /// <summary>AppBar'lara kapanma izni verir ve servisleri kapatır.</summary>
    public void Shutdown()
    {
        if (_disposed)
        {
            return;
        }

        Manager.AppBarManager.SignalGracefulShutdown();
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Manager.Dispose();
    }
}
