namespace CamAda.Core.Audio;

/// <summary>Varsayılan kayıt cihazının durumu (spec §7.1, §7.4 mikrofon).</summary>
public enum MicState
{
    /// <summary>Kayıt cihazı yok.</summary>
    Missing,
    On,
    Off,
}

public static class MicStates
{
    /// <summary>Durum, konsol rolündeki varsayılan kayıt cihazından okunur.</summary>
    public static MicState From(bool hasDevice, bool consoleMuted) =>
        !hasDevice ? MicState.Missing : consoleMuted ? MicState.Off : MicState.On;
}
