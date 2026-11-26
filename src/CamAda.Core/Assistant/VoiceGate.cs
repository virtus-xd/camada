using CamAda.Core.Audio;

namespace CamAda.Core.Assistant;

public enum SpeakerVerdict
{
    User,
    Other,
}

/// <summary>
/// Süzgecin yerel çözümleyicisi. Akış hâlinde çağrılır: her parça sırayla bir kez <see cref="IsSpeech"/>'e ve
/// <see cref="HeardName"/>'e verilir.
/// </summary>
public interface IVoiceAnalyzer
{
    /// <summary>Konuşma algılayıcı (VAD) şu an konuşma görüyor mu.</summary>
    bool IsSpeech(short[] chunk);

    /// <summary>"Ada" bu parçada bitti mi.</summary>
    bool HeardName(short[] chunk);

    /// <summary>Ses izine göre karar; ses izi yoksa User.</summary>
    SpeakerVerdict Verify(short[] segment);
}

/// <param name="ChunkSamples">Kararın birimi (100 ms).</param>
/// <param name="LookbackSamples">Konuşma algılanmadan önceki ses (algılayıcı geç fark eder), parçanın başına eklenir.</param>
/// <param name="DecideAfterSamples">Bu kadar konuşmadan sonra karar verilir (Plan 8 ölçümü: 1,0 sn).</param>
/// <param name="RecheckEverySamples">Geçen uzun konuşma bu aralıkla, son bu kadarlık sesle yeniden denetlenir.</param>
/// <param name="NameWindowSamples">"Ada" parçanın başından bu kadar içinde bitmeli (geriye bakış dahil).</param>
/// <param name="MinVerifySamples">Bundan kısa konuşma ses iziyle denetlenmez (gömme güvenilmez).</param>
/// <param name="MinSpeechRms">Bundan kısık konuşma (Ada'nın yankı kırıntısı) yok sayılır.</param>
public sealed record VoiceGateOptions(
    int ChunkSamples = 1600,
    int LookbackSamples = 8000,
    int DecideAfterSamples = 16000,
    int RecheckEverySamples = 16000,
    int NameWindowSamples = 24000,
    int MinVerifySamples = 8000,
    double MinSpeechRms = 0.006);

/// <summary>O anki kip: misafir kipi açık mı, not alınıyor mu (oturum yöneticisi her parçayla verir).</summary>
public readonly record struct GateInput(bool GuestMode, bool NoteActive);

/// <summary>Google'a şimdi gidecek parçalar (sırayla) ve bu itmede başka bir ses duyuldu mu.</summary>
public sealed record GateOutput(IReadOnlyList<short[]> Send, bool OtherVoiceHeard);

/// <summary>
/// Misafir kipinin ses süzgeci (tasarım "Oturumda ses süzgeci"): mikrofonla Google arasında her 100 ms ya olduğu gibi ya da
/// sıfır gider. Konuşmanın başı karar verilene kadar bekletilir; karar "kullanıcı" ise bekleyen kısım hemen gider. Başkasının
/// sesi hiç gitmez. Misafir kipinde yalnız başında "Ada" olan söz geçer ("Ada" kimden gelirse gelsin: yanlış rete karşı
/// güvenlik payı). Saat işlenen örnek sayısıdır (16 kHz).
/// </summary>
public sealed class VoiceGate(IVoiceAnalyzer analyzer, VoiceGateOptions options)
{
    private const int SampleRate = 16000;

    private enum State
    {
        Idle,
        Holding,
        Passing,
        Blocking,
    }

    private readonly Queue<short[]> _lookback = new();
    private readonly List<short[]> _held = [];
    private readonly Queue<short[]> _recent = new();
    private short[] _carry = [];
    private long _clock;
    private long _freeUntil = -1;
    private long _lastNameEnd = -1;
    private State _state;
    private int _heldLookback;
    private long _segStart;
    private int _speech;
    private bool _free;
    private SpeakerVerdict? _initial;
    private int _sinceCheck;

    /// <summary>Bundan sonra <paramref name="within"/> içinde başlayan ilk söz "Ada" demeden geçer (ilk söz, sorudan sonraki yanıt).</summary>
    public void AllowNextWithoutName(TimeSpan within) => _freeUntil = _clock + (long)(within.TotalSeconds * SampleRate);

    public GateOutput Push(short[] samples, GateInput input)
    {
        var send = new List<short[]>();
        var other = false;
        var all = _carry.Length == 0 ? samples : [.. _carry, .. samples];
        var at = 0;
        for (; at + options.ChunkSamples <= all.Length; at += options.ChunkSamples)
        {
            other |= Process(all[at..(at + options.ChunkSamples)], input, send);
        }

        _carry = all[at..];
        return new GateOutput(send, other);
    }

    private bool Process(short[] chunk, GateInput input, List<short[]> send)
    {
        var speech = analyzer.IsSpeech(chunk);
        var name = analyzer.HeardName(chunk);
        _clock += chunk.Length;
        if (name)
        {
            _lastNameEnd = _clock;
        }

        if (_state != State.Idle && !speech)
        {
            var other = _state == State.Holding && Decide(input, ended: true, send);
            _state = State.Idle;
            Idle(chunk, send);
            return other;
        }

        switch (_state)
        {
            case State.Idle when !speech:
                Idle(chunk, send);
                return false;
            case State.Idle:
                Start(chunk);
                return TryDecide(input, send);
            case State.Holding:
                _held.Add(chunk);
                _speech += chunk.Length;
                Remember(chunk);
                return TryDecide(input, send);
            case State.Passing:
                return Pass(chunk, send);
            default:
                send.Add(new short[chunk.Length]);
                return false;
        }
    }

    private void Idle(short[] chunk, List<short[]> send)
    {
        _lookback.Enqueue(chunk);
        while (_lookback.Sum(c => c.Length) > options.LookbackSamples)
        {
            _lookback.Dequeue();
        }

        send.Add(new short[chunk.Length]);
    }

    private void Start(short[] chunk)
    {
        _held.Clear();
        _held.AddRange(_lookback);
        _heldLookback = _lookback.Count;
        var lookbackSamples = _lookback.Sum(c => c.Length);
        _lookback.Clear();
        _held.Add(chunk);
        _recent.Clear();
        Remember(chunk);
        _segStart = _clock - chunk.Length - lookbackSamples;
        _free = _freeUntil >= 0 && _freeUntil >= _segStart;
        _speech = chunk.Length;
        _initial = null;
        _sinceCheck = 0;
        _state = State.Holding;
    }

    private bool TryDecide(GateInput input, List<short[]> send)
    {
        if (_speech < options.DecideAfterSamples || (NeedName(input) && !NameOk() && _speech < options.NameWindowSamples))
        {
            return false;
        }

        return Decide(input, ended: false, send);
    }

    private bool NeedName(GateInput input) => input.GuestMode && !_free && !input.NoteActive;

    private bool NameOk() => _lastNameEnd >= _segStart && _lastNameEnd <= _segStart + options.NameWindowSamples;

    /// <summary>Bekletilen parça için karar; başka bir ses duyulduysa true.</summary>
    private bool Decide(GateInput input, bool ended, List<short[]> send)
    {
        var speechPart = _held.Skip(_heldLookback).SelectMany(c => c).ToArray();
        if (Pcm.Rms(speechPart) < options.MinSpeechRms)
        {
            Block(ended, send); // yankı kırıntısı ya da uzak uğultu: karar yok, rapor yok, hak harcanmaz
            return false;
        }

        var needName = NeedName(input);
        var nameOk = NameOk();
        _free = false;
        if (_freeUntil >= 0 && _freeUntil >= _segStart)
        {
            _freeUntil = -1; // hak bu sözle kullanıldı
        }

        if (needName && !nameOk)
        {
            var other = _speech >= options.MinVerifySamples && Verify() == SpeakerVerdict.Other
                && _speech >= options.DecideAfterSamples;
            Block(ended, send);
            return other;
        }

        if (_speech < options.MinVerifySamples)
        {
            Release(null, ended, send);
            return false;
        }

        switch (Verify())
        {
            case SpeakerVerdict.User:
                Release(SpeakerVerdict.User, ended, send);
                return false;
            case SpeakerVerdict.Other when nameOk:
                Release(SpeakerVerdict.Other, ended, send); // güvenlik payı: "Ada" diyen geçer
                return true;
            default:
                Block(ended, send);
                return _speech >= options.DecideAfterSamples;
        }
    }

    private SpeakerVerdict Verify() => analyzer.Verify(_held.SelectMany(c => c).ToArray());

    private void Release(SpeakerVerdict? initial, bool ended, List<short[]> send)
    {
        send.AddRange(_held);
        _held.Clear();
        _initial = initial;
        _sinceCheck = 0;
        _state = ended ? State.Idle : State.Passing;
    }

    private void Block(bool ended, List<short[]> send)
    {
        foreach (var chunk in _held.Skip(_heldLookback))
        {
            send.Add(new short[chunk.Length]); // geriye bakış zaten sıfır gitmişti
        }

        _held.Clear();
        _state = ended ? State.Idle : State.Blocking;
    }

    /// <summary>Geçen parçada: ilk karar kullanıcıysa her aralıkta son sesle yeniden denetlenir; başkasıysa o andan engellenir.</summary>
    private bool Pass(short[] chunk, List<short[]> send)
    {
        Remember(chunk);
        _sinceCheck += chunk.Length;
        if (_initial == SpeakerVerdict.User && _sinceCheck >= options.RecheckEverySamples)
        {
            _sinceCheck = 0;
            if (analyzer.Verify(_recent.SelectMany(c => c).ToArray()) == SpeakerVerdict.Other)
            {
                _state = State.Blocking;
                send.Add(new short[chunk.Length]);
                return true;
            }
        }

        send.Add(chunk);
        return false;
    }

    private void Remember(short[] chunk)
    {
        _recent.Enqueue(chunk);
        while (_recent.Sum(c => c.Length) > options.RecheckEverySamples)
        {
            _recent.Dequeue();
        }
    }
}
