#if DEBUG
using System.Globalization;
using System.IO;
using System.Text;
using CamAda.Assistant.Audio;
using CamAda.Core.Assistant;
using CamAda.Core.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using SherpaOnnx;

namespace CamAda.Assistant;

internal static partial class AdaProbes
{
    private static readonly string TrialDir = Path.Combine(LocalDir, "deneme");
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Misafir kipi ölçümü (Plan 8 Görev 1). Hiçbir şey Google'a gitmez; kayıtlar yalnız <c>%LOCALAPPDATA%\CamAda\deneme</c>'de.
    /// <c>--kaydet=&lt;ad&gt; [--sure=sn]</c>: oturumdaki gibi (yankı engelleyiciyle) kaydeder; başında uyanma, sonunda hata bipi.
    /// <c>--yanki</c>: hoparlörden İngilizce konuşma çalarken kaydeder (Ada'nın yankısı). <c>--olc</c>: kayıtları her konuşmacı
    /// modeliyle puanlar ve "Ada" algılayıcıyı sayar.
    /// </summary>
    private static async Task SpeakerTrialAsync(string[] args)
    {
        if (args.FirstOrDefault(a => a.StartsWith("--kaydet=", StringComparison.Ordinal)) is { } record)
        {
            var name = record["--kaydet=".Length..];
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z0-9-]+$"))
            {
                Log("ada-konusmaci-sina: kayıt adı yalnız küçük harf, rakam ve tire");
                return;
            }

            await RecordTrialAsync(name, TimeSpan.FromSeconds(Seconds(args, 40)), playDuring: null);
            return;
        }

        if (args.Contains("--yanki"))
        {
            await EchoTrialAsync();
            return;
        }

        if (args.Contains("--olc") && args.Contains("--sadece-ad"))
        {
            MeasureNameSweep();
            return;
        }

        if (args.Contains("--olc") && args.Contains("--ayrinti"))
        {
            MeasureDetail();
            return;
        }

        if (args.Contains("--olc"))
        {
            MeasureTrial();
            return;
        }

        Log("ada-konusmaci-sina: --kaydet=<ad> [--sure=sn] | --yanki | --olc");
    }

    /// <summary>Oturum mikrofon zinciri (yankı engelleyici, olmazsa düz) ile 16 kHz mono WAV kaydı.</summary>
    private static async Task RecordTrialAsync(string name, TimeSpan duration, Func<MMDevice, Task>? playDuring)
    {
        var mic = MicDevices.ResolveMic(Settings().Microphone);
        var speaker = MicDevices.DefaultSpeaker();
        if (mic is null || speaker is null)
        {
            Log($"ada-konusmaci-sina: cihaz yok (mikrofon {mic?.FriendlyName ?? "-"}, hoparlör {speaker?.FriendlyName ?? "-"})");
            return;
        }

        Directory.CreateDirectory(TrialDir);
        var path = Path.Combine(TrialDir, name + ".wav");
        var samples = new List<short>();
        var gate = new object();
        void OnSamples(short[] chunk)
        {
            lock (gate)
            {
                samples.AddRange(chunk);
            }
        }

        using (var cue = new AdaSpeaker(speaker))
        {
            cue.PlayCue(AdaCue.Wake);
            await Task.Delay(400);
        }

        var (capture, render) = MicDevices.DspIndexes(mic, speaker);
        IAdaMic active = new EchoCancelledMic(capture, render);
        var failed = new TaskCompletionSource<string>();
        active.Samples += OnSamples;
        active.Failed += message => failed.TrySetResult(message);
        active.Start();
        var echoCancelled = true;
        var started = DateTime.UtcNow;
        var play = playDuring?.Invoke(speaker) ?? Task.CompletedTask;
        if (await Task.WhenAny(failed.Task, Task.Delay(duration)) == failed.Task)
        {
            Log("yankı engelleyici açılamadı (" + failed.Task.Result + "): düz mikrofonla sürüyor");
            active.Dispose();
            echoCancelled = false;
            active = new PlainMic(mic);
            active.Samples += OnSamples;
            active.Start();
            var rest = duration - (DateTime.UtcNow - started);
            if (rest > TimeSpan.Zero)
            {
                await Task.Delay(rest);
            }
        }

        await play;
        active.Dispose();
        using (var cue = new AdaSpeaker(speaker))
        {
            cue.PlayCue(AdaCue.Error);
            await Task.Delay(400);
        }

        short[] audio;
        lock (gate)
        {
            audio = samples.ToArray();
        }

        using (var writer = new WaveFileWriter(path, new WaveFormat(16000, 16, 1)))
        {
            writer.Write(Pcm.ToBytes(audio), 0, audio.Length * 2);
        }

        Log($"ada-konusmaci-sina: {name}.wav {audio.Length / 16000.0:F1} sn, RMS {Pcm.Rms(audio):F3}, mikrofon {mic.FriendlyName}, " +
            $"yankı engelli {echoCancelled.ToString().ToLowerInvariant()}");
    }

    /// <summary>Windows TTS ile ~10 sn İngilizce konuşma hoparlörden çalarken kayıt: Ada'nın yankısının düzeyi ve puanı için.</summary>
    private static async Task EchoTrialAsync()
    {
        using var synth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
        var text = "This is a test of the echo canceller. The assistant is speaking through the loudspeaker while the microphone " +
                   "is open. Nobody else should talk now. The weather today is sunny and the volume is set to thirty percent.";
        using var tts = await synth.SynthesizeTextToStreamAsync(text);
        var wav = new byte[tts.Size];
        using (var input = tts.GetInputStreamAt(0).AsStreamForRead())
        {
            await input.ReadExactlyAsync(wav);
        }

        // Ada'nın sesiyle aynı yol: 24 kHz mono 16 bit, AdaSpeaker cihaza çevirip çalar.
        byte[] pcm24k;
        using (var reader = new WaveFileReader(new MemoryStream(wav)))
        {
            ISampleProvider source = reader.ToSampleProvider();
            if (source.WaveFormat.Channels == 2)
            {
                source = new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(source);
            }

            var wave = new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(source, 24000).ToWaveProvider16();
            using var buffer = new MemoryStream();
            var chunk = new byte[4800];
            int read;
            while ((read = wave.Read(chunk, 0, chunk.Length)) > 0)
            {
                buffer.Write(chunk, 0, read);
            }

            pcm24k = buffer.ToArray();
        }

        var seconds = pcm24k.Length / 48000.0;
        await RecordTrialAsync("yanki", TimeSpan.FromSeconds(seconds + 1), async speaker =>
        {
            using var output = new AdaSpeaker(speaker);
            output.Add(pcm24k);
            while (output.IsPlaying)
            {
                await Task.Delay(50);
            }
        });
    }

    private static void MeasureTrial()
    {
        var speakerDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Speaker");
        var vadPath = Path.Combine(speakerDir, VoiceModels.Vad);
        if (ReadTrial("tanitma") is not { } enrollAudio)
        {
            Log("ada-konusmaci-sina: kayıt yok: tanitma.wav");
            return;
        }

        var files = new[] { "kullanici", "ada", "misafir", "yanki" }
            .Select(n => (Name: n, Audio: ReadTrial(n)))
            .Where(f => f.Audio is not null)
            .ToDictionary(f => f.Name, f => Segments(f.Audio!, vadPath));
        foreach (var missing in new[] { "kullanici", "ada", "misafir", "yanki" }.Where(n => !files.ContainsKey(n)))
        {
            Log($"ada-konusmaci-sina: kayıt yok: {missing}.wav (atlandı)");
        }

        var enroll = Segments(enrollAudio, vadPath).Where(s => s.Length >= 16000).ToList();
        Log($"ölçüm: tanıtma {enroll.Count} parça; " +
            string.Join(", ", files.Select(f => $"{f.Key} {f.Value.Count} parça / {f.Value.Sum(s => s.Length) / 16000.0:F1} sn konuşma")));
        if (files.TryGetValue("yanki", out var echo))
        {
            Log($"yankı: dosya RMS {Pcm.Rms(ReadTrial("yanki")!):F4}; parça RMS'leri [{string.Join(" ", echo.Select(s => Pcm.Rms(s).ToString("F3", Inv)))}]");
        }

        // Süzgecin kısık ses sınırı (MinSpeechRms) için: parçaların ilk 0,8 sn'sinin RMS'i.
        foreach (var (file, segments) in files)
        {
            Log($"RMS {file}: ilk 0,8 sn {Summary(segments.Select(s => Pcm.Rms(s.AsSpan(0, Math.Min(s.Length, 12800)))).ToList())}");
        }

        foreach (var model in Directory.GetFiles(speakerDir, "*.onnx").Where(p => Path.GetFileName(p) != VoiceModels.Vad))
        {
            MeasureModel(model, enroll, files);
        }

        MeasureName(files);
    }

    private static void MeasureModel(string modelPath, List<short[]> enroll, Dictionary<string, List<short[]>> files)
    {
        var name = Path.GetFileName(modelPath);
        using var embedder = new SpeakerEmbedder(modelPath);
        var print = new Voiceprint(name, enroll.Select(s => embedder.Compute(s)).ToList());
        var times = new List<double>();
        double Score(short[] audio)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var embedding = embedder.Compute(audio);
            times.Add(watch.Elapsed.TotalMilliseconds);
            return print.Score(embedding);
        }

        // İlk 0,8 sn (süzgecin ilk kararı) ve 1 sn'lik pencereler (yeniden denetim); 0,5 sn'den kısa parçalar denetlenmez.
        var first = new Dictionary<string, List<double>>();
        var windows = new Dictionary<string, List<double>>();
        foreach (var (file, segments) in files)
        {
            first[file] = segments.Where(s => s.Length >= 8000).Select(s => Score(s[..Math.Min(s.Length, 12800)])).ToList();
            windows[file] = segments.SelectMany(s => Enumerable.Range(0, s.Length / 16000).Select(i => s[(i * 16000)..((i + 1) * 16000)]))
                .Select(Score).ToList();
        }

        Log($"== {name} (boyut {embedder.Dim}); gömme {times.Average():F0} ms ort., {times.Max():F0} ms en çok");
        foreach (var (file, scores) in first)
        {
            Log($"  {file}: ilk 0,8 sn puanları {Summary(scores)}; 1 sn pencereler {Summary(windows[file])}");
        }

        var user = first.GetValueOrDefault("kullanici", []).Concat(first.GetValueOrDefault("ada", [])).ToList();
        var userWindows = windows.GetValueOrDefault("kullanici", []).Concat(windows.GetValueOrDefault("ada", [])).ToList();
        var guest = first.GetValueOrDefault("misafir", []);
        var guestWindows = windows.GetValueOrDefault("misafir", []);
        var echo = first.GetValueOrDefault("yanki", []);
        for (var t = 0.30; t <= 0.701; t += 0.05)
        {
            Log($"  eşik {t:F2}: kullanıcı geçer %{Percent(user, s => s >= t)} (pencere %{Percent(userWindows, s => s >= t)}), " +
                $"misafir durur %{Percent(guest, s => s < t)} (pencere %{Percent(guestWindows, s => s < t)}), " +
                $"yankı misafir sayılır {echo.Count(s => s < t)}/{echo.Count}");
        }
    }

    /// <summary>
    /// Ayrıntı: iki iyi modelde parça parça uzunluk, RMS ve 0,8/1,0/1,2 sn puanları; "Ada" algılayıcının yazım ve eşik taraması.
    /// </summary>
    private static void MeasureDetail()
    {
        var speakerDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Speaker");
        var vadPath = Path.Combine(speakerDir, VoiceModels.Vad);
        var enroll = Segments(ReadTrial("tanitma")!, vadPath).Where(s => s.Length >= 16000).ToList();
        var files = new[] { "kullanici", "ada", "misafir" }.ToDictionary(n => n, n => Segments(ReadTrial(n)!, vadPath));
        foreach (var model in new[] { "3dspeaker_speech_campplus_sv_zh_en_16k-common_advanced.onnx", "3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx" })
        {
            using var embedder = new SpeakerEmbedder(Path.Combine(speakerDir, model));
            var print = new Voiceprint(model, enroll.Select(s => embedder.Compute(s)).ToList());
            Log($"== {model}");
            foreach (var (file, segments) in files)
            {
                var rows = segments.Select(s =>
                {
                    string At(int n) => s.Length >= n ? print.Score(embedder.Compute(s.AsSpan(0, n))).ToString("F2", Inv) : "  - ";
                    return $"{s.Length / 16000.0:F1}s/{Pcm.Rms(s):F3}: {At(12800)} {At(16000)} {At(19200)} tam {print.Score(embedder.Compute(s)):F2}";
                });
                Log($"  {file}: " + string.Join(" | ", rows));
            }
        }

        var variants = new Dictionary<string, string[]>
        {
            ["A D A"] = ["▁A D A"],
            ["+E D A"] = ["▁A D A", "▁E D A"],
            ["+A DER"] = ["▁A D A", "▁A DER"],
            ["+OH D A"] = ["▁A D A", "▁OH D A"],
            ["hepsi"] = ["▁A D A", "▁E D A", "▁A DER", "▁OH D A"],
        };
        foreach (var (label, tokens) in variants)
        {
            foreach (var threshold in new[] { 0.10, 0.15, 0.20 })
            {
                foreach (var boost in new[] { 1.5, 2.0, 3.0 })
                {
                    var lines = string.Join(Environment.NewLine, tokens.Select(t => string.Create(Inv, $"{t} :{boost} #{threshold} @ADA")));
                    Log($"\"Ada\" {label} eşik {threshold:F2} güç {boost:F1}: {CountName(lines, threshold, boost)}");
                }
            }
        }
    }

    /// <summary>"Ada" algılayıcı için düşük eşik taraması (yakalama zamanları ve öbür kayıtlarda yanlış tetiklenme).</summary>
    private static void MeasureNameSweep()
    {
        foreach (var threshold in new[] { 0.03, 0.05, 0.07, 0.10 })
        {
            foreach (var boost in new[] { 1.5, 2.0, 2.5, 3.0 })
            {
                Log($"\"Ada\" eşik {threshold:F2} güç {boost:F1}: {CountName(WakeWord.NameKeywordLine(threshold, boost), threshold, boost)}");
            }
        }
    }

    private static string CountName(string keywordLines, double threshold, double boost)
    {
        var keywordsPath = Path.Combine(TrialDir, "ada-ad-olcum.txt");
        File.WriteAllText(keywordsPath, keywordLines + Environment.NewLine, new UTF8Encoding(false));
        var config = new KeywordSpotterConfig();
        config.FeatConfig.SampleRate = 16000;
        config.FeatConfig.FeatureDim = 80;
        config.ModelConfig.Transducer.Encoder = Path.Combine(ModelDir, "encoder-epoch-12-avg-2-chunk-16-left-64.int8.onnx");
        config.ModelConfig.Transducer.Decoder = Path.Combine(ModelDir, "decoder-epoch-12-avg-2-chunk-16-left-64.int8.onnx");
        config.ModelConfig.Transducer.Joiner = Path.Combine(ModelDir, "joiner-epoch-12-avg-2-chunk-16-left-64.int8.onnx");
        config.ModelConfig.Tokens = Path.Combine(ModelDir, "tokens.txt");
        config.ModelConfig.Provider = "cpu";
        config.ModelConfig.NumThreads = 1;
        config.KeywordsFile = keywordsPath;
        config.KeywordsThreshold = (float)threshold;
        config.KeywordsScore = (float)boost;
        using var spotter = new KeywordSpotter(config);
        var counts = new List<string>();
        foreach (var name in new[] { "ada", "kullanici", "misafir" })
        {
            var audio = ReadTrial(name)!;
            using var stream = spotter.CreateStream();
            var hits = new List<double>();
            for (var at = 0; at < audio.Length; at += 1600)
            {
                var chunk = audio[at..Math.Min(audio.Length, at + 1600)];
                stream.AcceptWaveform(16000, chunk.Select(s => s / 32768f).ToArray());
                while (spotter.IsReady(stream))
                {
                    spotter.Decode(stream);
                    if (!string.IsNullOrEmpty(spotter.GetResult(stream).Keyword))
                    {
                        hits.Add((at + 1600) / 16000.0);
                        spotter.Reset(stream);
                    }
                }
            }

            counts.Add(name == "ada"
                ? $"ada {hits.Count} [{string.Join(" ", hits.Select(h => h.ToString("F1", Inv)))}]"
                : $"{name} {hits.Count}");
        }

        File.Delete(keywordsPath);
        return string.Join(", ", counts);
    }

    /// <summary>"Ada" algılayıcı: ada.wav'da kaç cümle yakalanıyor, öbür kayıtlarda dakikada kaç yanlış tetiklenme.</summary>
    private static void MeasureName(Dictionary<string, List<short[]>> files)
    {
        foreach (var threshold in new[] { 0.20, 0.25, 0.30 })
        {
            foreach (var boost in new[] { 1.2, 1.5 })
            {
                var line = string.Create(Inv, $"▁A D A :{boost} #{threshold} @ADA");
                var keywordsPath = Path.Combine(TrialDir, "ada-ad-olcum.txt");
                File.WriteAllText(keywordsPath, line + Environment.NewLine, new UTF8Encoding(false));
                var config = new KeywordSpotterConfig();
                config.FeatConfig.SampleRate = 16000;
                config.FeatConfig.FeatureDim = 80;
                config.ModelConfig.Transducer.Encoder = Path.Combine(ModelDir, "encoder-epoch-12-avg-2-chunk-16-left-64.int8.onnx");
                config.ModelConfig.Transducer.Decoder = Path.Combine(ModelDir, "decoder-epoch-12-avg-2-chunk-16-left-64.int8.onnx");
                config.ModelConfig.Transducer.Joiner = Path.Combine(ModelDir, "joiner-epoch-12-avg-2-chunk-16-left-64.int8.onnx");
                config.ModelConfig.Tokens = Path.Combine(ModelDir, "tokens.txt");
                config.ModelConfig.Provider = "cpu";
                config.ModelConfig.NumThreads = 1;
                config.KeywordsFile = keywordsPath;
                config.KeywordsThreshold = (float)threshold;
                config.KeywordsScore = (float)boost;
                using var spotter = new KeywordSpotter(config);
                var counts = new List<string>();
                foreach (var name in new[] { "ada", "kullanici", "misafir" })
                {
                    if (ReadTrial(name) is not { } audio)
                    {
                        continue;
                    }

                    using var stream = spotter.CreateStream();
                    var hits = 0;
                    for (var at = 0; at < audio.Length; at += 1600)
                    {
                        var chunk = audio[at..Math.Min(audio.Length, at + 1600)];
                        stream.AcceptWaveform(16000, chunk.Select(s => s / 32768f).ToArray());
                        while (spotter.IsReady(stream))
                        {
                            spotter.Decode(stream);
                            if (!string.IsNullOrEmpty(spotter.GetResult(stream).Keyword))
                            {
                                hits++;
                                spotter.Reset(stream);
                            }
                        }
                    }

                    var minutes = audio.Length / 16000.0 / 60;
                    counts.Add(name == "ada" ? $"ada {hits} yakalama" : $"{name} {hits / minutes:F1}/dk");
                }

                File.Delete(keywordsPath);
                Log($"\"Ada\" eşik {threshold:F2} güç {boost:F1}: {string.Join(", ", counts)}");
            }
        }
    }

    private static short[]? ReadTrial(string name)
    {
        var path = Path.Combine(TrialDir, name + ".wav");
        if (!File.Exists(path))
        {
            return null;
        }

        using var reader = new WaveFileReader(path);
        var bytes = new byte[reader.Length];
        var read = reader.Read(bytes, 0, bytes.Length);
        var samples = new short[read / 2];
        Buffer.BlockCopy(bytes, 0, samples, 0, read);
        return samples;
    }

    /// <summary>Silero VAD parçaları (oturumdakiyle aynı ayarlar), 16 bit.</summary>
    private static List<short[]> Segments(short[] audio, string vadPath)
    {
        var config = new VadModelConfig();
        config.SileroVad.Model = vadPath;
        config.SileroVad.Threshold = 0.5f;
        config.SileroVad.MinSilenceDuration = 0.25f;
        config.SileroVad.MinSpeechDuration = 0.25f;
        config.SileroVad.WindowSize = 512;
        config.SileroVad.MaxSpeechDuration = 20f;
        config.SampleRate = 16000;
        config.NumThreads = 1;
        config.Provider = "cpu";
        using var vad = new VoiceActivityDetector(config, 60);
        var segments = new List<short[]>();
        void Drain()
        {
            while (!vad.IsEmpty())
            {
                segments.Add(vad.Front().Samples.Select(f => (short)Math.Clamp(f * 32768f, short.MinValue, short.MaxValue)).ToArray());
                vad.Pop();
            }
        }

        for (var at = 0; at + 512 <= audio.Length; at += 512)
        {
            vad.AcceptWaveform(audio[at..(at + 512)].Select(s => s / 32768f).ToArray());
            Drain();
        }

        vad.Flush();
        Drain();
        return segments;
    }

    private static string Summary(List<double> scores)
    {
        if (scores.Count == 0)
        {
            return "-";
        }

        var sorted = scores.Order().ToList();
        return string.Create(Inv, $"n={sorted.Count} en az {sorted[0]:F2} orta {sorted[sorted.Count / 2]:F2} en çok {sorted[^1]:F2}");
    }

    private static string Percent(List<double> scores, Func<double, bool> ok) =>
        scores.Count == 0 ? "-" : (100.0 * scores.Count(ok) / scores.Count).ToString("F0", Inv);
}
#endif
