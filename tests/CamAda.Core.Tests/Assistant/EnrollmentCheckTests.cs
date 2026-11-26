using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class EnrollmentCheckTests
{
    [Theory]
    [InlineData(16000, 0.05, SentenceCheck.Ok)]
    [InlineData(8000, 0.05, SentenceCheck.TooShort)]
    [InlineData(16000, 0.005, SentenceCheck.TooQuiet)]
    [InlineData(8000, 0.005, SentenceCheck.TooQuiet)]
    [InlineData(16000, 0.008, SentenceCheck.Ok)]
    public void Check_sentence(int speech, double rms, SentenceCheck expected) =>
        Assert.Equal(expected, EnrollmentCheck.Check(speech, rms));
}
