using CamAda.Core.Network;

namespace CamAda.Core.Tests.Network;

public class NetworkIconsTests
{
    private static NetworkSnapshot Snap(NetworkLink link, NetworkReach reach, int bars = 4, bool adapter = true,
        bool radioOn = true, string? ssid = null) => new(link, reach, bars, adapter, radioOn, ssid);

    [Fact]
    public void Ethernet_wins_and_warns_without_internet()
    {
        Assert.Equal(new NetworkIcon(NetworkIconKind.Ethernet, 0, false),
            NetworkIcons.For(Snap(NetworkLink.Ethernet, NetworkReach.Internet)));
        Assert.True(NetworkIcons.For(Snap(NetworkLink.Ethernet, NetworkReach.Local)).Warning);
    }

    [Theory]
    [InlineData(0, 5, 1)]
    [InlineData(1, 5, 1)]
    [InlineData(2, 5, 2)]
    [InlineData(3, 5, 3)]
    [InlineData(4, 5, 4)]
    [InlineData(5, 5, 4)]
    [InlineData(2, 4, 2)]
    [InlineData(4, 4, 4)]
    public void Bars_scale_to_1_to_4(int raw, int max, int expected) =>
        Assert.Equal(expected, NetworkIcons.BarsFrom(raw, max));

    [Fact]
    public void Wifi_without_internet_warns() =>
        Assert.Equal(new NetworkIcon(NetworkIconKind.Wifi, 3, true),
            NetworkIcons.For(Snap(NetworkLink.Wifi, NetworkReach.None, bars: 3, ssid: "Ev")));

    [Fact]
    public void Wifi_with_internet_shows_bars() =>
        Assert.Equal(new NetworkIcon(NetworkIconKind.Wifi, 2, false),
            NetworkIcons.For(Snap(NetworkLink.Wifi, NetworkReach.Internet, bars: 2, ssid: "Ev")));

    [Fact]
    public void No_link_shows_radio_state()
    {
        Assert.Equal(NetworkIconKind.NoNetwork, NetworkIcons.For(Snap(NetworkLink.None, NetworkReach.None, adapter: false)).Kind);
        Assert.Equal(NetworkIconKind.WifiOff, NetworkIcons.For(Snap(NetworkLink.None, NetworkReach.None, radioOn: false)).Kind);
        Assert.Equal(NetworkIconKind.WifiDisconnected, NetworkIcons.For(Snap(NetworkLink.None, NetworkReach.None)).Kind);
    }

    [Fact]
    public void Accessible_names()
    {
        Assert.Equal("Wi‑Fi: Ev, internet var", NetworkIcons.AccessibleName(Snap(NetworkLink.Wifi, NetworkReach.Internet, ssid: "Ev")));
        Assert.Equal("Wi‑Fi: Ev, internet yok", NetworkIcons.AccessibleName(Snap(NetworkLink.Wifi, NetworkReach.Local, ssid: "Ev")));
        Assert.Equal("Ethernet, internet var", NetworkIcons.AccessibleName(Snap(NetworkLink.Ethernet, NetworkReach.Internet)));
        Assert.Equal("Wi‑Fi kapalı", NetworkIcons.AccessibleName(Snap(NetworkLink.None, NetworkReach.None, radioOn: false)));
        Assert.Equal("Wi‑Fi: bağlı değil", NetworkIcons.AccessibleName(Snap(NetworkLink.None, NetworkReach.None)));
        Assert.Equal("Ağ bağlantısı yok", NetworkIcons.AccessibleName(Snap(NetworkLink.None, NetworkReach.None, adapter: false)));
    }

    [Fact]
    public void Wifi_tile_subtitle()
    {
        Assert.Equal("Ev", NetworkIcons.WifiTileSubtitle(Snap(NetworkLink.Wifi, NetworkReach.Internet, ssid: "Ev")));
        Assert.Equal("Bağlı değil", NetworkIcons.WifiTileSubtitle(Snap(NetworkLink.Ethernet, NetworkReach.Internet)));
        Assert.Equal("Kapalı", NetworkIcons.WifiTileSubtitle(Snap(NetworkLink.Ethernet, NetworkReach.Internet, radioOn: false)));
        Assert.Equal("Yok", NetworkIcons.WifiTileSubtitle(Snap(NetworkLink.Ethernet, NetworkReach.Internet, adapter: false)));
    }
}
