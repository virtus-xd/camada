namespace CamAda.Core.Network;

/// <summary>Bağlı bir Wi‑Fi ya da Ethernet bağdaştırıcısının profili ve erişim düzeyi.</summary>
public sealed record LinkCandidate(NetworkLink Link, NetworkReach Reach);

/// <summary>
/// İnternet bir VPN üzerinden gidiyorsa ya da internet yoksa adada gösterilecek alttaki bağlantı: önce internete çıkabilen,
/// eşitse Wi‑Fi (WSL/Hyper-V'nin sanal Ethernet'i de Ethernet türündedir ve hep yerel erişimlidir), eşitse listedeki ilk.
/// </summary>
public static class LinkChoice
{
    public static int? PickFallback(IReadOnlyList<LinkCandidate> candidates)
    {
        int? best = null;
        for (var i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Reach != NetworkReach.None && (best is null || Rank(candidates[i]) > Rank(candidates[best.Value])))
            {
                best = i;
            }
        }

        return best;
    }

    private static int Rank(LinkCandidate c) => (int)c.Reach * 2 + (c.Link == NetworkLink.Wifi ? 1 : 0);
}
