using CamAda.Core.Audio;

namespace CamAda.Core.Tests.Audio;

public class MicStateTests
{
    [Theory]
    [InlineData(false, false, MicState.Missing)]
    [InlineData(false, true, MicState.Missing)]
    [InlineData(true, true, MicState.Off)]
    [InlineData(true, false, MicState.On)]
    public void State_follows_device_and_mute(bool hasDevice, bool consoleMuted, MicState expected) =>
        Assert.Equal(expected, MicStates.From(hasDevice, consoleMuted));
}
