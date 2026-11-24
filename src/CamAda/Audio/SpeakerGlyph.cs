using CamAda.Core.Audio;

namespace CamAda.Audio;

/// <summary>Hoparlör düzeyinin Segoe Fluent Icons glifi.</summary>
public static class SpeakerGlyph
{
    public const string MicOn = "";
    public const string MicOff = "";

    public static string For(SpeakerLevel level) => level switch
    {
        SpeakerLevel.Muted => "",
        SpeakerLevel.Silent => "",
        SpeakerLevel.Low => "",
        SpeakerLevel.Medium => "",
        _ => "",
    };
}
