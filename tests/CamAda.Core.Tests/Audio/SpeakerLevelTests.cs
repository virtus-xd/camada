using CamAda.Core.Audio;

namespace CamAda.Core.Tests.Audio;

public class SpeakerLevelTests
{
    [Theory]
    [InlineData(0.5f, true, SpeakerLevel.Muted)]
    [InlineData(0f, false, SpeakerLevel.Silent)]
    [InlineData(0.004f, false, SpeakerLevel.Silent)]
    [InlineData(0.2f, false, SpeakerLevel.Low)]
    [InlineData(0.34f, false, SpeakerLevel.Medium)]
    [InlineData(0.5f, false, SpeakerLevel.Medium)]
    [InlineData(0.67f, false, SpeakerLevel.High)]
    [InlineData(1f, false, SpeakerLevel.High)]
    public void Level_follows_volume_and_mute(float volume, bool muted, SpeakerLevel expected) =>
        Assert.Equal(expected, SpeakerLevels.For(volume, muted));

    [Theory]
    [InlineData(0.125f, 13)]
    [InlineData(0.555f, 56)]
    [InlineData(1.2f, 100)]
    [InlineData(-0.1f, 0)]
    [InlineData(0.996f, 100)]
    public void Percent_rounds_half_up_and_clamps(float volume, int expected) =>
        Assert.Equal(expected, SpeakerLevels.Percent(volume));
}
