namespace CamAda.Panels;

/// <summary>
/// Esc'i önce kendisi işlemek isteyen panel içeriği (ör. Hızlı ayarlar'ın liste sayfası ana sayfaya döner). true dönerse
/// panel kapanmaz.
/// </summary>
public interface IPanelEscape
{
    bool HandleEscape();
}
