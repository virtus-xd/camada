namespace CamAda.Core.Apps;

/// <summary>Kapatma istenen pencerenin kartı için yapılacak iş.</summary>
public enum PendingCloseOutcome
{
    /// <summary>Pencere henüz kapanmadı: kart soluk beklesin.</summary>
    Wait,

    /// <summary>Pencere kapandı: kart kaldırılsın.</summary>
    Remove,

    /// <summary>Pencere süresinde kapanmadı (kapatma reddedildi): kart eski hâline dönsün.</summary>
    Restore,
}

public static class PendingCloseRule
{
    /// <summary>Bu süre içinde kapanmayan pencerenin kapatmayı reddettiği kabul edilir.</summary>
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromSeconds(3);

    public static PendingCloseOutcome Decide(bool windowExists, TimeSpan elapsed) =>
        !windowExists ? PendingCloseOutcome.Remove
        : elapsed >= GiveUpAfter ? PendingCloseOutcome.Restore
        : PendingCloseOutcome.Wait;
}
