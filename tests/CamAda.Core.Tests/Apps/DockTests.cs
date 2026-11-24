using CamAda.Core.Apps;

namespace CamAda.Core.Tests.Apps;

public class DockTests
{
    private static string Expand(string s) => s
        .Replace("%ProgramFiles%", @"C:\Program Files")
        .Replace("%ProgramFiles(x86)%", @"C:\Program Files (x86)")
        .Replace("%LOCALAPPDATA%", @"C:\Users\u\AppData\Local")
        .Replace("%WINDIR%", @"C:\Windows");

    private const string ChromePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
    private const string DiscordPath = @"C:\Users\u\AppData\Local\Discord\Update.exe";
    private const string SteamDefault = @"C:\Program Files (x86)\Steam\steam.exe";

    private static PinnedApp Pin(string id, string process) => new(id, id, $@"C:\{id}.exe", "", process);

    private static RunningWindow Win(string process, bool active = false, bool flashing = false,
        bool minimized = false, string? aumid = null, bool badge = false, nint handle = 1) =>
        new(handle, process, "başlık", active, flashing, minimized, aumid, badge);

    [Fact]
    public void Defaults_keep_order_and_skip_missing()
    {
        var existing = new HashSet<string> { ChromePath, DiscordPath };
        var apps = PinnedAppDefaults.Resolve(Expand, existing.Contains, steamExeFromRegistry: null);
        Assert.Equal(new[] { "chrome", "discord" }, apps.Select(a => a.Id));
    }

    [Fact]
    public void All_four_defaults_in_order()
    {
        var existing = new HashSet<string> { ChromePath, DiscordPath, SteamDefault, @"C:\Windows\explorer.exe" };
        var apps = PinnedAppDefaults.Resolve(Expand, existing.Contains, steamExeFromRegistry: null);
        Assert.Equal(new[] { "chrome", "explorer", "steam", "discord" }, apps.Select(a => a.Id));
        Assert.Equal("Dosya Gezgini", apps[1].Name);
        Assert.Equal("explorer.exe", apps[1].ProcessName);
    }

    [Fact]
    public void Chrome_falls_back_to_per_user_install()
    {
        var perUser = @"C:\Users\u\AppData\Local\Google\Chrome\Application\chrome.exe";
        var apps = PinnedAppDefaults.Resolve(Expand, p => p == perUser, steamExeFromRegistry: null);
        Assert.Equal(perUser, Assert.Single(apps).LaunchPath);
    }

    [Fact]
    public void Steam_from_registry_wins()
    {
        var registry = @"D:\Oyun\Steam\steam.exe";
        var apps = PinnedAppDefaults.Resolve(Expand, p => p == registry || p == SteamDefault, registry);
        Assert.Equal(registry, Assert.Single(apps).LaunchPath);
    }

    [Fact]
    public void Discord_uses_update_exe_with_process_start()
    {
        var discord = Assert.Single(PinnedAppDefaults.Resolve(Expand, p => p == DiscordPath, null));
        Assert.Equal(DiscordPath, discord.LaunchPath);
        Assert.Equal("--processStart Discord.exe", discord.Arguments);
        Assert.Equal("discord.exe", discord.ProcessName);
    }

    [Fact]
    public void Pinned_apps_appear_even_when_not_running()
    {
        var pinned = new[] { Pin("chrome", "chrome.exe"), Pin("explorer", "explorer.exe"), Pin("steam", "steam.exe"), Pin("discord", "discord.exe") };
        var entries = DockGrouping.Group(pinned, []);
        Assert.Equal(4, entries.Count);
        Assert.All(entries, e => Assert.False(e.IsRunning));
    }

    [Fact]
    public void Steam_web_helper_windows_group_under_pinned_steam()
    {
        // Yeni Steam istemcisinde ana pencereyi steamwebhelper.exe açar.
        var entries = DockGrouping.Group([Pin("steam", "steam.exe")], [Win("steamwebhelper.exe", handle: 1)]);
        var steam = Assert.Single(entries);
        Assert.Equal("pin:steam", steam.Key);
        Assert.Single(steam.Windows);
    }

    [Fact]
    public void Windows_group_under_pinned_app_case_insensitively()
    {
        var entries = DockGrouping.Group([Pin("chrome", "chrome.exe")], [Win("Chrome.exe", handle: 1), Win("chrome.exe", handle: 2)]);
        var chrome = Assert.Single(entries);
        Assert.Equal(2, chrome.Windows.Count);
    }

    [Fact]
    public void Unpinned_running_apps_follow_pinned_in_first_seen_order()
    {
        var entries = DockGrouping.Group(
            [Pin("chrome", "chrome.exe")],
            [Win("code.exe", handle: 1), Win("chrome.exe", handle: 2), Win("notepad.exe", handle: 3), Win("code.exe", handle: 4)]);
        Assert.Equal(3, entries.Count);
        Assert.Equal("chrome", entries[0].Pinned?.Id);
        Assert.Null(entries[1].Pinned);
        Assert.Equal(2, entries[1].Windows.Count);
        Assert.Equal("code.exe", entries[1].Windows[0].ProcessName);
        Assert.Equal("notepad.exe", entries[2].Windows[0].ProcessName);
    }

    [Fact]
    public void Unpinned_group_by_app_user_model_id_when_present()
    {
        var entries = DockGrouping.Group([], [Win("msedge.exe", aumid: "A", handle: 1), Win("msedge.exe", aumid: "B", handle: 2)]);
        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public void Attention_and_badge_flags()
    {
        var entry = Assert.Single(DockGrouping.Group([Pin("discord", "discord.exe")], [Win("discord.exe", flashing: true, badge: true)]));
        Assert.True(entry.NeedsAttention);
        Assert.True(entry.HasBadge);
    }

    [Fact]
    public void Click_decisions()
    {
        var pin = Pin("x", "x.exe");
        Assert.Equal(ClickKind.Launch, TaskClickAction.Decide(new DockEntry("k", pin, [])).Kind);

        var active = Win("x.exe", active: true);
        var minimize = TaskClickAction.Decide(new DockEntry("k", pin, [active]));
        Assert.Equal(ClickKind.Minimize, minimize.Kind);
        Assert.Equal(active, minimize.Window);

        Assert.Equal(ClickKind.Activate, TaskClickAction.Decide(new DockEntry("k", pin, [Win("x.exe")])).Kind);
        Assert.Equal(ClickKind.Activate, TaskClickAction.Decide(new DockEntry("k", pin, [Win("x.exe", active: true, minimized: true)])).Kind);
        Assert.Equal(ClickKind.ShowWindows, TaskClickAction.Decide(new DockEntry("k", pin, [Win("x.exe", handle: 1), Win("x.exe", handle: 2)])).Kind);
    }
}
