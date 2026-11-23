using ManagedShell;

namespace CamAda.Shell;

/// <summary>ManagedShell'in yaşam döngüsü. Plan 1'de tepsi servisi kapalı, görev (pencere) servisi açık.</summary>
public sealed class ShellHost : IDisposable
{
    private bool _disposed;

    public ShellHost()
    {
        var config = ShellManager.DefaultShellConfig;
        config.EnableTrayService = false;
        config.AutoStartTrayService = false;
        config.EnableTasksService = true; // FullScreenHelper tam ekranı algılamak için TasksService ister.
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
