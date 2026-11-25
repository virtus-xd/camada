using System.Text.Json.Nodes;

namespace CamAda.Core.Assistant;

/// <summary>Bir Gemini Live oturumunun kurulum bilgileri.</summary>
public sealed record LiveSetupOptions(string Model, string Voice, string SystemInstruction, JsonArray FunctionDeclarations);

/// <summary>İşlevin sonucu; modele "toolResponse" olarak geri gönderilir.</summary>
public sealed record ToolResult(string? Id, string Name, string Result);

/// <summary>
/// Gemini Live kurulum iletisi (denemede doğrulandı): yalnız sesli yanıt, seçilen ses, talimat, bizim işlevlerimiz,
/// iki yönlü yazılı döküm, kayan pencereli bağlam sıkıştırma ve Google'ın konuşma algısı. 3.8 Live düşünme bütçesini ve
/// düzeyini 400 ile reddettiği için düşünme yalnız gizlenir; Live'ın kendi Google araması eklenmez (model onu değil
/// "güncel bilgi" işlevini kullanmalı).
/// </summary>
public static class LiveSetup
{
    public static JsonObject Build(LiveSetupOptions o) => new()
    {
        ["setup"] = new JsonObject
        {
            ["model"] = o.Model,
            ["generationConfig"] = new JsonObject
            {
                ["responseModalities"] = new JsonArray("AUDIO"),
                ["speechConfig"] = new JsonObject
                {
                    ["voiceConfig"] = new JsonObject
                    {
                        ["prebuiltVoiceConfig"] = new JsonObject { ["voiceName"] = o.Voice },
                    },
                },
                ["thinkingConfig"] = new JsonObject { ["includeThoughts"] = false },
            },
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = o.SystemInstruction }),
            },
            ["tools"] = new JsonArray(new JsonObject { ["functionDeclarations"] = o.FunctionDeclarations.DeepClone() }),
            ["inputAudioTranscription"] = new JsonObject(),
            ["outputAudioTranscription"] = new JsonObject(),
            ["contextWindowCompression"] = new JsonObject { ["slidingWindow"] = new JsonObject() },
            ["realtimeInputConfig"] = new JsonObject { ["automaticActivityDetection"] = new JsonObject() },
        },
    };
}

/// <summary>İstemcinin Live'a gönderdiği ileti biçimleri.</summary>
public static class LiveMessages
{
    /// <summary>16 kHz mono 16 bit PCM parçası.</summary>
    public static JsonObject Audio(ReadOnlySpan<byte> pcm16k) => new()
    {
        ["realtimeInput"] = new JsonObject
        {
            ["audio"] = new JsonObject
            {
                ["data"] = Convert.ToBase64String(pcm16k),
                ["mimeType"] = "audio/pcm;rate=16000",
            },
        },
    };

    public static JsonObject ToolResponse(IReadOnlyList<ToolResult> results)
    {
        var responses = new JsonArray();
        foreach (var result in results)
        {
            var response = new JsonObject
            {
                ["name"] = result.Name,
                ["response"] = new JsonObject { ["sonuc"] = result.Result },
            };
            if (result.Id is not null)
            {
                response["id"] = result.Id;
            }

            responses.Add(response);
        }

        return new JsonObject { ["toolResponse"] = new JsonObject { ["functionResponses"] = responses } };
    }
}
