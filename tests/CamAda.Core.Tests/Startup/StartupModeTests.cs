using CamAda.Core.Startup;

namespace CamAda.Core.Tests.Startup;

public class StartupModeTests
{
    [Fact]
    public void No_arguments_means_normal() =>
        Assert.IsType<StartupMode.Normal>(StartupMode.Parse([]));

    [Fact]
    public void Watchdog_with_parent_pid() =>
        Assert.Equal(new StartupMode.Watchdog(4242), StartupMode.Parse(["--watchdog", "4242"]));

    [Fact]
    public void Restore_taskbar_flag() =>
        Assert.IsType<StartupMode.RestoreTaskbar>(StartupMode.Parse(["--restore-taskbar"]));

    [Theory]
    [InlineData(new object[] { new[] { "--watchdog" } })]
    [InlineData(new object[] { new[] { "--watchdog", "abc" } })]
    public void Broken_watchdog_arguments_fail_safe_to_restore(string[] args) =>
        Assert.IsType<StartupMode.RestoreTaskbar>(StartupMode.Parse(args));

    [Fact]
    public void Unknown_arguments_are_ignored() =>
        Assert.IsType<StartupMode.Normal>(StartupMode.Parse(["--bilinmeyen"]));

    [Fact]
    public void Restarting_flag_is_normal_mode_that_waits_for_the_previous_instance()
    {
        Assert.Equal(new StartupMode.Normal(Restarting: true), StartupMode.Parse([StartupMode.RestartingArgument]));
        Assert.Equal(new StartupMode.Normal(Restarting: false), StartupMode.Parse([]));
    }
}
