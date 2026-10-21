using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Missions;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.UnitTests;

public sealed class TelloMissionRulesTests
{
    private static Mission Plan(decimal x = 0.5m, decimal y = 0, decimal altitude = 1)
    {
        var waypoint = new MissionWaypoint { SequenceNo = 1, LocalX = x, LocalY = y, AltitudeM = altitude, ActionType = "COLLECT" };
        return new Mission { Uav = new Uav { Model = "DJI tElLo EDU" }, Waypoints = [waypoint],
            Targets = [new MissionTarget { SensorNodeId = 1, Waypoint = waypoint }] };
    }

    [Theory]
    [InlineData(0.8)]
    [InlineData(1)]
    [InlineData(1.2)]
    public void Valid_local_route_normalizes_action_and_optional_hold(decimal altitude)
    {
        var mission = Plan(altitude: altitude);
        mission.Waypoints[0].ActionType = " collect_sensor ";
        TelloMissionRules.ValidateAndNormalize(mission);
        Assert.Equal("COLLECT", mission.Waypoints[0].ActionType);
        Assert.Equal(0, mission.Waypoints[0].PlannedHoldSeconds);
    }

    [Theory]
    [InlineData(0.35)]
    [InlineData(0.79)]
    [InlineData(1.21)]
    [InlineData(1.005)]
    public void Rejects_incompatible_altitude(decimal altitude) =>
        Assert.Throws<BusinessRuleException>(() => TelloMissionRules.ValidateAndNormalize(Plan(altitude: altitude)));

    [Theory]
    [InlineData(0.19, 0)]
    [InlineData(0.501, 0)]
    [InlineData(100.01, 0)]
    [InlineData(0.5, -100.01)]
    [InlineData(0.5, 0.1)]
    public void Rejects_incompatible_coordinates(decimal x, decimal y) =>
        Assert.Throws<BusinessRuleException>(() => TelloMissionRules.ValidateAndNormalize(Plan(x, y)));

    [Fact]
    public void Rejects_gps_only_even_when_latitude_and_longitude_exist()
    {
        var mission = Plan();
        mission.Waypoints[0].LocalX = null;
        mission.Waypoints[0].LocalY = null;
        mission.Waypoints[0].Latitude = 10;
        mission.Waypoints[0].Longitude = 106;
        Assert.Throws<BusinessRuleException>(() => TelloMissionRules.ValidateAndNormalize(mission));
    }

    [Fact]
    public void Route_budget_includes_return_and_two_markers_per_waypoint()
    {
        TelloMissionRules.ValidateAndNormalize(Plan(63)); // 63 + 63 + 2 = 128.
        Assert.Throws<BusinessRuleException>(() => TelloMissionRules.ValidateAndNormalize(Plan(63.01m)));
        TelloMissionRules.ValidateAndNormalize(Plan(1.01m)); // Firmware redistributes 101cm into 81+20.
    }

    [Fact]
    public void Rejects_short_intermediate_and_return_axis_segments()
    {
        var mission = Plan();
        mission.Waypoints.Add(new() { SequenceNo = 2, LocalX = .6m, LocalY = 0, AltitudeM = 1 });
        Assert.Throws<BusinessRuleException>(() => TelloMissionRules.ValidateAndNormalize(mission));
        mission.Waypoints[1].LocalX = .1m; // Outbound difference valid, return is too short.
        Assert.Throws<BusinessRuleException>(() => TelloMissionRules.ValidateAndNormalize(mission));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    public void Rejects_hold_outside_firmware_bounds(int hold)
    {
        var mission = Plan();
        mission.Waypoints[0].PlannedHoldSeconds = hold;
        Assert.Throws<BusinessRuleException>(() => TelloMissionRules.ValidateAndNormalize(mission));
    }

    [Fact]
    public void Rejects_missing_collect_mapping_and_unsupported_action()
    {
        var mission = Plan();
        mission.Waypoints[0].ActionType = "MOVE";
        Assert.Throws<BusinessRuleException>(() => TelloMissionRules.ValidateAndNormalize(mission));
        mission.Waypoints[0].ActionType = "LAND";
        Assert.Throws<BusinessRuleException>(() => TelloMissionRules.ValidateAndNormalize(mission));
        mission.Waypoints[0].ActionType = "COLLECT";
        mission.Targets[0].Waypoint = null;
        Assert.Throws<BusinessRuleException>(() => TelloMissionRules.ValidateAndNormalize(mission));
    }

    [Fact]
    public void Enforces_capacity_and_normalizes_wait()
    {
        var mission = Plan();
        mission.Waypoints.Add(new() { SequenceNo = 2, LocalX = 0, LocalY = 0, AltitudeM = 1, ActionType = "WAIT", PlannedHoldSeconds = 30 });
        TelloMissionRules.ValidateAndNormalize(mission);
        Assert.Equal("HOLD", mission.Waypoints[1].ActionType);
        while (mission.Waypoints.Count < 17) mission.Waypoints.Add(new());
        Assert.Throws<BusinessRuleException>(() => TelloMissionRules.ValidateAndNormalize(mission));
        mission = Plan();
        while (mission.Targets.Count < 9) mission.Targets.Add(new());
        Assert.Throws<BusinessRuleException>(() => TelloMissionRules.ValidateAndNormalize(mission));
    }

    [Fact]
    public void Generic_uav_retains_existing_planning_behavior()
    {
        var mission = Plan(1000, 0.001m, 20);
        mission.Uav!.Model = "Other";
        mission.Waypoints[0].ActionType = "COLLECT_SENSOR";
        TelloMissionRules.ValidateAndNormalize(mission);
        Assert.Equal("COLLECT_SENSOR", mission.Waypoints[0].ActionType);
    }
}
