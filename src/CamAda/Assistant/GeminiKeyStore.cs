using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CamAda.Assistant;

/// <summary>
/// Gemini API anahtarı %LOCALAPPDATA%\CamAda\gemini-key.bin dosyasında, Windows DPAPI ile yalnız bu kullanıcının
/// çözebileceği şekilde şifreli saklanır (Spotify belirteciyle aynı yöntem). Anahtar hiçbir günlüğe yazılmaz.
/// </summary>
public sealed class GeminiKeyStore(string directory)
{
    private readonly string _path = Path.Combine(directory, "gemini-key.bin");

    public string? Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var key = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(_path), null, DataProtectionScope.CurrentUser));
            return string.IsNullOrWhiteSpace(key) ? null : key;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Gemini anahtarı okunamadı: {ex.GetType().Name}");
            return null;
        }
    }

    public void Save(string key)
    {
        Directory.CreateDirectory(directory);
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(Clean(key)), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, bytes);
    }

    public void Clear()
    {
        try
        {
            File.Delete(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Gemini anahtarı silinemedi: {ex.GetType().Name}");
        }
    }

    /// <summary>
    /// Deneme sırasında düz metin kaydedilen anahtarı (<c>gemini-anahtar.txt</c>) bir kez içeri alır ve dosyayı siler.
    /// Dosya yoksa ya da boşsa false.
    /// </summary>
    public bool ImportLegacy(string legacyPath)
    {
        try
        {
            if (!File.Exists(legacyPath))
            {
                return false;
            }

            var key = Clean(File.ReadAllText(legacyPath));
            if (key.Length == 0)
            {
                return false;
            }

            Save(key);
            File.Delete(legacyPath);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Eski Gemini anahtarı içeri alınamadı: {ex.GetType().Name}");
            return false;
        }
    }

    private static string Clean(string key) => key.Trim().Trim('﻿').Trim();
}
