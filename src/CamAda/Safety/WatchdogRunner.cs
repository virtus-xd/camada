using System.Diagnostics;
using CamAda.Core.Safety;

namespace CamAda.Safety;

/// <summary>Bekçi modu: ana süreç kapanana kadar bekler; temiz çıkmadıysa görev çubuğunu geri getirir.</summary>
public static class WatchdogRunner
{
    public static async Task RunAsync(int parentPid, TaskbarSafetyStore store)
    {
        try
        {
            using var parent = Process.GetProcessById(parentPid);
            await parent.WaitForExitAsync();
        }
        catch (ArgumentException)
        {
            // Ana süreç zaten kapanmış.
        }

        if (WatchdogDecision.ShouldRestore(store.Load()))
        {
            RestoreRunner.RestoreTaskbar(store);
        }

        // Ana süreç tepsiyi barındırıyordu: onun penceresi gidince uygulamalar simgelerini Explorer'a yeniden eklesin.
        // Temiz çıkışta da zararsızdır (uygulamalar zaten kayıtlı simgeyi günceller).
        Interop.Native.BroadcastTaskbarCreated();
    }
}
