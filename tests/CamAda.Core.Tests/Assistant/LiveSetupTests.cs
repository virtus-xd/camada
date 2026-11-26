using System.Text.Json.Nodes;
using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class LiveSetupTests
{
    private static JsonObject Setup()
    {
        var functions = new JsonArray(new JsonObject { ["name"] = "ses_ayarla", ["description"] = "d" });
        return LiveSetup.Build(new LiveSetupOptions("models/gemini-3.8-live", "Kore", "talimat", functions))["setup"]!.AsObject();
    }

    [Fact]
    public void Setup_carries_model_voice_and_tools()
    {
        var setup = Setup();
        Assert.Equal("models/gemini-3.8-live", (string?)setup["model"]);
        Assert.Equal("Kore",
            (string?)setup["generationConfig"]!["speechConfig"]!["voiceConfig"]!["prebuiltVoiceConfig"]!["voiceName"]);
        Assert.Equal("AUDIO", (string?)setup["generationConfig"]!["responseModalities"]![0]);
        Assert.Equal("talimat", (string?)setup["systemInstruction"]!["parts"]![0]!["text"]);
        Assert.Equal("ses_ayarla", (string?)setup["tools"]![0]!["functionDeclarations"]![0]!["name"]);
    }

    [Fact]
    public void Setup_has_transcription_and_compression()
    {
        var setup = Setup();
        Assert.NotNull(setup["inputAudioTranscription"]);
        Assert.NotNull(setup["outputAudioTranscription"]);
        Assert.NotNull(setup["contextWindowCompression"]!["slidingWindow"]);
        Assert.NotNull(setup["realtimeInputConfig"]!["automaticActivityDetection"]);
    }

    [Fact]
    public void Setup_never_sends_thinking_budget_or_level()
    {
        var setup = Setup();
        var json = setup.ToJsonString();
        Assert.DoesNotContain("thinkingBudget", json);
        Assert.DoesNotContain("thinkingLevel", json);
        Assert.False((bool)setup["generationConfig"]!["thinkingConfig"]!["includeThoughts"]!);
    }

    [Fact]
    public void Setup_has_no_builtin_google_search()
    {
        var json = Setup().ToJsonString();
        Assert.DoesNotContain("googleSearch", json);
        Assert.DoesNotContain("google_search", json);
        Assert.Single(Setup()["tools"]!.AsArray());
    }

    [Fact]
    public void Audio_message_is_base64_pcm_16k()
    {
        var audio = LiveMessages.Audio([0x01, 0x02])["realtimeInput"]!["audio"]!;
        Assert.Equal("AQI=", (string?)audio["data"]);
        Assert.Equal("audio/pcm;rate=16000", (string?)audio["mimeType"]);
    }

    [Fact]
    public void Text_goes_as_realtime_input()
    {
        var message = LiveMessages.Text(AdaInstruction.MaxDurationGoodbye);
        Assert.Equal(AdaInstruction.MaxDurationGoodbye, (string?)message["realtimeInput"]!["text"]);
        Assert.Contains("yeniden çağırabilirsin", AdaInstruction.MaxDurationGoodbye);
    }

    [Fact]
    public void Tool_response_keeps_ids()
    {
        var responses = LiveMessages.ToolResponse(
            [new ToolResult("abc", "ses_ayarla", "tamam"), new ToolResult(null, "kilitle", "tamam")])
            ["toolResponse"]!["functionResponses"]!.AsArray();
        Assert.Equal("abc", (string?)responses[0]!["id"]);
        Assert.Equal("ses_ayarla", (string?)responses[0]!["name"]);
        Assert.Equal("tamam", (string?)responses[0]!["response"]!["sonuc"]);
        Assert.False(responses[1]!.AsObject().ContainsKey("id"));
    }

    [Fact]
    public void Speak_only_setup_has_voice_but_no_tools_or_listening()
    {
        var setup = LiveSetup.BuildSpeakOnly("models/gemini-3.8-live", "Kore", "söyle")["setup"]!.AsObject();

        Assert.Equal("models/gemini-3.8-live", (string?)setup["model"]);
        Assert.Equal("Kore", (string?)setup["generationConfig"]!["speechConfig"]!["voiceConfig"]!["prebuiltVoiceConfig"]!["voiceName"]);
        Assert.Equal("söyle", (string?)setup["systemInstruction"]!["parts"]![0]!["text"]);
        Assert.False(setup.ContainsKey("tools"));
        Assert.False(setup.ContainsKey("inputAudioTranscription"));
        Assert.False(setup.ContainsKey("realtimeInputConfig"));
    }
}
