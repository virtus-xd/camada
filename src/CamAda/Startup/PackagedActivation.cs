using System.Runtime.InteropServices;

namespace CamAda.Startup;

/// <summary>
/// Paketli uygulamanın yeni bir örneğini Windows'un uygulama etkinleştirme servisiyle başlatır. Böylece yeni süreç paket
/// kimliğini taşır (ayarlar paketin klasöründen okunur, başlangıç görevi ve güncelleme sonrası yeniden açılma çalışır) ve
/// CamAda'nın süreç ağacının dışında kalır (ana süreç öldürülse de yaşar). 26.11 denemesi: Explorer'ın çocuğu olarak
/// başlatılan bekçi Explorer'ın jetonunu alıp paket kimliğini kaybediyordu.
/// </summary>
internal static class PackagedActivation
{
    /// <summary>Manifestteki ana uygulama girişi (<c>Application Id="CamAda"</c>).</summary>
    public const string ApplicationId = "CamAda";

    private const int ActivateNoErrorUi = 0x2;
    private const int ActivateNoSplashScreen = 0x4;

    /// <summary>Yeni örneği <paramref name="arguments"/> komut satırıyla başlatır ve süreç kimliğini döner.</summary>
    /// <exception cref="COMException">Etkinleştirme reddedildi.</exception>
    public static int Start(string arguments)
    {
        var aumid = Windows.ApplicationModel.Package.Current.Id.FamilyName + "!" + ApplicationId;
        var manager = (IApplicationActivationManager)new ApplicationActivationManager();
        try
        {
            manager.ActivateApplication(aumid, arguments, ActivateNoErrorUi | ActivateNoSplashScreen, out var processId);
            return (int)processId;
        }
        finally
        {
            Marshal.ReleaseComObject(manager);
        }
    }

    [ComImport]
    [Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        void ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments, int options, out uint processId);
    }

    [ComImport]
    [Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private class ApplicationActivationManager;
}
