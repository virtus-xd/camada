using CamAda.Core.Assistant;
using static CamAda.Core.Tests.Text.Cultures;

namespace CamAda.Core.Tests.Assistant;

public class VoiceprintTests
{
    [Fact]
    public void Same_direction_scores_one() =>
        Assert.Equal(1.0, new Voiceprint("m", [[1, 0, 0], [2, 0, 0]]).Score([3, 0, 0]), 6);

    [Fact]
    public void Orthogonal_scores_zero() =>
        Assert.Equal(0.0, new Voiceprint("m", [[1, 0, 0]]).Score([0, 1, 0]), 6);

    [Fact]
    public void Mean_of_normalized_samples_is_used() =>
        Assert.Equal(1.0, new Voiceprint("m", [[10, 0], [0, 1]]).Score([1, 1]), 6);

    [Fact]
    public void Zero_embedding_scores_zero() =>
        Assert.Equal(0.0, new Voiceprint("m", [[1, 0]]).Score([0, 0]));

    [Fact]
    public void Json_round_trips()
    {
        var print = new Voiceprint("model.onnx", [[0.25f, -1.5f, 3f], [1f, 2f, -0.125f]]);

        var back = Voiceprint.FromJson(print.ToJson(), "model.onnx");

        Assert.NotNull(back);
        Assert.Equal("model.onnx", back.Model);
        Assert.Equal(2, back.Samples.Count);
        Assert.Equal(print.Samples[0], back.Samples[0]);
        Assert.Equal(print.Samples[1], back.Samples[1]);
    }

    [Fact]
    public void Microphone_round_trips_and_is_optional()
    {
        var print = new Voiceprint("m", [[1, 0]], mic: "{0.0.1.00000000}.{abc}");

        Assert.Equal("{0.0.1.00000000}.{abc}", Voiceprint.FromJson(print.ToJson(), "m")!.Mic);
        Assert.Null(Voiceprint.FromJson("""{"model":"m","samples":[[1,0]]}""", "m")!.Mic);
    }

    [Fact]
    public void Other_model_or_broken_json_is_null()
    {
        var json = new Voiceprint("m", [[1, 0]]).ToJson();

        Assert.Null(Voiceprint.FromJson(json, "baska"));
        Assert.Null(Voiceprint.FromJson("{", "m"));
        Assert.Null(Voiceprint.FromJson("""{"model":"m","samples":[]}""", "m"));
        Assert.Null(Voiceprint.FromJson("""{"model":"m","samples":[[1,0],[1,0,0]]}""", "m"));
        Assert.Null(Voiceprint.FromJson("""{"model":"m","samples":[[]]}""", "m"));
        Assert.Null(Voiceprint.FromJson("null", "m"));
    }

    [Fact]
    public void Enrollment_has_five_sentences_and_one_addresses_ada()
    {
        var sentences = VoiceEnrollmentText.Sentences(Tr);

        Assert.Equal(5, sentences.Count);
        Assert.All(sentences, s => Assert.EndsWith(".", s));
        Assert.StartsWith("Ada,", sentences[1]);
    }
}
