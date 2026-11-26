using System.Runtime.InteropServices;

namespace CamAda.Startup;

/// <summary>Uygulamanın MSIX paketinden (Store kurulumu ya da gevşek kayıt) çalışıp çalışmadığı.</summary>
internal static partial class PackageContext
{
    private const int AppModelErrorNoPackage = 15700;

    /// <summary>Süreç bir paket kimliğiyle çalışıyorsa true. Bir kez hesaplanır.</summary>
    public static bool IsPackaged { get; } = Detect();

    private static bool Detect()
    {
        // Sıfır uzunlukla sorulunca paketli süreçte ERROR_INSUFFICIENT_BUFFER (122), paketsizde APPMODEL_ERROR_NO_PACKAGE döner.
        var length = 0;
        return GetCurrentPackageFullName(ref length, 0) != AppModelErrorNoPackage;
    }

    [LibraryImport("kernel32.dll")]
    private static partial int GetCurrentPackageFullName(ref int packageFullNameLength, nint packageFullName);
}
