using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using CamAda.Audio;
using CamAda.Core.Search;
using CamAda.Core.Settings;
using CamAda.Media;
using CamAda.Panels;
using CamAda.QuickSettings;
using CamAda.Spotify;

namespace CamAda.Assistant;

/// <summary>
/// Açık bir üst düzey pencere. <see cref="AppName"/> kullanıcının söyleyeceği ad ("Not Defteri", "Google Chrome");
/// <see cref="ExeName"/> uzantısız dosya adı ("notepad", "chrome").
/// </summary>
public sealed record OpenWindow(nint Hwnd, string AppName, string ExeName, string Title);

/// <summary>Ada'nın işlevlerinin dokunduğu servisler; olmayan servis null (işlev "kullanılamıyor" der).</summary>
internal sealed record AdaToolContext(
    AudioService? Audio,
    BrightnessService? Brightness,
    RadioService? Radios,
    NowPlayingService? Media,
    SpotifyClient? Spotify,
    AppIndex Apps,
    IQuickLauncher Launcher,
    IPowerActions Power,
    Func<IReadOnlyList<OpenWindow>> OpenWindows,
    MemoryFile Memory,
    CurrentInfoClient Info,
    Func<AssistantSettings> Settings,
    Action EndSessionAfterSpeech,
    Action<LookupResult> LookupUsed)
{
    /// <summary>Misafir kipini elle açar/kapatır; kip kullanılamıyorsa (ses tanıma yüklenemedi) false. AdaAssistant bağlar.</summary>
    public Func<bool, bool> SetGuestMode { get; init; } = _ => false;

    /// <summary>Hatırlatmalar (Plan 9); null ise işlevler "kullanılamıyor" der.</summary>
    public ReminderFile? Reminders { get; init; }
}

public static class OpenWindowNames
{
    private const string AppsFolderPrefix = @"shell:AppsFolder\";
    private static readonly ConcurrentDictionary<string, string> Descriptions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Pencerenin kullanıcıya görünen uygulama adı: Store uygulamasıysa Başlat listesindeki adı (AUMID eşleşmesi), değilse
    /// dosya açıklaması ("Google Chrome"), o da yoksa uzantısız dosya adı.
    /// </summary>
    public static string AppName(string? aumid, string? exePath, IReadOnlyList<SearchItem> apps)
    {
        if (!string.IsNullOrEmpty(aumid)
            && apps.FirstOrDefault(a => a.Target.Equals(AppsFolderPrefix + aumid, StringComparison.OrdinalIgnoreCase)) is { } app)
        {
            return app.Name;
        }

        if (string.IsNullOrEmpty(exePath))
        {
            return "";
        }

        return Descriptions.GetOrAdd(exePath, static path =>
        {
            try
            {
                var description = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
                return string.IsNullOrEmpty(description) ? Path.GetFileNameWithoutExtension(path) : description;
            }
            catch (Exception ex) when (ex is FileNotFoundException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                return Path.GetFileNameWithoutExtension(path);
            }
        });
    }
}
