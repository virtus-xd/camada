namespace CamAda.Core.Audio;

/// <summary>Ses düğmesi ve göstergedeki hoparlör simgesinin düzeyi.</summary>
public enum SpeakerLevel
{
    Muted,
    Silent,
    Low,
    Medium,
    High,
}

public static class SpeakerLevels
{
    /// <summary>Sessizse Muted; 0,005 altı Silent; 0,34 altı Low; 0,67 altı Medium; üstü High.</summary>
    public static SpeakerLevel For(float volume, bool muted) =>
        muted ? SpeakerLevel.Muted
        : volume < 0.005f ? SpeakerLevel.Silent
        : volume < 0.34f ? SpeakerLevel.Low
        : volume < 0.67f ? SpeakerLevel.Medium
        : SpeakerLevel.High;

    /// <summary>0–1 ses düzeyinin yüzdesi; yarımlar yukarı yuvarlanır, 0–100'e kırpılır.</summary>
    public static int Percent(float volume) =>
        (int)Math.Clamp(Math.Round(volume * 100.0, MidpointRounding.AwayFromZero), 0, 100);
}
