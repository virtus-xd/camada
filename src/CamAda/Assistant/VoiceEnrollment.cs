using System.IO;
using CamAda.Assistant.Audio;
using CamAda.Core.Assistant;
using CamAda.Core.Audio;

namespace CamAda.Assistant;

/// <summary>Bir tanıtma cümlesinin sonucu: yeterliyse gömmesi.</summary>
internal sealed record SentenceResult(SentenceCheck Check, float[]? Embedding);

/// <summary>
/// Sesini tanıtma (misafir kipi): oturumla aynı mikrofon zinciri (yankı engelleyici, olmazsa düz mikrofon) ki Ada sesi oturumda
/// nasıl duyuyorsa öyle öğrensin. Her cümle en çok 8 sn dinlenir; en az 1 sn konuşmadan sonra 0,8 sn susunca biter. Kayıt
/// bellekte kalır, yalnız gömme döner. Mikrofon olayları ses iş parçacığında gelir.
/// </summary>
internal sealed class VoiceEnrollment : IDisposable
{
    private const int MaxSamples = 16000 * 8;
    private const int EndQuietChunks = 8;
    private const int MinVoicedChunks = 10;

    private readonly object _lock = new();
    private readonly SpeakerEmbedder _embedder;
    private readonly string _vadPath;
    private readonly string _micId;
    private IAdaMic _mic;
    private List<short>? _recording;
    private int _voiced;
    private int _quietRun;
    private TaskCompletionSource? _done;
    private bool _disposed;

    private VoiceEnrollment(IAdaMic mic, string micId, SpeakerEmbedder embedder, string vadPath)
    {
        _mic = mic;
        _micId = micId;
        _embedder = embedder;
        _vadPath = vadPath;
        Wire(mic);
    }

    /// <summary>Tanıtmanın yapıldığı mikrofonun kimliği: ses izine yazılır, oturum başka mikrofonda izi kullanmaz.</summary>
    public string MicId => _micId;

    /// <summary>Mikrofonu açar ve modeli yükler (arka planda, tek COM dairesinde); mikrofon ya da model yoksa null.</summary>
    public static Task<VoiceEnrollment?> OpenAsync(string? micHint, string speakerDir) => Task.Run(() =>
    {
        if (!VoiceModels.Present(speakerDir) || MicDevices.ResolveMic(micHint) is not { } device)
        {
            return null;
        }

        var embedder = new SpeakerEmbedder(Path.Combine(speakerDir, VoiceModels.SpeakerModel));
        try
        {
            IAdaMic mic = MicDevices.DefaultSpeaker() is { } speaker && MicDevices.DspIndexes(device, speaker) is var (capture, render)
                && capture >= 0 && render >= 0
                ? MicFactory.EchoCancelled(capture, render)
                : MicFactory.Plain(device);
            var enrollment = new VoiceEnrollment(mic, device.ID, embedder, Path.Combine(speakerDir, VoiceModels.Vad));
            mic.Start();
            return enrollment;
        }
        catch
        {
            embedder.Dispose();
            throw;
        }
    });

    public async Task<SentenceResult> RecordSentenceAsync(CancellationToken ct)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lock)
        {
            _recording = [];
            _voiced = 0;
            _quietRun = 0;
            _done = done;
        }

        await using (ct.Register(() => done.TrySetCanceled(ct)))
        {
            await done.Task;
        }

        short[] audio;
        lock (_lock)
        {
            audio = _recording?.ToArray() ?? [];
            _recording = null;
        }

        return await Task.Run(() =>
        {
            var speech = SpeechSegments.Speech(audio, _vadPath);
            var check = EnrollmentCheck.Check(speech.Length, Pcm.Rms(speech));
            return new SentenceResult(check, check == SentenceCheck.Ok ? _embedder.Compute(speech) : null);
        }, CancellationToken.None);
    }

    public void Dispose()
    {
        IAdaMic mic;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            mic = _mic;
            _done?.TrySetCanceled();
        }

        mic.Samples -= OnSamples;
        mic.Failed -= OnFailed;
        mic.Dispose();
        _embedder.Dispose();
    }

    private void Wire(IAdaMic mic)
    {
        mic.Samples += OnSamples;
        mic.Failed += OnFailed;
    }

    private void OnSamples(short[] chunk)
    {
        lock (_lock)
        {
            if (_recording is null || _done is null)
            {
                return;
            }

            _recording.AddRange(chunk);
            if (Pcm.Rms(chunk) > EnrollmentCheck.MinRms)
            {
                _voiced++;
                _quietRun = 0;
            }
            else
            {
                _quietRun++;
            }

            if ((_voiced >= MinVoicedChunks && _quietRun >= EndQuietChunks) || _recording.Count >= MaxSamples)
            {
                _done.TrySetResult();
            }
        }
    }

    /// <summary>Yankı engelleyici açılamadı (ör. sanal çıkış cihazı): oturumdaki gibi düz mikrofona geçilir.</summary>
    private void OnFailed(string message)
    {
        IAdaMic failed;
        lock (_lock)
        {
            if (_disposed || !_mic.EchoCancelled)
            {
                return;
            }

            failed = _mic;
        }

        _ = Task.Run(() =>
        {
            failed.Samples -= OnSamples;
            failed.Failed -= OnFailed;
            failed.Dispose();
            if (MicDevices.ById(_micId) is not { } device)
            {
                return;
            }

            var plain = MicFactory.Plain(device);
            lock (_lock)
            {
                if (_disposed)
                {
                    plain.Dispose();
                    return;
                }

                _mic = plain;
            }

            Wire(plain);
            plain.Start();
        });
    }
}
