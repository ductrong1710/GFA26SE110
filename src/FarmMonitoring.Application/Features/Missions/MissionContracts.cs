using FarmMonitoring.Application.Common;

namespace FarmMonitoring.Application.Features.Missions;

public sealed record WaypointRequest(int SequenceNo, decimal? Latitude, decimal? Longitude, decimal? LocalX,
    decimal? LocalY, decimal? AltitudeM, string? ActionType, int? PlannedHoldSeconds);
public sealed record MissionRequest(string Name, int FarmId, int? UavId, int? GatewayId,
    DateTimeOffset? ScheduledStartAt, DateTimeOffset? ScheduledEndAt, int[] SensorNodeIds,
    WaypointRequest[] Waypoints, string? OperatorNotes);
public sealed record ScheduleMissionRequest(DateTimeOffset ScheduledStartAt, DateTimeOffset ScheduledEndAt);
public sealed record MissionActionRequest(string? OperatorNotes, string? FailureReason, string? Note, string? Reason);
public sealed record MissionStatusRequest(string Status, string? Note);
public sealed class MissionQuery : PageQuery
{
    public int? FarmId { get; init; }
    public int? UavId { get; init; }
    public int? GatewayId { get; init; }
    public string? Status { get; init; }
    public DateTimeOffset? ScheduledFrom { get; init; }
    public DateTimeOffset? ScheduledTo { get; init; }
}
public sealed record MissionReference(int Id, string Name);
public sealed record WaypointResponse(int Id, int SequenceNo, decimal? Latitude, decimal? Longitude,
    decimal? LocalX, decimal? LocalY, decimal? AltitudeM, string? ActionType, int? PlannedHoldSeconds);
public sealed record TargetResponse(int Id, int SensorNodeId, string? SensorName, int? WaypointId, int? SequenceNo, string Status);
public sealed record MissionProgress(int TotalTargets, int SuccessfulTargets, int FailedTargets, int SkippedTargets, int PendingTargets);
public sealed record MissionSummary(int Id, string Name, int FarmId, int? UavId, int? GatewayId, string Status,
    DateTimeOffset? ScheduledStartAt, DateTimeOffset? ScheduledEndAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt);
public sealed record MissionResponse(int Id, string Name, string Status, MissionReference Farm,
    MissionReference? Uav, MissionReference? Gateway, DateTimeOffset? ScheduledStartAt, DateTimeOffset? ScheduledEndAt,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, int CreatedByUserId, string? OperatorNotes,
    string? FailureReason, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt,
    IReadOnlyList<TargetResponse> Targets, IReadOnlyList<WaypointResponse> Waypoints, MissionProgress Progress);
public sealed record MissionResults(int MissionId, string Status, int TotalTargets, int SuccessfulTargets,
    int FailedTargets, int SkippedTargets, int PendingTargets, IReadOnlyList<TargetResponse> Targets);
public sealed record MissionLogResponse(int Id, int MissionId, int? UserId, int? GatewayId, string LogType, string Message, DateTimeOffset CreatedAt);
