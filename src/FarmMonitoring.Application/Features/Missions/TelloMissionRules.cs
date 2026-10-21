using FarmMonitoring.Application.Common;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Features.Missions;

/// <summary>Planning compatibility with the ESP32 mission_flight profile; never executes flight commands.</summary>
public static class TelloMissionRules
{
    public static bool Applies(Mission mission) =>
        mission.Uav?.Model?.Contains("Tello", StringComparison.OrdinalIgnoreCase) == true;

    public static void ValidateAndNormalize(Mission mission)
    {
        if (!Applies(mission)) return;
        if (mission.Waypoints.Count is < 1 or > 16 || mission.Targets.Count is < 1 or > 8)
            throw new BusinessRuleException("Tello missions require 1–16 waypoints and 1–8 targets.");

        var steps = 0;
        decimal x = 0, y = 0;
        foreach (var waypoint in mission.Waypoints.OrderBy(w => w.SequenceNo))
        {
            if (waypoint.LocalX is not decimal nextX || waypoint.LocalY is not decimal nextY)
                throw new BusinessRuleException("Tello waypoints require localX and localY in meters; GPS coordinates cannot define a Tello route.");
            if (Math.Abs(nextX) > 100 || Math.Abs(nextY) > 100 || !Centimeters(nextX) || !Centimeters(nextY))
                throw new BusinessRuleException("Tello local coordinates must be within ±100 meters with whole-centimeter precision.");
            if (waypoint.AltitudeM is not decimal altitude || altitude is < 0.8m or > 1.2m || !Centimeters(altitude))
                throw new BusinessRuleException("Tello waypoint altitude must be 0.8–1.2 meters with whole-centimeter precision (nominal 1 meter).");
            var action = waypoint.ActionType?.Trim().ToUpperInvariant();
            action = action switch { null or "" => "MOVE", "COLLECT_SENSOR" => "COLLECT", "WAIT" => "HOLD", _ => action };
            if (action is not ("MOVE" or "HOLD" or "COLLECT"))
                throw new BusinessRuleException("Tello waypoint actions must be MOVE, HOLD or COLLECT.");
            if (waypoint.PlannedHoldSeconds is < 0 or > 30)
                throw new BusinessRuleException("Tello waypoint hold must be between 0 and 30 seconds.");
            var hasTargets = mission.Targets.Any(t => ReferenceEquals(t.Waypoint, waypoint) ||
                waypoint.Id > 0 && t.WaypointId == waypoint.Id);
            if ((action == "COLLECT") != hasTargets)
                throw new BusinessRuleException("Each Tello COLLECT waypoint requires targets, and every target must map to a COLLECT waypoint.");
            waypoint.ActionType = action;
            waypoint.PlannedHoldSeconds ??= 0;
            // Firmware emits an altitude step and waypoint marker even when no movement is needed.
            steps += 2 + AxisSteps(nextX - x) + AxisSteps(nextY - y);
            x = nextX; y = nextY;
        }
        steps += AxisSteps(-x) + AxisSteps(-y); // Automatic return to takeoff origin.
        if (steps > 128)
            throw new BusinessRuleException("Tello route exceeds 128 steps including automatic return to origin.");
    }

    private static bool Centimeters(decimal value) => value * 100 == decimal.Truncate(value * 100);
    private static int AxisSteps(decimal delta)
    {
        var distance = Math.Abs(delta);
        if (distance is > 0 and < 0.2m)
            throw new BusinessRuleException("Every nonzero Tello route axis movement, including return to origin, must be at least 0.2 meters.");
        // Firmware splits at 1m and redistributes a final segment shorter than 0.2m.
        return (int)decimal.Ceiling(distance);
    }
}
