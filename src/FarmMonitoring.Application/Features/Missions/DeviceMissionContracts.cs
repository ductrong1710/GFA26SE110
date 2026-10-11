namespace FarmMonitoring.Application.Features.Missions;

public sealed record DeviceMissionTargetResponse(int Id, int SensorNodeId, string DeviceCode,
    int? WaypointId, int? WaypointSequenceNo, int? SequenceNo, string Status);
public sealed record DeviceMissionResponse(int Id, string Name, string Status, int? GatewayId, int? UavId,
    DateTimeOffset? ScheduledStartAt, DateTimeOffset? ScheduledEndAt,
    IReadOnlyList<WaypointResponse> Waypoints, IReadOnlyList<DeviceMissionTargetResponse> Targets);
public sealed record DeviceMissionStateResponse(int Id, string Status, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string? FailureReason);
public sealed record DeviceMissionFailureRequest(string FailureReason, string? Note);
public sealed record DeviceMissionCancelRequest(string? Reason);
