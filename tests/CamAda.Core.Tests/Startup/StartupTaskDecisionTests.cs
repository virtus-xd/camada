using CamAda.Core.Startup;

namespace CamAda.Core.Tests.Startup;

public class StartupTaskDecisionTests
{
    [Theory]
    [InlineData(true, StartupTaskStatus.Disabled, StartupTaskAction.RequestEnable)]
    [InlineData(true, StartupTaskStatus.Enabled, StartupTaskAction.None)]
    [InlineData(true, StartupTaskStatus.EnabledByPolicy, StartupTaskAction.None)]
    [InlineData(true, StartupTaskStatus.DisabledByUser, StartupTaskAction.BlockedByUser)]
    [InlineData(true, StartupTaskStatus.DisabledByPolicy, StartupTaskAction.BlockedByPolicy)]
    [InlineData(false, StartupTaskStatus.Enabled, StartupTaskAction.Disable)]
    [InlineData(false, StartupTaskStatus.EnabledByPolicy, StartupTaskAction.BlockedByPolicy)]
    [InlineData(false, StartupTaskStatus.Disabled, StartupTaskAction.None)]
    [InlineData(false, StartupTaskStatus.DisabledByUser, StartupTaskAction.None)]
    [InlineData(false, StartupTaskStatus.DisabledByPolicy, StartupTaskAction.None)]
    public void Decide_maps_wish_and_state(bool wanted, StartupTaskStatus status, StartupTaskAction expected) =>
        Assert.Equal(expected, StartupTaskDecision.Decide(wanted, status));

    [Theory]
    [InlineData(StartupTaskStatus.Enabled, true)]
    [InlineData(StartupTaskStatus.EnabledByPolicy, true)]
    [InlineData(StartupTaskStatus.Disabled, false)]
    [InlineData(StartupTaskStatus.DisabledByUser, false)]
    [InlineData(StartupTaskStatus.DisabledByPolicy, false)]
    public void IsOn_only_for_enabled_states(StartupTaskStatus status, bool expected) =>
        Assert.Equal(expected, StartupTaskDecision.IsOn(status));

    [Fact]
    public void Status_values_match_windows_startup_task_state() =>
        Assert.Equal([0, 1, 2, 3, 4], Enum.GetValues<StartupTaskStatus>().Select(s => (int)s));
}
