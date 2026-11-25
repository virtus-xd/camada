using System.IO;
using ManagedShell.Common.Helpers;

namespace CamAda.Assistant;

/// <summary>Ada'nın kapatma ve yeniden başlatma işlemleri (Debug denemesinde sahtesiyle değiştirilir).</summary>
public interface IPowerActions
{
    void Shutdown();

    void Reboot();
}

public sealed class SystemPowerActions : IPowerActions
{
    public void Shutdown() => PowerHelper.Shutdown();

    public void Reboot() => PowerHelper.Reboot();
}

#if DEBUG
/// <summary>Yalnız Debug denemesi için (<c>CAMADA_FAKE_POWER</c>): kapatmak yerine çağrıyı günlüğe bir satır olarak yazar.</summary>
public sealed class RecordingPowerActions(string logPath) : IPowerActions
{
    public void Shutdown() => File.AppendAllText(logPath, "shutdown" + Environment.NewLine);

    public void Reboot() => File.AppendAllText(logPath, "reboot" + Environment.NewLine);
}
#endif

public static class PowerActions
{
    /// <summary>Gerçek güç işlemleri; Debug'da <c>CAMADA_FAKE_POWER</c> verilmişse o dosyaya yazan sahtesi.</summary>
    public static IPowerActions Create()
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("CAMADA_FAKE_POWER") is { Length: > 0 } log)
        {
            return new RecordingPowerActions(log);
        }
#endif
        return new SystemPowerActions();
    }
}
