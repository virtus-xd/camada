using System.Text.Json;
using System.Text.Json.Nodes;

namespace CamAda.Core.Assistant;

/// <summary>
/// Kullanıcının ses izi (misafir kipi): tanıtmada her cümleden çıkarılan konuşmacı gömmeleri ve hangi modelle alındıkları.
/// Ses kaydı tutulmaz. Puan, normalize edilmiş örneklerin ortalamasıyla kosinüs benzerliğidir (−1..1).
/// </summary>
public sealed class Voiceprint
{
    private readonly float[] _mean;

    public Voiceprint(string model, IReadOnlyList<float[]> samples)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (!Valid(samples))
        {
            throw new ArgumentException("Ses izi en az bir örnek ister ve örneklerin boyu aynı olmalı.", nameof(samples));
        }

        Model = model;
        Samples = samples.Select(s => s.ToArray()).ToList();
        _mean = new float[Samples[0].Length];
        foreach (var sample in Samples)
        {
            var norm = Norm(sample);
            if (norm == 0)
            {
                continue;
            }

            for (var i = 0; i < sample.Length; i++)
            {
                _mean[i] += (float)(sample[i] / norm / Samples.Count);
            }
        }
    }

    /// <summary>Gömmeleri çıkaran konuşmacı modelinin dosya adı; başka modelin gömmesiyle karşılaştırılamaz.</summary>
    public string Model { get; }

    public IReadOnlyList<float[]> Samples { get; }

    /// <summary>Gömmenin ses iziyle kosinüs benzerliği; sıfır vektörde 0.</summary>
    public double Score(ReadOnlySpan<float> embedding)
    {
        if (embedding.Length != _mean.Length)
        {
            throw new ArgumentException("Gömmenin boyu ses iziyle aynı değil.", nameof(embedding));
        }

        double dot = 0;
        for (var i = 0; i < embedding.Length; i++)
        {
            dot += embedding[i] * (double)_mean[i];
        }

        var norms = Norm(embedding) * Norm(_mean);
        return norms == 0 ? 0 : dot / norms;
    }

    public string ToJson()
    {
        var samples = new JsonArray();
        foreach (var sample in Samples)
        {
            samples.Add(new JsonArray(sample.Select(v => (JsonNode?)JsonValue.Create(v)).ToArray()));
        }

        return new JsonObject { ["model"] = Model, ["samples"] = samples }.ToJsonString();
    }

    /// <summary>Bozuk, boş, boyları farklı ya da başka modelle alınmış iz null döner (yeniden tanıtılmalı).</summary>
    public static Voiceprint? FromJson(string json, string expectedModel)
    {
        try
        {
            if (JsonNode.Parse(json) is not JsonObject root
                || root["model"]?.GetValue<string>() is not { } model
                || model != expectedModel
                || root["samples"] is not JsonArray array)
            {
                return null;
            }

            var samples = array.Select(s => s is JsonArray values ? values.Select(v => v!.GetValue<float>()).ToArray() : null).ToList();
            return samples.Any(s => s is null) || !Valid(samples!) ? null : new Voiceprint(model, samples!);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or NullReferenceException)
        {
            return null;
        }
    }

    private static bool Valid(IReadOnlyList<float[]> samples) =>
        samples.Count > 0 && samples[0].Length > 0 && samples.All(s => s.Length == samples[0].Length);

    private static double Norm(ReadOnlySpan<float> values)
    {
        double sum = 0;
        foreach (var v in values)
        {
            sum += v * (double)v;
        }

        return Math.Sqrt(sum);
    }
}
