using System.Text.Json.Serialization;

namespace CamAda.Core.Settings;

/// <summary>Plak çaların konumu ve ölçüleri (spec §7.8; DIP). Hepsi ayardan değişir.</summary>
public sealed record RecordSettings
{
    /// <summary>320 DIP plakta etiket 108 DIP (spec §7.8: "çapın ~1/3'ü").</summary>
    public const double DefaultLabelRatio = 0.3375;

    public bool Enabled { get; init; } = true;

    /// <summary>Plak çapı; 160–640.</summary>
    public double Diameter { get; init; } = 320;

    /// <summary>Çapın ekranın sol kenarından dışarıda kalan oranı; 0–0,8.</summary>
    public double OutsideFraction { get; init; } = 0.45;

    /// <summary>Plağın alt kenarının ekranın altına uzaklığı; 0–600.</summary>
    public double BottomOffset { get; init; } = 70;

    /// <summary>Etiket (kapak) çapı; null: çap × 0,3375. 40 ile çap × 0,6 arası.</summary>
    public double? LabelDiameter { get; init; }

    [JsonIgnore]
    public double EffectiveLabelDiameter => LabelDiameter ?? Math.Round(Diameter * DefaultLabelRatio);
}
