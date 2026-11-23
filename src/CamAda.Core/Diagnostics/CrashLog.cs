using System.Globalization;

namespace CamAda.Core.Diagnostics;

/// <summary>
/// Beklenmeyen hataları %LOCALAPPDATA%\CamAda\crash.log dosyasına yazar; kod bilmeyen kullanıcı bu dosyayı paylaşarak
/// sorunu bildirebilir. Dosya büyüyünce crash.old.log olarak saklanır.
/// </summary>
public static class CrashLog
{
    public const long MaxBytes = 512 * 1024;

    public static void Append(string directory, Exception exception, DateTimeOffset now)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "crash.log");

        if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
        {
            File.Move(path, Path.Combine(directory, "crash.old.log"), overwrite: true);
        }

        var entry = $"===== {now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} ====={Environment.NewLine}" +
                    $"{exception}{Environment.NewLine}{Environment.NewLine}";
        File.AppendAllText(path, entry);
    }
}
