using System.Text;
using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class LiveEventsTests
{
    private static IReadOnlyList<LiveEvent> Parse(string json) => LiveEvents.Parse(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Setup_complete() =>
        Assert.IsType<LiveEvent.SetupComplete>(Assert.Single(Parse("""{"setupComplete":{}}""")));

    [Fact]
    public void Audio_chunk_decodes_base64()
    {
        var chunk = Assert.IsType<LiveEvent.AudioChunk>(Assert.Single(Parse(
            """{"serverContent":{"modelTurn":{"parts":[{"inlineData":{"mimeType":"audio/pcm;rate=24000","data":"AQI="}}]}}}""")));
        Assert.Equal(new byte[] { 1, 2 }, chunk.Pcm24k);
    }

    [Fact]
    public void Interrupted_comes_first()
    {
        var events = Parse("""{"serverContent":{"inputTranscription":{"text":"dur"},"interrupted":true}}""");
        Assert.Collection(events,
            e => Assert.IsType<LiveEvent.Interrupted>(e),
            e => Assert.Equal("dur", Assert.IsType<LiveEvent.InputText>(e).Text));
    }

    [Fact]
    public void Thought_parts_are_skipped() =>
        Assert.Empty(Parse("""{"serverContent":{"modelTurn":{"parts":[{"text":"**Düşünüyor**","thought":true}]}}}"""));

    [Fact]
    public void Turn_complete_after_text()
    {
        var events = Parse("""{"serverContent":{"turnComplete":true,"outputTranscription":{"text":"Tamam."}}}""");
        Assert.Collection(events,
            e => Assert.Equal("Tamam.", Assert.IsType<LiveEvent.OutputText>(e).Text),
            e => Assert.IsType<LiveEvent.TurnComplete>(e));
    }

    [Fact]
    public void Tool_calls_parse_id_name_args()
    {
        var calls = Assert.IsType<LiveEvent.ToolCalls>(Assert.Single(Parse(
            """{"toolCall":{"functionCalls":[{"id":"f1","name":"ses_ayarla","args":{"yuzde":30}}]}}"""))).Calls;
        var call = Assert.Single(calls);
        Assert.Equal("f1", call.Id);
        Assert.Equal("ses_ayarla", call.Name);
        Assert.Equal(30, (int)call.Args["yuzde"]!);
    }

    [Fact]
    public void Tool_call_without_args_has_empty_object()
    {
        var call = Assert.Single(Assert.IsType<LiveEvent.ToolCalls>(Assert.Single(Parse(
            """{"toolCall":{"functionCalls":[{"name":"kilitle"}]}}"""))).Calls);
        Assert.Null(call.Id);
        Assert.Empty(call.Args);
    }

    [Fact]
    public void Tool_cancellation_lists_ids() =>
        Assert.Equal(["a", "b"],
            Assert.IsType<LiveEvent.ToolCancelled>(Assert.Single(Parse("""{"toolCallCancellation":{"ids":["a","b"]}}"""))).Ids);

    [Fact]
    public void Usage_by_modality()
    {
        var report = Assert.IsType<LiveEvent.Usage>(Assert.Single(Parse("""
            {"usageMetadata":{"promptTokenCount":1766,"responseTokenCount":48,"totalTokenCount":1814,
              "promptTokensDetails":[{"modality":"TEXT","tokenCount":1765},{"modality":"AUDIO","tokenCount":1}],
              "responseTokensDetails":[{"modality":"AUDIO","tokenCount":48}],"thoughtsTokenCount":37}}
            """))).Report;
        Assert.Equal(1765, report.Prompt["TEXT"]);
        Assert.Equal(1, report.Prompt["AUDIO"]);
        Assert.Equal(48, report.Response["AUDIO"]);
        Assert.Equal(37, report.Thoughts);
        Assert.Equal(0, report.ToolUsePrompt);
    }

    [Fact]
    public void Go_away_parses_time_left() =>
        Assert.Equal(TimeSpan.FromSeconds(5),
            Assert.IsType<LiveEvent.GoAway>(Assert.Single(Parse("""{"goAway":{"timeLeft":"5s"}}"""))).TimeLeft);

    [Fact]
    public void Garbage_returns_empty() => Assert.Empty(Parse("{bozuk"));

    [Fact]
    public void Unknown_keys_are_ignored() => Assert.Empty(Parse("""{"sessionResumptionUpdate":{"newHandle":"x"}}"""));
}
