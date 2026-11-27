using System.Collections.Concurrent;
using CamAda.Core.Assistant;

namespace CamAda.Assistant.Audio;

/// <summary>
/// Misafir kipi süzgecini kendi iş parçacığında, sırayla yürütür: gömme hesabı (~20 ms) arayüzü bekletmesin. Çıktı süzgeç iş
/// parçacığında verilir. Çözümleyici atarsa günlüğe bir kez yazılır ve süzgeç geçirgen olur: ses olduğu gibi gider, Ada bugünkü
/// gibi sürer.
/// </summary>
internal sealed class VoiceFilter : IDisposable
{
    private readonly BlockingCollection<Action> _work = new();
    private readonly VoiceGate _gate;
    private readonly Action<GateOutput> _output;
    private readonly Action<Exception> _log;
    private readonly Thread _thread;
    private bool _passThrough;

    public VoiceFilter(VoiceGate gate, Action<GateOutput> output, Action<Exception> log)
    {
        _gate = gate;
        _output = output;
        _log = log;
        _thread = new Thread(Run) { IsBackground = true, Name = "Ada voice filter" };
        _thread.Start();
    }

    public void Post(short[] samples, GateInput input) => Enqueue(() => Process(samples, input));

    public void AllowNextWithoutName(TimeSpan within) => Enqueue(() => _gate.AllowNextWithoutName(within));

    /// <summary>Kuyruğu kapatır, en çok 1 sn bekler; iki kez çağrılabilir (oturum kapanışı ve uygulamadan çıkış).</summary>
    public void Dispose()
    {
        lock (_work)
        {
            if (_work.IsAddingCompleted)
            {
                return;
            }

            _work.CompleteAdding();
        }

        _thread.Join(TimeSpan.FromSeconds(1));
    }

    private void Enqueue(Action work)
    {
        try
        {
            _work.Add(work);
        }
        catch (InvalidOperationException)
        {
            // kapanıyor: geç gelen ses atılır
        }
    }

    private void Run()
    {
        foreach (var work in _work.GetConsumingEnumerable())
        {
            try
            {
                work();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _log(ex); // çıktı geri çağrısı patladı (ör. kapanışta): süzgeç sürer
            }
        }
    }

    private void Process(short[] samples, GateInput input)
    {
        GateOutput output;
        if (_passThrough)
        {
            output = new GateOutput([samples], false);
        }
        else
        {
            try
            {
                output = _gate.Push(samples, input);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _passThrough = true;
                _log(ex);
                output = new GateOutput([samples], false);
            }
        }

        _output(output);
    }
}
