using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class CostTableTests
{
    private static readonly DateOnly Today = new(2026, 11, 25);

    private static UsageReport Usage(
        (string, long)[] prompt, (string, long)[] response, long thoughts = 0, long toolUse = 0) =>
        new(prompt.ToDictionary(p => p.Item1, p => p.Item2), response.ToDictionary(r => r.Item1, r => r.Item2), thoughts, toolUse);

    [Fact]
    public void Live_matches_spike_first_turn() =>
        Assert.Equal(0.00206925m, CostTable.Live(Usage([("TEXT", 1765), ("AUDIO", 1)], [("AUDIO", 48)], thoughts: 37), Today));

    [Fact]
    public void Live_audio_output_dominates() =>
        Assert.Equal(0.012m, CostTable.Live(Usage([], [("AUDIO", 1000)]), Today));

    [Fact]
    public void Live_tool_use_tokens_are_text_input() =>
        Assert.Equal(0.00075m, CostTable.Live(Usage([], [], toolUse: 1000), Today));

    [Fact]
    public void Lookup_free_searches_then_paid() =>
        Assert.Equal(0.0148m, CostTable.Lookup(1000, 200, searchesBeforeThisMonth: 4999, searches: 2));

    [Fact]
    public void Lookup_within_free_searches_costs_only_tokens() =>
        Assert.Equal(0m, CostTable.Lookup(0, 0, searchesBeforeThisMonth: 0, searches: 1));
}
