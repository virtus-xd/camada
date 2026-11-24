using CamAda.Core.Display;

namespace CamAda.Core.Tests.Display;

// Son inceleme: parlaklık yazımları ayrı görevlerde koşunca yavaş bir eski yazım sona kalabiliyordu. Tek yazıcı:
// aynı anda bir yazım, yazım sürerken gelenlerin yalnız sonuncusu.
public class LatestValuePumpTests
{
    private sealed class SlowWriter
    {
        private readonly List<TaskCompletionSource> _gates = [];
        private int _running;

        public List<int> Written { get; } = [];

        public int MaxConcurrent { get; private set; }

        public async Task Write(int value)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gates)
            {
                Written.Add(value);
                _gates.Add(gate);
                MaxConcurrent = Math.Max(MaxConcurrent, ++_running);
            }

            await gate.Task;
            lock (_gates)
            {
                _running--;
            }
        }

        public void ReleaseNext()
        {
            TaskCompletionSource gate;
            lock (_gates)
            {
                gate = _gates.First(g => !g.Task.IsCompleted);
            }

            gate.SetResult();
        }

        public async Task WaitForWrites(int count)
        {
            for (var i = 0; i < 200 && Written.Count < count; i++)
            {
                await Task.Delay(5);
            }
        }
    }

    [Fact]
    public async Task First_value_is_written_at_once()
    {
        var writer = new SlowWriter();
        var pump = new LatestValuePump(writer.Write);
        pump.Post(10);
        await writer.WaitForWrites(1);
        Assert.Equal([10], writer.Written);
    }

    [Fact]
    public async Task While_writing_only_the_latest_waits_and_ends_last()
    {
        var writer = new SlowWriter();
        var pump = new LatestValuePump(writer.Write);
        pump.Post(10);
        await writer.WaitForWrites(1);
        pump.Post(20);
        pump.Post(30);
        writer.ReleaseNext();
        await writer.WaitForWrites(2);
        writer.ReleaseNext();
        await pump.WhenIdle();
        Assert.Equal([10, 30], writer.Written);
        Assert.Equal(1, writer.MaxConcurrent);
    }

    [Fact]
    public async Task After_idle_a_new_value_is_written_again()
    {
        var writer = new SlowWriter();
        var pump = new LatestValuePump(writer.Write);
        pump.Post(10);
        await writer.WaitForWrites(1);
        writer.ReleaseNext();
        await pump.WhenIdle();
        pump.Post(40);
        await writer.WaitForWrites(2);
        writer.ReleaseNext();
        await pump.WhenIdle();
        Assert.Equal([10, 40], writer.Written);
    }

    [Fact]
    public async Task A_failing_write_does_not_stop_the_pump()
    {
        var written = new List<int>();
        var pump = new LatestValuePump(value =>
        {
            written.Add(value);
            return value == 10 ? Task.FromException(new InvalidOperationException()) : Task.CompletedTask;
        });
        pump.Post(10);
        await pump.WhenIdle();
        pump.Post(20);
        await pump.WhenIdle();
        Assert.Equal([10, 20], written);
    }
}
