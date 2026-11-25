using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CamAda.Core.Assistant;

namespace CamAda.Assistant;

/// <summary>Güncel bilgi aramasının sonucu; <see cref="Searched"/> Google'da gerçekten arandıysa true.</summary>
public sealed record LookupResult(bool Ok, string Text, bool Searched, long PromptTokens, long OutputTokens, AssistantError Error);

/// <summary>
/// Zamanla değişen bilgileri (döviz, altın, hava, maç…) ayrı bir istekte Google aramasıyla getirir (REST
/// <c>generateContent</c> + <c>google_search</c>). Live modelinin kendi araması denemede kullanılmadığı için Ada bunu
/// "guncel_bilgi" işleviyle çağırır. Anahtar başlıkla gider; hata metinlerinde yer almaz.
/// </summary>
internal sealed class CurrentInfoClient(Func<string?> apiKey, Func<string> model)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public async Task<LookupResult> AskAsync(string question, CancellationToken ct)
    {
        if (apiKey() is not { Length: > 0 } key || !ApiKeyText.IsWellFormed(key))
        {
            return Failed(AssistantError.InvalidKey);
        }

        var body = new JsonObject
        {
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(new JsonObject
                {
                    ["text"] = $"Bugün {DateTime.Now.ToString("dd MMMM yyyy", Turkish)}. Google'da arayarak şu soruya güncel, " +
                               $"kesin ve kısa yanıt ver (sayılarla, tarih ve kaynak adıyla): {question}",
                }),
            }),
            ["tools"] = new JsonArray(new JsonObject { ["google_search"] = new JsonObject() }),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model())}:generateContent")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-goog-api-key", key);

        try
        {
            using var response = await Http.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                return Failed(LiveErrors.FromHttp((int)response.StatusCode, text));
            }

            return Parse(text);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Failed(AssistantError.Network);
        }
    }

    private static LookupResult Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var candidate = root.GetProperty("candidates")[0];
            var answer = string.Concat(candidate.GetProperty("content").GetProperty("parts").EnumerateArray()
                .Select(p => p.TryGetProperty("text", out var t) ? t.GetString() : "")).Trim();
            var searched = candidate.TryGetProperty("groundingMetadata", out var grounding)
                           && grounding.TryGetProperty("webSearchQueries", out var queries)
                           && queries.GetArrayLength() > 0;
            long Count(string field) =>
                root.TryGetProperty("usageMetadata", out var usage) && usage.TryGetProperty(field, out var n) && n.TryGetInt64(out var v) ? v : 0;
            return new LookupResult(answer.Length > 0, answer, searched, Count("promptTokenCount"),
                Count("candidatesTokenCount") + Count("thoughtsTokenCount"), answer.Length > 0 ? AssistantError.None : AssistantError.Unknown);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            return Failed(AssistantError.Unknown);
        }
    }

    private static LookupResult Failed(AssistantError error) => new(false, "", false, 0, 0, error);
}
