using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CamAda.Core.Assistant;

namespace CamAda.Assistant;

/// <summary>
/// Ada'nın hafızası: kurallar <see cref="MemoryBook"/>'ta, saklama %LOCALAPPDATA%\CamAda\ada-hafiza.bin dosyasında
/// (Windows DPAPI, yalnız bu kullanıcı). Her değişiklikte yazılır. Bozuk ya da çözülemeyen dosya
/// <c>ada-hafiza.bozuk.bin</c> olarak yedeklenir ve boş hafızayla başlanır. Dosyaya yazılamayan değişiklik geri alınır ve
/// başarısız sayılır: Ada kaydetmediği şeyi "kaydedildi" demez.
/// </summary>
public sealed class MemoryFile
{
    private readonly string _path;
    private readonly string _corruptPath;
    private readonly TimeProvider _time;
    private readonly object _lock = new();

    public MemoryFile(string directory, TimeProvider time)
    {
        _path = Path.Combine(directory, "ada-hafiza.bin");
        _corruptPath = Path.Combine(directory, "ada-hafiza.bozuk.bin");
        _time = time;
        Book = Load();
    }

    public MemoryBook Book { get; }

    /// <summary>Hafıza değişti (Ada paneli listeyi yeniler).</summary>
    public event Action? Changed;

    public MemoryChange Remember(string topic, string fact)
    {
        MemoryChange change;
        lock (_lock)
        {
            var before = Book.ToJson();
            change = Book.Remember(topic, fact, _time.GetUtcNow());
            if (change is MemoryChange.Rejected)
            {
                return change;
            }

            if (!Save())
            {
                Book.RestoreFrom(before);
                return new MemoryChange.Rejected(WriteFailed);
            }
        }

        Changed?.Invoke();
        return change;
    }

    public MemoryForget Forget(string topic)
    {
        lock (_lock)
        {
            var before = Book.ToJson();
            if (!Book.Forget(topic))
            {
                return MemoryForget.NotFound;
            }

            if (!Save())
            {
                Book.RestoreFrom(before);
                return MemoryForget.WriteFailed;
            }
        }

        Changed?.Invoke();
        return MemoryForget.Removed;
    }

    /// <summary>Hafızayı boşaltır; dosyaya yazılamazsa hiçbir şey silinmez ve false.</summary>
    public bool Clear()
    {
        lock (_lock)
        {
            var before = Book.ToJson();
            Book.Clear();
            if (!Save())
            {
                Book.RestoreFrom(before);
                return false;
            }
        }

        Changed?.Invoke();
        return true;
    }

    public const string WriteFailed = "hafıza dosyasına yazılamadı";

    /// <summary>Talimata eklenecek kopya.</summary>
    public IReadOnlyList<MemoryItem> Snapshot()
    {
        lock (_lock)
        {
            return Book.Items.ToList();
        }
    }

    private MemoryBook Load()
    {
        if (!File.Exists(_path))
        {
            return new MemoryBook();
        }

        try
        {
            var json = ProtectedData.Unprotect(File.ReadAllBytes(_path), null, DataProtectionScope.CurrentUser);
            return MemoryBook.FromJson(Encoding.UTF8.GetString(json));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Ada hafızası okunamadı, yedekleniyor: {ex.GetType().Name}");
            try
            {
                File.Copy(_path, _corruptPath, overwrite: true);
            }
            catch (Exception copy) when (copy is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Ada hafızası yedeklenemedi: {copy.GetType().Name}");
            }

            return new MemoryBook();
        }
    }

    private bool Save()
    {
        var temp = _path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(Book.ToJson()), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, _path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Ada hafızası kaydedilemedi: {ex.GetType().Name}");
            TryDelete(temp);
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

public enum MemoryForget
{
    Removed,
    NotFound,

    /// <summary>Dosyaya yazılamadı; madde yerinde kaldı.</summary>
    WriteFailed,
}
