using CamAda.Core.Safety;

namespace CamAda.Core.Tests.Safety;

public class TaskbarSafetyTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("camada-").FullName;
    private string FilePath => Path.Combine(_dir, "taskbar-state.json");
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void Restore_only_when_hidden_and_not_clean(bool hidden, bool clean, bool expected) =>
        Assert.Equal(expected, WatchdogDecision.ShouldRestore(new TaskbarSafetyState(hidden, clean, 2)));

    [Fact]
    public void No_state_means_no_restore() =>
        Assert.False(WatchdogDecision.ShouldRestore(null));

    [Fact]
    public void Missing_file_loads_null() =>
        Assert.Null(new TaskbarSafetyStore(FilePath).Load());

    [Fact]
    public void Save_then_load_round_trips()
    {
        var store = new TaskbarSafetyStore(FilePath);
        store.Save(new TaskbarSafetyState(true, false, 1));
        Assert.Equal(new TaskbarSafetyState(true, false, 1), store.Load());
    }

    [Fact]
    public void Corrupt_file_fails_safe_to_restore()
    {
        File.WriteAllText(FilePath, "xx");
        var state = new TaskbarSafetyStore(FilePath).Load();
        Assert.Equal(new TaskbarSafetyState(true, false, TaskbarSafetyStore.DefaultTaskbarState), state);
        Assert.True(WatchdogDecision.ShouldRestore(state));
    }
}
