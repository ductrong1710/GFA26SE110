using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Application.Features.Missions;

namespace FarmMonitoring.UnitTests;

public class MissionRulesTests
{
    [Fact]
    public void State_machine_allows_only_documented_edges()
    {
        var allowed = new HashSet<(MissionStatus, MissionStatus)>
        {
            (MissionStatus.PENDING, MissionStatus.SCHEDULED), (MissionStatus.PENDING, MissionStatus.CANCELLED),
            (MissionStatus.SCHEDULED, MissionStatus.PENDING), (MissionStatus.SCHEDULED, MissionStatus.RUNNING), (MissionStatus.SCHEDULED, MissionStatus.CANCELLED),
            (MissionStatus.RUNNING, MissionStatus.COMPLETED), (MissionStatus.RUNNING, MissionStatus.FAILED), (MissionStatus.RUNNING, MissionStatus.CANCELLED)
        };
        foreach (var from in Enum.GetValues<MissionStatus>())
        foreach (var to in Enum.GetValues<MissionStatus>())
            Assert.Equal(allowed.Contains((from, to)), MissionRules.CanTransition(from, to));
    }
    [Fact]
    public void Waypoints_require_complete_coordinate_pairs()
    {
        var validator = new WaypointValidator();
        Assert.False(validator.Validate(new WaypointRequest(1, 10, null, null, null, null, null, null)).IsValid);
        Assert.False(validator.Validate(new WaypointRequest(1, null, null, null, null, null, null, null)).IsValid);
        Assert.True(validator.Validate(new WaypointRequest(1, null, null, 0, 0, null, null, null)).IsValid);
    }
}
