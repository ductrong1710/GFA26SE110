using FarmMonitoring.Application.Features.Missions;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.UnitTests;

public class MissionLaunchRulesTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-10-24T10:00:00Z");
    private static Mission Ready() => new() { Status = MissionStatus.SCHEDULED,
        ScheduledStartAt = Start, ScheduledEndAt = Start.AddMinutes(5), LaunchSession = "boot",
        LaunchReadyAt = Start, LaunchApprovedAt = Start };

    [Theory]
    [InlineData(-1, "WAITING_FOR_TIME")]
    [InlineData(0, "READY_TO_START")]
    [InlineData(59, "READY_TO_START")]
    [InlineData(60, "MISSED")]
    public void Start_window_is_bounded(int seconds, string expected)
    {
        var mission = Ready();
        mission.LaunchReadyAt = Start.AddSeconds(seconds);
        Assert.Equal(expected, MissionLaunchRules.State(mission, Start.AddSeconds(seconds)));
    }

    [Fact]
    public void Missing_confirmation_stale_readiness_and_consumption_prevent_start()
    {
        var mission = Ready();
        mission.LaunchApprovedAt = null;
        Assert.Equal("AWAITING_CONFIRMATION", MissionLaunchRules.State(mission, Start));
        mission.LaunchApprovedAt = Start;
        mission.LaunchReadyAt = Start.AddSeconds(-31);
        Assert.Equal("WAITING_FOR_GATEWAY", MissionLaunchRules.State(mission, Start));
        mission.LaunchConsumedAt = Start;
        Assert.Equal("CONSUMED", MissionLaunchRules.State(mission, Start));
    }

    [Fact]
    public void Invalidate_changes_revision_and_clears_approval()
    {
        var mission = Ready();
        MissionLaunchRules.Invalidate(mission);
        Assert.Equal(1, mission.PlanRevision);
        Assert.Null(mission.LaunchSession);
        Assert.Null(mission.LaunchApprovedAt);
    }
}
