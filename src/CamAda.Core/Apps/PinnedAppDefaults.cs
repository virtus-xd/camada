namespace CamAda.Core.Apps;

/// <summary>
/// İlk açılışta sabitlenecek uygulamaları (Chrome, Dosya Gezgini, Steam, Discord) bilgisayarda arar.
/// Yüklü olmayanlar listeye girmez.
/// </summary>
public static class PinnedAppDefaults
{
    private static readonly string[] ChromeCandidates =
    [
        @"%ProgramFiles%\Google\Chrome\Application\chrome.exe",
        @"%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe",
        @"%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe",
    ];

    public static IReadOnlyList<PinnedApp> Resolve(
        Func<string, string> expandEnvironment,
        Func<string, bool> fileExists,
        string? steamExeFromRegistry)
    {
        var apps = new List<PinnedApp>();

        var chrome = ChromeCandidates.Select(expandEnvironment).FirstOrDefault(fileExists);
        if (chrome is not null)
        {
            apps.Add(new PinnedApp("chrome", "Chrome", chrome, "", "chrome.exe"));
        }

        var explorer = expandEnvironment(@"%WINDIR%\explorer.exe");
        if (fileExists(explorer))
        {
            apps.Add(new PinnedApp("explorer", "Dosya Gezgini", explorer, "", "explorer.exe"));
        }

        var steam = steamExeFromRegistry is not null && fileExists(steamExeFromRegistry)
            ? steamExeFromRegistry
            : expandEnvironment(@"%ProgramFiles(x86)%\Steam\steam.exe");
        if (fileExists(steam))
        {
            apps.Add(new PinnedApp("steam", "Steam", steam, "", "steam.exe"));
        }

        var discord = expandEnvironment(@"%LOCALAPPDATA%\Discord\Update.exe");
        if (fileExists(discord))
        {
            apps.Add(new PinnedApp("discord", "Discord", discord, "--processStart Discord.exe", "discord.exe"));
        }

        return apps;
    }
}
