using System.IO;
using CamAda.Core.Startup;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace CamAda.Startup;

/// <summary>
/// "Windows açılınca başlat": paketliyken manifestteki başlangıç görevi, paketsizken (geliştirme kurulumu)
/// HKCU\...\Run değeri. Paketliyken Run anahtarına hiç yazılmaz (paket içinde sanallaşır ve etkisiz kalır).
/// </summary>
internal sealed class AutostartService(bool packaged, Action<Exception> log)
{
    public const string TaskId = "CamAdaStartup";

    /// <summary>Windows'un şu an uyguladığı durum (menüdeki işaret bunu gösterir).</summary>
    public bool IsOn { get; private set; }

    /// <summary>İsteği uygular. Kullanıcı ya da kurum ilkesi engelliyorsa engelleyen eylemi döner, durumu değiştirmez.</summary>
    public async Task<StartupTaskAction> ApplyAsync(bool wanted)
    {
        if (!packaged)
        {
            if (TryWriteRunValue(wanted))
            {
                IsOn = wanted;
            }

            return StartupTaskAction.None;
        }

        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            var state = (StartupTaskStatus)task.State;
            var action = StartupTaskDecision.Decide(wanted, state);
            switch (action)
            {
                case StartupTaskAction.RequestEnable:
                    state = (StartupTaskStatus)await task.RequestEnableAsync(); // dönen durum kesin; State özelliği bayat kalabilir
                    break;
                case StartupTaskAction.Disable:
                    task.Disable();
                    state = StartupTaskStatus.Disabled;
                    break;
            }

            IsOn = StartupTaskDecision.IsOn(state);
            return action;
        }
        catch (Exception ex)
        {
            // Menüden de çağrılır (async void): Windows'un beklenmedik bir hatası kabuğu düşürmesin.
            log(ex);
            return StartupTaskAction.None;
        }
    }

    /// <summary>
    /// Çalışan exe'yi yazar ya da değeri siler. Her açılışta yeniden yazılır; uygulama taşınırsa (ör. kurulum betiğiyle)
    /// Run değeri yeni yeri gösterir.
    /// </summary>
    private bool TryWriteRunValue(bool enabled)
    {
        try
        {
            using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (enabled && Environment.ProcessPath is { } exe)
            {
                run.SetValue(AutostartCommand.RunValueName, AutostartCommand.Build(exe));
            }
            else
            {
                run.DeleteValue(AutostartCommand.RunValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            log(ex);
            return false;
        }
    }
}
