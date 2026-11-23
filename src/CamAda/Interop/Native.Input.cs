using System.Runtime.InteropServices;

namespace CamAda.Interop;

/// <summary>Çift tıklama süresi (tepsi paneli için).</summary>
internal static partial class Native
{
    /// <summary>Sistemin çift tıklama süresi (ms); tepsi simgelerine iletilir.</summary>
    public static int DoubleClickTime => (int)GetDoubleClickTime();

    [LibraryImport("user32.dll")]
    private static partial uint GetDoubleClickTime();
}
