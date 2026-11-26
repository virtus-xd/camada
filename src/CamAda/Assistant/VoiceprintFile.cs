using System.IO;
using System.Security.Cryptography;
using System.Text;
using CamAda.Core.Assistant;

namespace CamAda.Assistant;

public enum VoiceprintStatus
{
    /// <summary>Ses tanıtılmadı.</summary>
    None,

    /// <summary>Ses izi var ve bu modelle alınmış.</summary>
    Ready,

    /// <summary>Dosya var ama başka modelle alınmış ya da okunamıyor: yeniden tanıtılmalı.</summary>
    Stale,
}

/// <summary>
/// Kullanıcının ses izi (misafir kipi): <c>%LOCALAPPDATA%\CamAda\ada-ses-izi.bin</c>, Windows DPAPI ile yalnız bu kullanıcıya
/// şifreli. Ses kaydı değil, yalnız sayısal gömmeler. Okunamayan ya da başka modelle alınmış iz yok sayılır.
/// </summary>
internal sealed class VoiceprintFile(string directory)
{
    private readonly string _path = Path.Combine(directory, "ada-ses-izi.bin");

    /// <summary>Ses izi kaydedildi ya da silindi.</summary>
    public event Action? Changed;

    public Voiceprint? Load(string model)
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var json = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(_path), null, DataProtectionScope.CurrentUser));
            return Voiceprint.FromJson(json, model);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return null;
        }
    }

    public VoiceprintStatus Status(string model) =>
        !File.Exists(_path) ? VoiceprintStatus.None : Load(model) is null ? VoiceprintStatus.Stale : VoiceprintStatus.Ready;

    /// <summary>Önce geçici dosyaya yazılır, sonra yerine konur (yarım iz kalmasın). Yazılamazsa false.</summary>
    public bool Save(Voiceprint print)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var temp = _path + ".yeni";
            File.WriteAllBytes(temp, ProtectedData.Protect(Encoding.UTF8.GetBytes(print.ToJson()), null, DataProtectionScope.CurrentUser));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return false;
        }

        Changed?.Invoke();
        return true;
    }

    public bool Delete()
    {
        try
        {
            File.Delete(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }

        Changed?.Invoke();
        return true;
    }
}
