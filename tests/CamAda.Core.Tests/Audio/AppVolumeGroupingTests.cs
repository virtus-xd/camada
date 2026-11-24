using CamAda.Core.Audio;

namespace CamAda.Core.Tests.Audio;

public class AppVolumeGroupingTests
{
    private static SessionSample S(int pid, string exe, float volume = 1f, string name = "", bool muted = false,
        bool active = true, bool system = false) => new(pid, exe, name, volume, muted, active, system);

    [Fact]
    public void Inactive_and_system_sounds_are_hidden() =>
        Assert.Empty(AppVolumeGrouping.Group([S(1, "a.exe", active: false), S(0, "", system: true)]));

    [Fact]
    public void Sessions_of_one_app_are_one_row()
    {
        var row = Assert.Single(AppVolumeGrouping.Group(
            [S(10, "chrome.exe", 0.2f), S(11, "Chrome.EXE", 0.8f), S(12, "chrome.exe", 0.5f)]));

        Assert.Equal("chrome.exe", row.Key);
        Assert.Equal(3, row.ProcessIds.Count);
        Assert.Equal(0.8f, row.Volume);
    }

    [Fact]
    public void Name_falls_back_to_exe_name()
    {
        var rows = AppVolumeGrouping.Group([S(1, "discord.exe"), S(2, "spotify.exe", name: "Spotify")]);

        Assert.Equal(["discord", "Spotify"], rows.Select(r => r.Name));
    }

    [Fact]
    public void Muted_only_when_every_session_is_muted()
    {
        Assert.False(Assert.Single(AppVolumeGrouping.Group([S(1, "a.exe", muted: true), S(2, "a.exe")])).Muted);
        Assert.True(Assert.Single(AppVolumeGrouping.Group([S(1, "a.exe", muted: true), S(2, "a.exe", muted: true)])).Muted);
    }

    [Fact]
    public void Rows_are_sorted_by_name() =>
        Assert.Equal(["çalar", "zil"],
            AppVolumeGrouping.Group([S(1, "zil.exe"), S(2, "calar.exe", name: "çalar")]).Select(r => r.Name));
}
