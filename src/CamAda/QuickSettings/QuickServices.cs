namespace CamAda.QuickSettings;

/// <summary>Hızlı ayarlar panelinin kullandığı servisler (Plan 4.3). Null olanlar bu bilgisayarda kurulamadı.</summary>
public sealed class QuickServices
{
    public required NetworkStatusService Network { get; init; }

    public required BatteryService Battery { get; init; }

    public required ThemeService Theme { get; init; }

    public required IQuickLauncher Launcher { get; init; }

    public RadioService? Radios { get; init; }

    public BrightnessService? Brightness { get; init; }

    public WifiService? Wifi { get; init; }
}
