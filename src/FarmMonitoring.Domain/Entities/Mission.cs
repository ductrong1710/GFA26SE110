namespace FarmMonitoring.Domain.Entities;

public enum MissionStatus { PENDING, SCHEDULED, RUNNING, COMPLETED, FAILED, CANCELLED }
public enum MissionTargetStatus { PENDING, COLLECTED, FAILED, SKIPPED }

public static class MissionRules
{
    public static bool CanTransition(MissionStatus from, MissionStatus to) => (from, to) switch
    {
        (MissionStatus.PENDING, MissionStatus.SCHEDULED or MissionStatus.CANCELLED) => true,
        (MissionStatus.SCHEDULED, MissionStatus.PENDING or MissionStatus.RUNNING or MissionStatus.CANCELLED) => true,
        (MissionStatus.RUNNING, MissionStatus.COMPLETED or MissionStatus.FAILED or MissionStatus.CANCELLED) => true,
        _ => false
    };
}

public sealed class Mission
{
    public int Id { get; set; }
    public int FarmId { get; set; }
    public Farm Farm { get; set; } = null!;
    public int? UavId { get; set; }
    public Uav? Uav { get; set; }
    public int? GatewayId { get; set; }
    public Gateway? Gateway { get; set; }
    public string Name { get; set; } = "";
    public MissionStatus Status { get; set; }
    public DateTimeOffset? ScheduledStartAt { get; set; }
    public DateTimeOffset? ScheduledEndAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public string? OperatorNotes { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public List<MissionTarget> Targets { get; set; } = [];
    public List<MissionWaypoint> Waypoints { get; set; } = [];
}

public sealed class MissionWaypoint
{
    public int Id { get; set; }
    public int MissionId { get; set; }
    public Mission Mission { get; set; } = null!;
    public int SequenceNo { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? LocalX { get; set; }
    public decimal? LocalY { get; set; }
    public decimal? AltitudeM { get; set; }
    public string? ActionType { get; set; }
    public int? PlannedHoldSeconds { get; set; }
}

public sealed class MissionTarget
{
    public int Id { get; set; }
    public int MissionId { get; set; }
    public Mission Mission { get; set; } = null!;
    public int SensorNodeId { get; set; }
    public SensorNode SensorNode { get; set; } = null!;
    public int? WaypointId { get; set; }
    public MissionWaypoint? Waypoint { get; set; }
    public int? SequenceNo { get; set; }
    public MissionTargetStatus Status { get; set; }
}

public sealed class MissionLog
{
    public int Id { get; set; }
    public int MissionId { get; set; }
    public Mission Mission { get; set; } = null!;
    public int? UserId { get; set; }
    public User? User { get; set; }
    public int? GatewayId { get; set; }
    public Gateway? Gateway { get; set; }
    public string LogType { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
