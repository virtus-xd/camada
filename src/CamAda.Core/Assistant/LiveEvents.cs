using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CamAda.Core.Assistant;

/// <summary>Modelin istediği işlev çağrısı.</summary>
public sealed record ToolCall(string? Id, string Name, JsonObject Args);

/// <summary>Bir yanıtın token kullanımı, kip ("TEXT", "AUDIO" …) başına.</summary>
public sealed record UsageReport(
    IReadOnlyDictionary<string, long> Prompt,
    IReadOnlyDictionary<string, long> Response,
    long Thoughts,
    long ToolUsePrompt);

/// <summary>Live sunucusundan gelen bir olay.</summary>
public abstract record LiveEvent
{
    private LiveEvent()
    {
    }

    public sealed record SetupComplete : LiveEvent;

    public sealed record AudioChunk(byte[] Pcm24k) : LiveEvent;

    public sealed record InputText(string Text) : LiveEvent;

    public sealed record OutputText(string Text) : LiveEvent;

    public sealed record Interrupted : LiveEvent;

    public sealed record TurnComplete : LiveEvent;

    public sealed record ToolCalls(IReadOnlyList<ToolCall> Calls) : LiveEvent;

    public sealed record ToolCancelled(IReadOnlyList<string> Ids) : LiveEvent;

    public sealed record Usage(UsageReport Report) : LiveEvent;

    public sealed record GoAway(TimeSpan TimeLeft) : LiveEvent;
}

/// <summary>
/// Live sunucu iletisini olaylara çevirir. Bir iletide birden çok şey olabilir; sıra: kurulum, kesilme, kullanıcının sözü,
/// ses, Ada'nın sözü, yanıt bitti, sonra işlev çağrıları, iptaller, kullanım, ayrılma. Düşünme parçaları atlanır; bozuk
/// ileti boş liste verir (hata atmaz).
/// </summary>
public static class LiveEvents
{
    public static IReadOnlyList<LiveEvent> Parse(ReadOnlySpan<byte> json)
    {
        var events = new List<LiveEvent>();
        try
        {
            using var doc = JsonDocument.Parse(json.ToArray());
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return events;
            }

            if (root.TryGetProperty("setupComplete", out _))
            {
                events.Add(new LiveEvent.SetupComplete());
            }

            if (root.TryGetProperty("serverContent", out var content))
            {
                ParseContent(content, events);
            }

            if (root.TryGetProperty("toolCall", out var toolCall)
                && toolCall.TryGetProperty("functionCalls", out var calls) && calls.ValueKind == JsonValueKind.Array)
            {
                events.Add(new LiveEvent.ToolCalls(calls.EnumerateArray().Select(ParseCall).ToList()));
            }

            if (root.TryGetProperty("toolCallCancellation", out var cancel)
                && cancel.TryGetProperty("ids", out var ids) && ids.ValueKind == JsonValueKind.Array)
            {
                events.Add(new LiveEvent.ToolCancelled(ids.EnumerateArray().Select(i => i.GetString() ?? "").ToList()));
            }

            if (root.TryGetProperty("usageMetadata", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                events.Add(new LiveEvent.Usage(ParseUsage(usage)));
            }

            if (root.TryGetProperty("goAway", out var goAway))
            {
                events.Add(new LiveEvent.GoAway(ParseDuration(goAway.TryGetProperty("timeLeft", out var t) ? t.GetString() : null)));
            }
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException)
        {
            events.Clear();
        }

        return events;
    }

    private static void ParseContent(JsonElement content, List<LiveEvent> events)
    {
        if (content.TryGetProperty("interrupted", out var interrupted) && interrupted.ValueKind == JsonValueKind.True)
        {
            events.Add(new LiveEvent.Interrupted());
        }

        if (Text(content, "inputTranscription") is { } input)
        {
            events.Add(new LiveEvent.InputText(input));
        }

        if (content.TryGetProperty("modelTurn", out var turn) && turn.TryGetProperty("parts", out var parts)
            && parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True)
                {
                    continue;
                }

                if (part.TryGetProperty("inlineData", out var inline) && inline.TryGetProperty("data", out var data)
                    && data.GetString() is { } base64)
                {
                    events.Add(new LiveEvent.AudioChunk(Convert.FromBase64String(base64)));
                }
            }
        }

        if (Text(content, "outputTranscription") is { } output)
        {
            events.Add(new LiveEvent.OutputText(output));
        }

        if (content.TryGetProperty("turnComplete", out var complete) && complete.ValueKind == JsonValueKind.True)
        {
            events.Add(new LiveEvent.TurnComplete());
        }
    }

    private static string? Text(JsonElement content, string name) =>
        content.TryGetProperty(name, out var element) && element.TryGetProperty("text", out var text)
            && text.GetString() is { Length: > 0 } value
            ? value
            : null;

    private static ToolCall ParseCall(JsonElement call)
    {
        var args = call.TryGetProperty("args", out var a) && a.ValueKind == JsonValueKind.Object
            ? JsonNode.Parse(a.GetRawText())!.AsObject()
            : new JsonObject();
        return new ToolCall(
            call.TryGetProperty("id", out var id) ? id.GetString() : null,
            call.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
            args);
    }

    private static UsageReport ParseUsage(JsonElement usage)
    {
        static Dictionary<string, long> Details(JsonElement usage, string field)
        {
            var map = new Dictionary<string, long>();
            if (usage.TryGetProperty(field, out var details) && details.ValueKind == JsonValueKind.Array)
            {
                foreach (var d in details.EnumerateArray())
                {
                    var modality = d.TryGetProperty("modality", out var m) ? m.GetString() ?? "?" : "?";
                    var count = d.TryGetProperty("tokenCount", out var c) && c.TryGetInt64(out var n) ? n : 0;
                    map[modality] = map.GetValueOrDefault(modality) + count;
                }
            }

            return map;
        }

        static long Number(JsonElement usage, string field) =>
            usage.TryGetProperty(field, out var value) && value.TryGetInt64(out var n) ? n : 0;

        return new UsageReport(
            Details(usage, "promptTokensDetails"),
            Details(usage, "responseTokensDetails"),
            Number(usage, "thoughtsTokenCount"),
            Number(usage, "toolUsePromptTokenCount"));
    }

    /// <summary>Google'ın süre biçimi: "5s", "1.5s".</summary>
    private static TimeSpan ParseDuration(string? text) =>
        text is { Length: > 1 } && text.EndsWith('s')
        && double.TryParse(text[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.Zero;
}
