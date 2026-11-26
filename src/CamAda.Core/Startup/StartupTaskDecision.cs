namespace CamAda.Core.Startup;

/// <summary>Paketli uygulamanın başlangıç görevi durumu; değerler <c>Windows.ApplicationModel.StartupTaskState</c> ile aynı.</summary>
public enum StartupTaskStatus
{
    Disabled = 0,
    DisabledByUser = 1,
    Enabled = 2,
    DisabledByPolicy = 3,
    EnabledByPolicy = 4,
}

/// <summary>Kullanıcının isteğine ve görevin durumuna göre yapılacak iş.</summary>
public enum StartupTaskAction
{
    /// <summary>Durum zaten istenen gibi.</summary>
    None,

    /// <summary>Görevi aç (kullanıcı ya da yönetici kapatmamış).</summary>
    RequestEnable,

    /// <summary>Görevi kapat.</summary>
    Disable,

    /// <summary>Kullanıcı Windows ayarlarından ya da Görev Yöneticisi'nden kapatmış; uygulama zorla açamaz.</summary>
    BlockedByUser,

    /// <summary>Kurum ilkesi belirliyor; uygulama değiştiremez.</summary>
    BlockedByPolicy,
}

/// <summary>Paketli uygulamada "Windows açılınca başlat" isteğini başlangıç görevi durumuna çevirir.</summary>
public static class StartupTaskDecision
{
    public static StartupTaskAction Decide(bool wanted, StartupTaskStatus status) => (wanted, status) switch
    {
        (true, StartupTaskStatus.Disabled) => StartupTaskAction.RequestEnable,
        (true, StartupTaskStatus.DisabledByUser) => StartupTaskAction.BlockedByUser,
        (true, StartupTaskStatus.DisabledByPolicy) => StartupTaskAction.BlockedByPolicy,
        (false, StartupTaskStatus.Enabled) => StartupTaskAction.Disable,
        (false, StartupTaskStatus.EnabledByPolicy) => StartupTaskAction.BlockedByPolicy,
        _ => StartupTaskAction.None,
    };

    public static bool IsOn(StartupTaskStatus status) =>
        status is StartupTaskStatus.Enabled or StartupTaskStatus.EnabledByPolicy;
}
