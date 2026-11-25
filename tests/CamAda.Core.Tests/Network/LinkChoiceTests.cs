using CamAda.Core.Network;

namespace CamAda.Core.Tests.Network;

public class LinkChoiceTests
{
    private static LinkCandidate Wlan(NetworkReach reach) => new(NetworkLink.Wifi, reach);
    private static LinkCandidate Ethernet(NetworkReach reach) => new(NetworkLink.Ethernet, reach);

    [Fact]
    public void Wifi_wins_over_virtual_ethernet_with_the_same_reach()
    {
        // WSL/Hyper-V vEthernet de Ethernet türündedir ve yerel erişimlidir; listede önce gelse bile Wi‑Fi gösterilmeli.
        Assert.Equal(1, LinkChoice.PickFallback([Ethernet(NetworkReach.Local), Wlan(NetworkReach.Local)]));
    }

    [Fact]
    public void Link_with_internet_wins_over_local_only()
    {
        Assert.Equal(1, LinkChoice.PickFallback([Wlan(NetworkReach.Local), Ethernet(NetworkReach.Internet)]));
    }

    [Fact]
    public void Links_without_connectivity_are_never_chosen()
    {
        Assert.Null(LinkChoice.PickFallback([Ethernet(NetworkReach.None), Wlan(NetworkReach.None)]));
        Assert.Null(LinkChoice.PickFallback([]));
    }

    [Fact]
    public void Earlier_link_wins_a_tie()
    {
        Assert.Equal(0, LinkChoice.PickFallback([Ethernet(NetworkReach.Local), Ethernet(NetworkReach.Local)]));
    }
}
