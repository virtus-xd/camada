using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CamAda.Core.Audio;

/// <summary>Ses oturumunun sürecinin exe adı, açıklaması ve yolu (simgesi için).</summary>
public sealed record ProcessDetails(string Exe, string Description, string? Path);

/// <summary>
/// Süreç bilgisini okur; hiçbir zaman fırlatmaz. Süreç okunurken kapanabilir (ProcessName "Process has exited",
/// FileVersionInfo silinmiş exe) — o zaman null (son inceleme, önemli 1). Idle (0) ve geçersiz kimlikler aranmaz.
/// </summary>
public static class ProcessInfoReader
{
    public static ProcessDetails? Read(int pid)
    {
        if (pid <= 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            return Read(process);
        }
        catch (Exception ex) when (IsGone(ex))
        {
            return null;
        }
    }

    public static ProcessDetails? Read(Process process)
    {
        try
        {
            string? path = null;
            try
            {
                path = process.MainModule?.FileName;
            }
            catch (Win32Exception)
            {
                // yükseltilmiş ya da korumalı süreç: yol yok, süreç adı kullanılır
            }

            var exe = path is not null ? Path.GetFileName(path) : process.ProcessName + ".exe";
            var description = path is not null ? FileVersionInfo.GetVersionInfo(path).FileDescription ?? "" : "";
            return new ProcessDetails(exe, description, path);
        }
        catch (Exception ex) when (IsGone(ex))
        {
            return null;
        }
    }

    private static bool IsGone(Exception ex) =>
        ex is ArgumentException or InvalidOperationException or Win32Exception or IOException or UnauthorizedAccessException;
}

/// <summary>
/// Süreç bilgisi önbelleği: yalnız başarılı okumalar saklanır; her tazelemede artık oturumu olmayan süreçler unutulur.
/// Windows kimlikleri yeniden kullanır; unutulmazsa yeni süreç eskisinin adıyla görünür ya da hiç görünmez (önemli 2).
/// </summary>
public sealed class ProcessInfoCache(Func<int, ProcessDetails?> read)
{
    private readonly Dictionary<int, ProcessDetails> _known = new();

    public ProcessDetails? Get(int pid)
    {
        if (_known.TryGetValue(pid, out var known))
        {
            return known;
        }

        var details = read(pid);
        if (details is not null)
        {
            _known[pid] = details;
        }

        return details;
    }

    /// <summary>Yalnız verilen kimlikleri tutar.</summary>
    public void Retain(IReadOnlyCollection<int> pids)
    {
        foreach (var pid in _known.Keys.Where(pid => !pids.Contains(pid)).ToList())
        {
            _known.Remove(pid);
        }
    }
}

/// <summary>
/// Core Audio çağrılarında kurtarılabilir hatalar. Marshal.ThrowExceptionForHR bazı HRESULT'ları COMException dışı
/// türlere çevirir (E_INVALIDARG → ArgumentException, E_ACCESSDENIED → UnauthorizedAccessException); bunlar da cihazın
/// kaybolması gibi geçici durumlardır, kabuğu çökertmemeli. Programlama hataları (InvalidOperation, NullReference)
/// yutulmaz.
/// </summary>
public static class CoreAudioErrors
{
    public static bool IsRecoverable(Exception ex) =>
        ex is COMException or ArgumentException or UnauthorizedAccessException or InvalidCastException;
}
