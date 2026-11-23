namespace CamAda.Core.Startup;

/// <summary>Oturum açılışında başlatma: HKCU\...\Run altındaki değerin adı ve komutu.</summary>
public static class AutostartCommand
{
    public const string RunValueName = "CamAda";

    /// <summary>Exe yolunu tırnaklar (kullanıcı klasöründe boşluk olabilir).</summary>
    public static string Build(string exePath) => $"\"{exePath}\"";
}
