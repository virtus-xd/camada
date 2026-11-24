using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using CamAda.Interop;
using ManagedShell.Common.Helpers;

namespace CamAda.QuickSettings;

/// <summary>Hızlı ayarların dışarıya dokunan düğmeleri (Plan 4.3); denemede sahtesiyle değiştirilir.</summary>
public interface IQuickLauncher
{
    /// <summary>Windows'un ekran alıntısı aracı (Win+Shift+S ile aynı).</summary>
    void Screenshot();

    void Lock();

    void Sleep();

    void TaskManager();

    /// <summary>Ayar sayfası gibi bir adres açar (ör. <c>ms-settings:focus</c>).</summary>
    void Open(string uri);
}

public sealed class SystemQuickLauncher : IQuickLauncher
{
    public void Screenshot() => Open("ms-screenclip:");

    public void Lock() => Native.LockWorkStation();

    public void Sleep() => PowerHelper.Sleep();

    public void TaskManager() => Open("taskmgr.exe");

    public void Open(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true })?.Dispose();
        }
        catch (Win32Exception)
        {
            // Adresi açan uygulama yoksa yapacak bir şey yok; panel zaten kapandı.
        }
    }
}

/// <summary>
/// Yalnız Debug denemesi için (<c>CAMADA_FAKE_LAUNCHER</c>): kilitlemek, uyutmak ya da Görev Yöneticisi'ni açmak yerine
/// her çağrıyı günlüğe bir satır olarak yazar.
/// </summary>
public sealed class RecordingQuickLauncher(string logPath) : IQuickLauncher
{
    public void Screenshot() => Write("screenshot");

    public void Lock() => Write("lock");

    public void Sleep() => Write("sleep");

    public void TaskManager() => Write("taskmgr");

    public void Open(string uri) => Write($"open {uri}");

    private void Write(string line) => File.AppendAllText(logPath, line + Environment.NewLine);
}
