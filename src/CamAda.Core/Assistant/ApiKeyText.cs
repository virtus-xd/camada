namespace CamAda.Core.Assistant;

public static class ApiKeyText
{
    /// <summary>
    /// Google API anahtarı yalnız boşluksuz, yazdırılabilir ASCII'dir. Türkçe klavyeden kalan bir harf ya da görünmez bir
    /// karakter bağlanmadan anahtar hatası sayılır (yoksa istek başlığı kurulamaz ve "internet yok" görünürdü).
    /// </summary>
    public static bool IsWellFormed(string key) => key.Length > 0 && key.All(c => c is > ' ' and <= '~');
}
