using System.Diagnostics;
using CamAda.Core.Clipboard;
using CamAda.Interop;

namespace CamAda.Clips;

/// <summary>
/// Win+Alt+V ile açılan panelde seçilen öğeyi önceki pencereye yapıştırır (tasarım "Win+Alt+V ve yapıştırma"). Ctrl+V
/// yalnız hedef pencere öndeyken ve Win/Alt/Ctrl/Shift bırakılmışken gönderilir; aksi hâlde hiçbir tuş gönderilmez.
/// </summary>
internal static class PasteBack
{
    public static nint CaptureForeground() => Native.GetForegroundWindow();

    public static PasteTarget Describe(nint hwnd, int ownProcessId)
    {
        if (hwnd == 0 || !Native.WindowExists(hwnd))
        {
            return new PasteTarget(Exists: false, IsShellOrSelf: false, Elevated: false);
        }

        var processId = Native.ProcessIdOf(hwnd);
        var shellOrSelf = processId == ownProcessId || Native.ClassNameOf(hwnd) is "Progman" or "WorkerW";
        return new PasteTarget(Exists: true, shellOrSelf, Elevated: !shellOrSelf && Native.IsElevated(processId));
    }

    /// <summary>Hedefi öne alır, tuşların bırakılmasını bekler ve Ctrl+V gönderir. Gönderildiyse true.</summary>
    public static async Task<bool> PasteIntoAsync(nint hwnd)
    {
        Native.SetForegroundWindow(hwnd);
        if (!await WaitUntilAsync(() => Native.GetForegroundWindow() == hwnd, TimeSpan.FromMilliseconds(500)))
        {
            return false;
        }

        if (Native.AltOrWinDown())
        {
            Native.SendMaskKey(); // bırakılan Alt hedefi menü moduna sokmasın (yoksa Ctrl+V yutulur)
        }

        if (!await WaitUntilAsync(() => !Native.AnyModifierDown(), TimeSpan.FromSeconds(2))
            || Native.GetForegroundWindow() != hwnd)
        {
            return false;
        }

        return Native.SendCtrlV();
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan limit)
    {
        var started = Stopwatch.GetTimestamp();
        while (!condition())
        {
            if (Stopwatch.GetElapsedTime(started) > limit)
            {
                return false;
            }

            await Task.Delay(20);
        }

        return true;
    }
}
