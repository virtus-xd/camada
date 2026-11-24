using CamAda.Core.Network;

namespace CamAda.Core.Tests.Network;

public class WifiNetworkListTests
{
    private static readonly IReadOnlySet<string> NoneSaved = new HashSet<string>();

    [Fact]
    public void Same_ssid_is_one_row_with_strongest_bars()
    {
        var rows = WifiNetworkList.Build(
            [new("Ev", 2, WifiSecurity.Password), new("Ev", 4, WifiSecurity.Password)], null, NoneSaved);

        var row = Assert.Single(rows);
        Assert.Equal(4, row.Bars);
    }

    [Fact]
    public void Hidden_networks_are_skipped()
    {
        var rows = WifiNetworkList.Build(
            [new("", 4, WifiSecurity.Open), new("  ", 3, WifiSecurity.Open), new("Kafe", 2, WifiSecurity.Open)], null, NoneSaved);

        Assert.Equal(["Kafe"], rows.Select(r => r.Ssid));
    }

    [Fact]
    public void Order_is_connected_then_saved_then_signal()
    {
        var rows = WifiNetworkList.Build(
            [new("A", 4, WifiSecurity.Open), new("B", 2, WifiSecurity.Password), new("C", 3, WifiSecurity.Password)],
            new WifiNetworkSeen("D", 3, WifiSecurity.Password),
            new HashSet<string> { "B", "D" });

        Assert.Equal(["D", "B", "A", "C"], rows.Select(r => r.Ssid));
        Assert.True(rows[0].Connected);
        Assert.True(rows[1].Saved);
        Assert.False(rows[1].Connected);
        Assert.False(rows[2].Saved);
    }

    [Fact]
    public void Connected_network_merges_with_seen_row()
    {
        var rows = WifiNetworkList.Build(
            [new("Ev", 4, WifiSecurity.Password), new("Komşu", 2, WifiSecurity.Password)],
            new WifiNetworkSeen("Ev", 3, WifiSecurity.Password), NoneSaved);

        Assert.Equal(2, rows.Count);
        Assert.Equal(new WifiRow("Ev", 4, WifiSecurity.Password, true, false), rows[0]);
    }

    [Fact]
    public void Equal_bars_sort_by_name()
    {
        var rows = WifiNetworkList.Build(
            [new("b", 3, WifiSecurity.Open), new("A", 3, WifiSecurity.Open)], null, NoneSaved);

        Assert.Equal(["A", "b"], rows.Select(r => r.Ssid));
    }

    [Theory]
    [InlineData("Open80211", "None", WifiSecurity.Open)]
    [InlineData("Owe", "Ccmp", WifiSecurity.Open)]
    [InlineData("None", "None", WifiSecurity.Open)]
    [InlineData("Open80211", "Wep", WifiSecurity.Password)]
    [InlineData("SharedKey80211", "Wep", WifiSecurity.Password)]
    [InlineData("WpaPsk", "Tkip", WifiSecurity.Password)]
    [InlineData("RsnaPsk", "Ccmp", WifiSecurity.Password)]
    [InlineData("Wpa3Sae", "Ccmp", WifiSecurity.Password)]
    [InlineData("Wpa", "Tkip", WifiSecurity.Enterprise)]
    [InlineData("Rsna", "Ccmp", WifiSecurity.Enterprise)]
    [InlineData("Wpa3Enterprise", "Gcmp", WifiSecurity.Enterprise)]
    [InlineData("Wpa3Enterprise192Bits", "Gcmp256", WifiSecurity.Enterprise)]
    [InlineData("Ihv", "None", WifiSecurity.Enterprise)]
    [InlineData("WpaNone", "None", WifiSecurity.Enterprise)]
    [InlineData("Unknown", "None", WifiSecurity.Enterprise)]
    public void Security_from_authentication(string authentication, string encryption, WifiSecurity expected) =>
        Assert.Equal(expected, WifiNetworkList.SecurityFrom(authentication, encryption));

    [Theory]
    [InlineData(3, 0, true)]
    [InlineData(0, 0, false)]
    [InlineData(3, 1, false)]
    public void Location_blocked_when_networks_have_no_names(int total, int named, bool expected) =>
        Assert.Equal(expected, WifiNetworkList.LooksLocationBlocked(total, named));
}
