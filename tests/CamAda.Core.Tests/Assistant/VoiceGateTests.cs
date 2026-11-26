using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class VoiceGateTests
{
    // Testler kendi değerleriyle: ölçümle değişen varsayılanlar testleri değiştirmez.
    private static readonly VoiceGateOptions Options = new(
        ChunkSamples: 1600, LookbackSamples: 8000, DecideAfterSamples: 12800, RecheckEverySamples: 16000,
        NameWindowSamples: 24000, MinVerifySamples: 8000, MinSpeechRms: 0.02);

    private const short Quiet = 50;     // konuşma algılayıcı duymaz
    private const short Soft = 300;     // konuşma ama çok kısık (RMS ~0,009)
    private const short User = 1000;
    private const short Other = 2000;
    private const short UserName = 3000;  // kullanıcı "Ada" der
    private const short OtherName = 4000; // başkası "Ada" der

    private static readonly GateInput Normal = new(GuestMode: false, NoteActive: false);
    private static readonly GateInput Guest = new(GuestMode: true, NoteActive: false);

    private readonly FakeAnalyzer _analyzer = new();
    private readonly VoiceGate _gate;

    public VoiceGateTests() => _gate = new VoiceGate(_analyzer, Options);

    /// <summary>Parça tek değerle dolu: değeri kimin konuştuğunu söyler.</summary>
    private sealed class FakeAnalyzer : IVoiceAnalyzer
    {
        public int VerifyCalls { get; private set; }

        public bool IsSpeech(short[] chunk) => Math.Abs((int)chunk[0]) >= 100;

        public bool HeardName(short[] chunk) => chunk[0] is UserName or OtherName;

        public SpeakerVerdict Verify(short[] segment)
        {
            VerifyCalls++;
            var other = segment.Count(s => s is Other or OtherName);
            var user = segment.Count(s => s is User or UserName);
            return other > user ? SpeakerVerdict.Other : SpeakerVerdict.User;
        }
    }

    private List<GateOutput> Push(short value, int chunks, GateInput? input = null)
    {
        var outputs = new List<GateOutput>();
        for (var i = 0; i < chunks; i++)
        {
            outputs.Add(_gate.Push(Enumerable.Repeat(value, 1600).ToArray(), input ?? Normal));
        }

        return outputs;
    }

    private static IEnumerable<short[]> Sent(IEnumerable<GateOutput> outputs) => outputs.SelectMany(o => o.Send);

    private static int Raw(IEnumerable<GateOutput> outputs, short value) => Sent(outputs).Count(a => a.Length > 0 && a[0] == value);

    private static int ZeroSamples(IEnumerable<GateOutput> outputs) => Sent(outputs).Where(a => a.All(s => s == 0)).Sum(a => a.Length);

    private static int Reports(IEnumerable<GateOutput> outputs) => outputs.Count(o => o.OtherVoiceHeard);

    [Fact]
    public void Silence_is_sent_as_zeros()
    {
        var outputs = Push(Quiet, 5);

        Assert.All(outputs, o =>
        {
            var only = Assert.Single(o.Send);
            Assert.Equal(1600, only.Length);
            Assert.All(only, s => Assert.Equal(0, s));
        });
    }

    [Fact]
    public void User_speech_is_held_until_decided_then_sent_with_lookback()
    {
        Push(Quiet, 5);

        var speech = Push(User, 8);
        var next = Push(User, 1);

        Assert.All(speech.Take(7), o => Assert.Empty(o.Send));
        var flushed = speech[7].Send;
        Assert.Equal(13, flushed.Count);
        Assert.All(flushed.Take(5), a => Assert.Equal(Quiet, a[0]));
        Assert.All(flushed.Skip(5), a => Assert.Equal(User, a[0]));
        Assert.Equal(User, Assert.Single(next[0].Send)[0]);
    }

    [Fact]
    public void Other_voice_is_never_sent_and_reported_once()
    {
        var outputs = Push(Other, 20);

        Assert.Equal(0, Raw(outputs, Other));
        Assert.Equal(20 * 1600, ZeroSamples(outputs));
        Assert.Equal(1, Reports(outputs));
    }

    [Fact]
    public void Short_segment_passes_unverified_when_guest_mode_is_off()
    {
        var outputs = Push(User, 3).Concat(Push(Quiet, 1)).ToList();

        Assert.Equal(3, Raw(outputs, User));
        Assert.Equal(0, _analyzer.VerifyCalls);
    }

    [Fact]
    public void Short_other_segment_is_blocked_without_guest_report()
    {
        var outputs = Push(Other, 6).Concat(Push(Quiet, 1)).ToList();

        Assert.Equal(0, Raw(outputs, Other));
        Assert.Equal(7 * 1600, ZeroSamples(outputs));
        Assert.Equal(0, Reports(outputs));
        Assert.Equal(1, _analyzer.VerifyCalls);
    }

    [Fact]
    public void Guest_speaking_right_after_user_is_cut_within_one_second()
    {
        var user = Push(User, 8);
        var other = Push(Other, 20);

        Assert.Equal(8, Raw(user, User));
        Assert.InRange(Raw(other, Other), 1, 10);
        Assert.Equal(1, Reports(other));
    }

    [Fact]
    public void Quiet_speech_is_ignored()
    {
        var outputs = Push(Soft, 10);

        Assert.Equal(0, Raw(outputs, Soft));
        Assert.Equal(0, _analyzer.VerifyCalls);
        Assert.Equal(0, Reports(outputs));
    }

    [Fact]
    public void Guest_mode_blocks_user_without_name()
    {
        var outputs = Push(User, 20, Guest);

        Assert.Equal(0, Raw(outputs, User));
        Assert.Equal(0, Reports(outputs));
    }

    [Fact]
    public void Guest_mode_passes_user_starting_with_name()
    {
        var outputs = Push(UserName, 1, Guest).Concat(Push(User, 12, Guest)).ToList();

        Assert.Equal(1, Raw(outputs, UserName));
        Assert.Equal(12, Raw(outputs, User));
    }

    [Fact]
    public void Name_from_unknown_voice_passes_and_reports_guest()
    {
        var outputs = Push(OtherName, 1, Guest).Concat(Push(Other, 12, Guest)).ToList();

        Assert.Equal(1, Raw(outputs, OtherName));
        Assert.Equal(12, Raw(outputs, Other));
        Assert.Equal(1, Reports(outputs));
    }

    [Fact]
    public void Name_late_in_sentence_does_not_count()
    {
        var outputs = Push(User, 20, Guest).Concat(Push(UserName, 1, Guest)).Concat(Push(User, 5, Guest)).ToList();

        Assert.Equal(0, Raw(outputs, User));
        Assert.Equal(0, Raw(outputs, UserName));
    }

    [Fact]
    public void Allowance_lets_next_sentence_through_once()
    {
        _gate.AllowNextWithoutName(TimeSpan.FromSeconds(8));

        var first = Push(User, 12, Guest);
        Push(Quiet, 10, Guest);
        var second = Push(User, 12, Guest).Concat(Push(Quiet, 1, Guest)).ToList();

        Assert.Equal(12, Raw(first, User));
        Assert.Equal(0, Raw(second, User));
    }

    [Fact]
    public void Allowance_expires()
    {
        _gate.AllowNextWithoutName(TimeSpan.FromSeconds(2));

        Push(Quiet, 30, Guest);
        var outputs = Push(User, 12, Guest).Concat(Push(Quiet, 1, Guest)).ToList();

        Assert.Equal(0, Raw(outputs, User));
    }

    [Fact]
    public void Allowance_is_not_spent_on_quiet_noise()
    {
        _gate.AllowNextWithoutName(TimeSpan.FromSeconds(8));

        Push(Soft, 9, Guest);
        Push(Quiet, 2, Guest);
        var outputs = Push(User, 12, Guest);

        Assert.Equal(12, Raw(outputs, User));
    }

    [Fact]
    public void Note_mode_needs_no_name_but_still_blocks_others()
    {
        var note = new GateInput(GuestMode: true, NoteActive: true);

        var user = Push(User, 12, note);
        Push(Quiet, 10, note);
        var other = Push(Other, 12, note);

        Assert.Equal(12, Raw(user, User));
        Assert.Equal(0, Raw(other, Other));
    }

    [Fact]
    public void Long_user_speech_keeps_passing_and_is_rechecked()
    {
        var outputs = Push(User, 40);

        Assert.Equal(40, Raw(outputs, User));
        Assert.True(_analyzer.VerifyCalls >= 3);
    }

    [Fact]
    public void Input_of_any_length_is_split_into_chunks()
    {
        var first = _gate.Push(Enumerable.Repeat(Quiet, 4000).ToArray(), Normal);
        var second = _gate.Push(Enumerable.Repeat(Quiet, 800).ToArray(), Normal);

        Assert.Equal(3200, first.Send.Sum(a => a.Length));
        Assert.Equal(1600, second.Send.Sum(a => a.Length));
    }
}
