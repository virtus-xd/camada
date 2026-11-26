using CamAda.Core.Network;

namespace CamAda.Core.Tests.Network;

public class WifiStartupTests
{
    [Fact]
    public void Unpackaged_keeps_asking_at_startup() =>
        Assert.Equal(WifiStartupMode.RequestNow, WifiStartup.Decide(packaged: false, hasWifiRadio: true));

    [Fact]
    public void Packaged_with_wifi_radio_defers_until_list_opens() =>
        Assert.Equal(WifiStartupMode.Deferred, WifiStartup.Decide(packaged: true, hasWifiRadio: true));

    [Fact]
    public void Packaged_without_wifi_radio_never_asks() =>
        Assert.Equal(WifiStartupMode.None, WifiStartup.Decide(packaged: true, hasWifiRadio: false));
}
