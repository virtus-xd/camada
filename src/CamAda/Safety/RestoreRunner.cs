using System.Diagnostics;
using CamAda.Core.Safety;
using ManagedShell.AppBar;
using static ManagedShell.Interop.NativeMethods;

namespace CamAda.Safety;

/// <summary>Kurtarma modu: çalışan adayı kapatır ve Windows görev çubuğunu geri getirir.</summary>
public static class RestoreRunner
{
    public static void Run(TaskbarSafetyStore store)
    {
        var self = Environment.ProcessId;
        foreach (var process in Process.GetProcessesByName("CamAda"))
        {
            using (process)
            {
                if (process.Id == self)
                {
                    continue;
                }

                try
                {
                    process.Kill();
                    process.WaitForExit(2000);
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Süreç bu arada kapanmış ya da erişilemiyor; görev çubuğunu yine de geri getiriyoruz.
                }
            }
        }

        RestoreTaskbar(store);
        Interop.Native.BroadcastTaskbarCreated(); // öldürülen ada tepsiyi barındırıyordu: simgeler Explorer'a dönsün
    }

    /// <summary>Görev çubuğunu kaydedilmiş özgün otomatik gizleme durumuyla görünür yapar. UI iş parçacığında çağrılmalı.</summary>
    public static void RestoreTaskbar(TaskbarSafetyStore store)
    {
        var original = store.Load()?.OriginalTaskbarState ?? TaskbarSafetyStore.DefaultTaskbarState;
        var explorer = new ExplorerHelper();
        explorer.SetTaskbarState((ABState)original);
        explorer.SetTaskbarVisibility((int)SetWindowPosFlags.SWP_SHOWWINDOW);
        store.Save(new TaskbarSafetyState(HiddenByUs: false, CleanShutdown: true, original));
    }
}
