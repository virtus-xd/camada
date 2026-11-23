namespace CamAda.Core.Startup;

/// <summary>Uygulamanın hangi modda başlatıldığı (komut satırı argümanlarından).</summary>
public abstract record StartupMode
{
    /// <summary>Normal çalışma: şerit ve ada açılır.</summary>
    public sealed record Normal : StartupMode;

    /// <summary>Bekçi süreci: ana süreç kapanınca gerekirse görev çubuğunu geri getirir.</summary>
    public sealed record Watchdog(int ParentProcessId) : StartupMode;

    /// <summary>Kurtarma: çalışan adayı kapatır ve görev çubuğunu geri getirir.</summary>
    public sealed record RestoreTaskbar : StartupMode;

    public static StartupMode Parse(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--restore-taskbar":
                    return new RestoreTaskbar();
                case "--watchdog":
                    // Bozuk bekçi argümanı güvenli tarafta kalır: görev çubuğunu geri getir.
                    return i + 1 < args.Count && int.TryParse(args[i + 1], out var pid) && pid > 0
                        ? new Watchdog(pid)
                        : new RestoreTaskbar();
            }
        }

        return new Normal();
    }
}
