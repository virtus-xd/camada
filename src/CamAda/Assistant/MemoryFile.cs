using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CamAda.Core.Assistant;

namespace CamAda.Assistant;

/// <summary>
/// Ada'nın hafızası: kurallar <see cref="MemoryBook"/>'ta, saklama %LOCALAPPDATA%\CamAda\ada-hafiza.bin dosyasında
/// (Windows DPAPI, yalnız bu kullanıcı). Her değişiklikte yazılır. Bozuk ya da çözülemeyen dosya
/// <c>ada-hafiza.bozuk.bin</c> olarak yedeklenir ve boş hafızayla başlanır.
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
            change = Book.Remember(topic, fact, _time.GetUtcNow());
            if (change is MemoryChange.Rejected)
            {
                return change;
            }

            Save();
        }

        Changed?.Invoke();
        return change;
    }

    public bool Forget(string topic)
    {
        lock (_lock)
        {
            if (!Book.Forget(topic))
            {
                return false;
            }

            Save();
        }

        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        lock (_lock)
        {
            Book.Clear();
            Save();
        }

        Changed?.Invoke();
    }

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

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(Book.ToJson()), null, DataProtectionScope.CurrentUser);
            var temp = _path + ".tmp";
            File.WriteAllBytes(temp, bytes);
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Ada hafızası kaydedilemedi: {ex.GetType().Name}");
        }
    }
}
