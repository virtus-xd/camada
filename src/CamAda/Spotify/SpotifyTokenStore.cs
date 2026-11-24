using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using CamAda.Core.Spotify;

namespace CamAda.Spotify;

/// <summary>
/// Spotify belirteçleri %LOCALAPPDATA%\CamAda\spotify-token.bin dosyasında, Windows DPAPI ile yalnız bu kullanıcının
/// çözebileceği şekilde şifreli saklanır. Dosya bozuk ya da çözülemiyorsa silinir (yeniden bağlanmak gerekir).
/// </summary>
public sealed class SpotifyTokenStore
{
    private readonly string _path;

    public SpotifyTokenStore(string directory)
    {
        _path = Path.Combine(directory, "spotify-token.bin");
    }

    public SpotifyToken? Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var json = ProtectedData.Unprotect(File.ReadAllBytes(_path), null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<SpotifyToken>(json);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or JsonException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Spotify belirteci okunamadı, siliniyor: {ex.Message}");
            Clear();
            return null;
        }
    }

    public void Save(SpotifyToken token)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var protectedBytes = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(token), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_path, protectedBytes);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Spotify belirteci kaydedilemedi: {ex.Message}");
        }
    }

    public void Clear()
    {
        try
        {
            File.Delete(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Spotify belirteci silinemedi: {ex.Message}");
        }
    }
}
