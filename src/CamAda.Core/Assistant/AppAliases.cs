namespace CamAda.Core.Assistant;

/// <summary>
/// Windows'un bazı yerleşik uygulamaları Türkçe sistemde de Başlat menüsünde İngilizce adla görünür ("Notepad"); kullanıcı
/// ise Türkçe adını söyler ("Not Defteri"). Bu tablo İngilizce ada Türkçe karşılıklarını ekler.
/// </summary>
public static class AppAliases
{
    private static readonly Dictionary<string, string[]> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Notepad"] = ["Not Defteri"],
        ["Calculator"] = ["Hesap Makinesi"],
        ["Photos"] = ["Fotoğraflar"],
        ["Camera"] = ["Kamera"],
        ["Clock"] = ["Saat", "Alarmlar ve Saat"],
        ["Snipping Tool"] = ["Ekran Alıntısı Aracı"],
        ["Sticky Notes"] = ["Yapışkan Notlar"],
        ["Media Player"] = ["Medya Oynatıcı"],
        ["Sound Recorder"] = ["Ses Kaydedici"],
        ["Weather"] = ["Hava Durumu"],
        ["Maps"] = ["Haritalar"],
        ["Task Manager"] = ["Görev Yöneticisi"],
        ["File Explorer"] = ["Dosya Gezgini"],
        ["Control Panel"] = ["Denetim Masası"],
        ["Command Prompt"] = ["Komut İstemi"],
        ["Settings"] = ["Ayarlar"],
    };

    /// <summary>Uygulamanın Başlat adına karşılık gelen Türkçe adlar; yoksa boş.</summary>
    public static IReadOnlyList<string> For(string name) => Table.TryGetValue(name.Trim(), out var aliases) ? aliases : [];
}
