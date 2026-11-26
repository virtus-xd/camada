using System.Runtime.InteropServices;
using CamAda.Core.Startup;

namespace CamAda.Startup;

/// <summary>Store güncellemesi uygulamayı kapattıktan sonra Windows'un CamAda'yı yeniden açması için kayıt.</summary>
internal static partial class AppRestart
{
    private const int RestartNoCrash = 1;
    private const int RestartNoHang = 2;
    private const int RestartNoReboot = 8;

    /// <summary>
    /// Yalnız paket güncellemesinden sonra yeniden açılır: çökme ve donmada açılmaz (bekçi görev çubuğunu geri getirir),
    /// sistem yeniden başlayınca da açılmaz (başlangıç görevi açar). Yeni örnek eskisinin kapanmasını bekler.
    /// </summary>
    public static void RegisterAfterUpdate() =>
        _ = RegisterApplicationRestart(StartupMode.RestartingArgument, RestartNoCrash | RestartNoHang | RestartNoReboot);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int RegisterApplicationRestart(string? commandLine, int flags);
}
