#if DEBUG
using System.Collections.Concurrent;
using System.IO;
using CamAda.Assistant.Audio;
using CamAda.Core.Assistant;
using CamAda.Core.Audio;
using CamAda.Core.Settings;

namespace CamAda.Assistant;

internal static partial class AdaProbes
{
    private const int Chunk = 1600;

    /// <summary>
    /// Süzgeç denemesi (Plan 8 Görev 4), Google'a hiçbir şey gitmez. <c>--bos-klasor</c>: model yokken çözümleyici kurulmaz (null).
    /// Varsayılan: <c>deneme\tanitma.wav</c>'dan ses izi; <c>kullanici.wav</c> ve <c>misafir.wav</c>'ın 4'er sn'lik parçaları önce
    /// 1 sn arayla, sonra arasız sırayla gerçek süzgeçten geçer. <c>--misafir</c>: kip açık; sona <c>ada.wav</c> da eklenir.
    /// </summary>
    private static async Task FilterTrialAsync(string[] args)
    {
        if (args.Contains("--bos-klasor"))
        {
            var empty = Directory.CreateTempSubdirectory("camada-bos-").FullName;
            using (var none = SherpaVoiceAnalyzer.TryCreate(empty, empty, ex => Log("hata: " + ex.Message)))
            {
                Log($"ada-suzgec-sina --bos-klasor: {(none is null ? "null (model yok, kurulmadı)" : "BEKLENMEDİK: çözümleyici kuruldu")}");
            }

            Directory.Delete(empty);
            return;
        }

        if (new[] { "tanitma", "kullanici", "misafir", "ada" }.FirstOrDefault(n => ReadTrial(n) is null) is { } missing)
        {
            Log($"ada-suzgec-sina: kayıt yok: {missing}.wav");
            return;
        }

        using var analyzer = SherpaVoiceAnalyzer.TryCreate(VoiceModels.Dir, ModelDir, ex => Log("hata: " + ex.Message));
        if (analyzer is null)
        {
            Log("ada-suzgec-sina: çözümleyici kurulamadı");
            return;
        }

        using (var embedder = new SpeakerEmbedder(Path.Combine(VoiceModels.Dir, VoiceModels.SpeakerModel)))
        {
            var enroll = Segments(ReadTrial("tanitma")!, Path.Combine(VoiceModels.Dir, VoiceModels.Vad)).Where(s => s.Length >= 16000);
            analyzer.Voiceprint = new Voiceprint(VoiceModels.SpeakerModel, enroll.Select(s => embedder.Compute(s)).ToList());
        }

        analyzer.Threshold = AssistantSettings.DefaultSpeakerThreshold;
        var guestMode = args.Contains("--misafir");
        var (audio, labels, pieces) = BuildRoom(guestMode);
        var input = new GateInput(guestMode, NoteActive: false);

        // Gönderilen ham parçalar içerikleriyle tanınır (süzgeç kopya verir); zaman = süzgecin o ana kadar işlediği parça.
        var index = new Dictionary<int, int>();
        for (var i = 0; i < audio.Length / Chunk; i++)
        {
            index.TryAdd(Hash(audio.AsSpan(i * Chunk, Chunk)), i);
        }

        var emittedAt = new ConcurrentDictionary<int, int>();
        var reports = new ConcurrentQueue<int>();
        var processed = 0;
        var done = new TaskCompletionSource();
        var total = audio.Length / Chunk;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var options = new VoiceGateOptions(NameOverridesVoice: !args.Contains("--ad-yetmez"));
        using (var filter = new VoiceFilter(new VoiceGate(analyzer, options), output =>
               {
                   var now = Interlocked.Increment(ref processed);
                   foreach (var sent in output.Send)
                   {
                       if (sent.Any(s => s != 0) && index.TryGetValue(Hash(sent), out var at))
                       {
                           emittedAt.TryAdd(at, now);
                       }
                   }

                   if (output.OtherVoiceHeard)
                   {
                       reports.Enqueue(now);
                   }

                   if (now == total)
                   {
                       done.TrySetResult();
                   }
               }, ex => Log("süzgeç hatası: " + ex.Message)))
        {
            for (var i = 0; i < total; i++)
            {
                filter.Post(audio[(i * Chunk)..((i + 1) * Chunk)], input);
            }

            await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromMinutes(2)));
        }

        Log($"ada-suzgec-sina ({(guestMode ? "misafir kipi" : "normal")}, \"Ada\" güvenlik payı {(options.NameOverridesVoice ? "açık" : "kapalı")}): " +
            $"{total / 10.0:F0} sn oda {timer.ElapsedMilliseconds} ms'de işlendi");
        foreach (var (label, name) in new[] { (1, "kullanıcı"), (2, "misafir"), (3, "ada") })
        {
            var speech = Enumerable.Range(0, total).Where(i => labels[i] == label && Pcm.Rms(audio.AsSpan(i * Chunk, Chunk)) >= 0.006).ToList();
            if (speech.Count > 0)
            {
                Log($"  {name}: konuşma {speech.Count / 10.0:F1} sn, gönderilen %{100.0 * speech.Count(emittedAt.ContainsKey) / speech.Count:F0}");
            }
        }

        foreach (var piece in pieces)
        {
            var sentChunks = Enumerable.Range(piece.Start, piece.Length).Where(emittedAt.ContainsKey).ToList();
            var delay = sentChunks.Count == 0 ? "-" : $"{(emittedAt[sentChunks[0]] - piece.Start) * 100} ms";
            Log($"  {piece.Start / 10.0,5:F1} sn {piece.Name,-9} {(piece.Gap ? "aralı " : "arasız")}: gönderilen {sentChunks.Count / 10.0:F1}/{piece.Length / 10.0:F1} sn, ilk gönderim {delay}");
        }

        Log($"  başka ses raporları: [{string.Join(" ", reports.Select(r => (r / 10.0).ToString("F1", Inv)))}] sn");

        // "Ada, …" cümleleri: 1 sn'den uzun sessizlikle ayrılan konuşma kümeleri.
        var sentences = new List<List<int>>();
        var gap = 10;
        for (var i = 0; i < total; i++)
        {
            var speaking = labels[i] == 3 && Pcm.Rms(audio.AsSpan(i * Chunk, Chunk)) >= 0.006;
            if (!speaking)
            {
                gap = Math.Min(gap + 1, 10);
                continue;
            }

            if (gap >= 10)
            {
                sentences.Add([]);
            }

            sentences[^1].Add(i);
            gap = 0;
        }

        if (sentences.Count > 0)
        {
            Log($"  \"Ada\" cümleleri: {sentences.Count(s => s.Any(emittedAt.ContainsKey))}/{sentences.Count} gönderildi; " +
                string.Join(" ", sentences.Select(s => $"{s[0] / 10.0:F1}sn:%{100.0 * s.Count(emittedAt.ContainsKey) / s.Count:F0}")));
        }
    }

    /// <summary>
    /// Sesini tanıtma (Plan 8 Görev 8), yalnız sahte odayla: VoiceEnrollment beş cümleyi oturum mikrofon zincirinden kaydeder,
    /// her cümlenin denetimi yazılır; çıkan ses izi <c>--puanla=wav</c> dosyalarıyla karşılaştırılır. Hiçbir şey kaydedilmez.
    /// </summary>
    private static async Task EnrollTrialAsync(string[] args)
    {
        if (!FakeRoom.Active)
        {
            Log("ada-tanitma-sina: CAMADA_ADA_FAKE_ROOM yok (gerçek mikrofon açılmaz, deneme çıkıyor)");
            return;
        }

        using var enrollment = await VoiceEnrollment.OpenAsync(Settings().Microphone, VoiceModels.Dir);
        if (enrollment is null)
        {
            Log("ada-tanitma-sina: tanıtma açılamadı");
            return;
        }

        var embeddings = new List<float[]>();
        for (var i = 0; i < VoiceEnrollmentText.Sentences.Count; i++)
        {
            var result = await enrollment.RecordSentenceAsync(CancellationToken.None);
            Log($"cümle {i + 1}: {result.Check}, oda {FakeRoom.Now() / 16000.0:F1} sn");
            if (result.Embedding is { } embedding)
            {
                embeddings.Add(embedding);
            }
        }

        if (embeddings.Count == 0)
        {
            Log("ada-tanitma-sina: hiç geçerli cümle yok");
            return;
        }

        var print = new Voiceprint(VoiceModels.SpeakerModel, embeddings);
        using var embedder = new SpeakerEmbedder(Path.Combine(VoiceModels.Dir, VoiceModels.SpeakerModel));
        foreach (var scoreArg in args.Where(a => a.StartsWith("--puanla=", StringComparison.Ordinal)))
        {
            var file = scoreArg["--puanla=".Length..];
            var scores = Segments(ReadWav(file), Path.Combine(VoiceModels.Dir, VoiceModels.Vad))
                .Where(s => s.Length >= 8000).Select(s => print.Score(embedder.Compute(s)).ToString("F2", Inv));
            Log($"puan {Path.GetFileName(file)}: [{string.Join(" ", scores)}]");
        }
    }

    private sealed record RoomPiece(string Name, int Start, int Length, bool Gap);

    /// <summary>Kullanıcı ve misafir 4'er sn sırayla: ilk beş tur 1 sn arayla, sonraki beş arasız; kipte ada.wav sonda.</summary>
    private static (short[] Audio, int[] Labels, List<RoomPiece> Pieces) BuildRoom(bool withAda)
    {
        var user = ReadTrial("kullanici")!;
        var guest = ReadTrial("misafir")!;
        var audio = new List<short>();
        var labels = new List<int>();
        var pieces = new List<RoomPiece>();
        void Add(short[] source, int from, int count, int label, string name, bool gap)
        {
            count = Math.Max(0, Math.Min(count, source.Length - from));
            count -= count % Chunk;
            if (count == 0)
            {
                return;
            }

            pieces.Add(new RoomPiece(name, audio.Count / Chunk, count / Chunk, gap));
            audio.AddRange(source.AsSpan(from, count).ToArray());
            labels.AddRange(Enumerable.Repeat(label, count / Chunk));
        }

        void Silence(int chunks)
        {
            audio.AddRange(new short[chunks * Chunk]);
            labels.AddRange(new int[chunks]);
        }

        Silence(10);
        for (var round = 0; round < 10; round++)
        {
            var gap = round < 5;
            Add(user, round * 64000, 64000, 1, "kullanıcı", gap);
            if (gap)
            {
                Silence(10);
            }

            Add(guest, round * 64000, 64000, 2, "misafir", gap);
            if (gap)
            {
                Silence(10);
            }
        }

        if (withAda)
        {
            Silence(20);
            var ada = ReadTrial("ada")!;
            Add(ada, 0, ada.Length, 3, "ada", gap: true);
        }

        Silence(20);
        return (audio.ToArray(), labels.ToArray(), pieces);
    }

    private static int Hash(ReadOnlySpan<short> samples)
    {
        var hash = new HashCode();
        foreach (var s in samples)
        {
            hash.Add(s);
        }

        return hash.ToHashCode();
    }
}
#endif
