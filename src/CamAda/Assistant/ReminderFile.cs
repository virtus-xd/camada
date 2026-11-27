using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CamAda.Core.Assistant;

namespace CamAda.Assistant;

/// <summary>
/// Ada'nın hatırlatmaları: kurallar <see cref="ReminderBook"/>'ta, saklama <c>%LOCALAPPDATA%\CamAda\ada-hatirlatmalar.bin</c>
/// dosyasında (DPAPI, yalnız bu kullanıcı; hatırlatma metni kişisel olabilir). Her değişiklikte yazılır; yazılamayan değişiklik geri
/// alınır ve başarısız sayılır (Ada kurmadığı hatırlatmayı "kurdum" demez). Okunamayan dosya <c>.bozuk.bin</c> olarak yedeklenir.
/// </summary>
internal sealed class ReminderFile
{
    public const string WriteFailed = "hatırlatma dosyasına yazılamadı"; // i18n-ignore: modele gider (Ada 2. adım)

    private readonly string _path;
    private readonly string _corruptPath;
    private readonly object _lock = new();

    public ReminderFile(string directory, TimeProvider time)
    {
        _path = Path.Combine(directory, "ada-hatirlatmalar.bin");
        _corruptPath = Path.Combine(directory, "ada-hatirlatmalar.bozuk.bin");
        Book = new ReminderBook(time);
        Load();
    }

    public ReminderBook Book { get; }

    public ReminderChange Add(string text, DateTimeOffset at) => Change(() => Book.Add(text, at));

    public ReminderChange Snooze(int minutes) => Change(() => Book.Snooze(minutes));

    /// <summary>Söylenen ada en çok benzeyeni siler; dosyaya yazılamazsa hiçbir şey silinmez ve null.</summary>
    public NameMatchResult? Cancel(string query)
    {
        lock (_lock)
        {
            var before = Book.ToJson();
            var result = Book.Cancel(query);
            if (result is NameMatchResult.Found && !Save())
            {
                Book.LoadJson(before);
                return null;
            }

            return result;
        }
    }

    /// <summary>Zamanı gelenler; dosyaya yazılamazsa da döner (söylenirler; dosya bir sonraki yazımda düzelir).</summary>
    public IReadOnlyList<Reminder> TakeDue()
    {
        lock (_lock)
        {
            var due = Book.TakeDue();
            if (due.Count > 0 && !Save())
            {
                Debug.WriteLine("Dolan hatırlatmalar dosyaya yazılamadı");
            }

            return due;
        }
    }

    private ReminderChange Change(Func<ReminderChange> change)
    {
        lock (_lock)
        {
            var before = Book.ToJson();
            var result = change();
            if (result is ReminderChange.Added && !Save())
            {
                Book.LoadJson(before);
                return new ReminderChange.Rejected(WriteFailed);
            }

            return result;
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            Book.LoadJson(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(_path), null, DataProtectionScope.CurrentUser)));
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Hatırlatmalar okunamadı, yedekleniyor: {ex.GetType().Name}");
            try
            {
                File.Copy(_path, _corruptPath, overwrite: true);
            }
            catch (Exception copy) when (copy is IOException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Hatırlatmalar yedeklenemedi: {copy.GetType().Name}");
            }
        }
    }

    private bool Save()
    {
        var temp = _path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllBytes(temp, ProtectedData.Protect(Encoding.UTF8.GetBytes(Book.ToJson()), null, DataProtectionScope.CurrentUser));
            File.Move(temp, _path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Hatırlatmalar kaydedilemedi: {ex.GetType().Name}");
            try
            {
                File.Delete(temp);
            }
            catch (Exception delete) when (delete is IOException or UnauthorizedAccessException)
            {
            }

            return false;
        }
    }
}
