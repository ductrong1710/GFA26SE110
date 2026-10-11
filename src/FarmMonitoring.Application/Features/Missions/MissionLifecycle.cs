using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Alerts;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Missions;

// Callers own authentication, transaction and mission row lock. Both actor types share these rules.
public sealed class MissionLifecycle(IMissionRepository repository, TimeProvider clock, AlertService alerts)
{
    public async Task ChangeStatusAsync(Mission mission, MissionStatus status, MissionActionRequest request,
        int? userId, int? gatewayId, CancellationToken ct)
    {
        if (!MissionRules.CanTransition(mission.Status, status))
            throw new ConflictException($"Cannot change mission from {mission.Status} to {status}.");
        if (status is MissionStatus.SCHEDULED or MissionStatus.RUNNING)
        {
            await ValidateResources(mission, true, ct);
            if (mission.ScheduledStartAt is null || mission.ScheduledEndAt is null || mission.ScheduledEndAt <= mission.ScheduledStartAt)
                throw new BusinessRuleException("A valid scheduled start and end are required.");
            if (status == MissionStatus.SCHEDULED) ValidateFutureSchedule(mission);
            await CheckOverlap(mission, status == MissionStatus.RUNNING, ct);
        }
        if (status == MissionStatus.COMPLETED && (mission.Targets.Count == 0 || mission.Targets.Any(x => x.Status == MissionTargetStatus.PENDING)))
            throw new BusinessRuleException("Every mission target must have a collection outcome before completion.");
        if (status == MissionStatus.FAILED)
        {
            var reason = request.FailureReason ?? request.Reason ?? request.Note;
            if (string.IsNullOrWhiteSpace(reason)) throw new ValidationException("A failure reason is required.");
            mission.FailureReason = reason.Trim();
            await alerts.RaiseAsync(new(AlertType.MISSION_ERROR, AlertSeverity.CRITICAL, null, null, mission.GatewayId, mission.UavId, mission.Id,
                "Mission failed: " + mission.FailureReason, null), ct);
        }
        if (status == MissionStatus.PENDING)
        {
            mission.ScheduledStartAt = null;
            mission.ScheduledEndAt = null;
        }
        if (status == MissionStatus.RUNNING) mission.StartedAt = Timestamp();
        if (status is MissionStatus.COMPLETED or MissionStatus.FAILED or MissionStatus.CANCELLED)
            mission.CompletedAt = Timestamp();
        mission.Status = status;
        mission.UpdatedAt = Timestamp();
        if (request.OperatorNotes is not null) mission.OperatorNotes = request.OperatorNotes.Trim();
        Log(mission, userId, gatewayId, status == MissionStatus.RUNNING ? "STARTED" : status.ToString(),
            request.FailureReason ?? request.Reason ?? request.Note ?? request.OperatorNotes ?? $"Mission changed to {status}.");
    }

    public async Task ValidateResources(Mission mission, bool ready, CancellationToken ct)
    {
        mission.Farm = await repository.GetFarmAsync(mission.FarmId, ct) ?? throw new NotFoundException("Farm not found.");
        if (!mission.Farm.IsActive) throw new BusinessRuleException("Mission farm must be active.");
        // Resource locks follow mission, UAV, gateway, nodes, channels across ingestion and planning.
        mission.Uav = mission.UavId is int uavId ? await repository.LockUavAsync(uavId, ct) ?? throw new NotFoundException("UAV not found.") : null;
        mission.Gateway = mission.GatewayId is int gatewayId ? await repository.LockGatewayAsync(gatewayId, ct) ?? throw new NotFoundException("Gateway not found.") : null;
        var sensors = await repository.GetSensorsAsync(mission.Targets.Select(x => x.SensorNodeId).ToArray(), ct);
        if (sensors.Count != mission.Targets.Count) throw new NotFoundException("A selected sensor node was not found.");
        if (sensors.Any(x => x.Zone.FarmId != mission.FarmId)) throw new BusinessRuleException("Every target sensor must belong to the mission farm.");
        foreach (var target in mission.Targets) target.SensorNode = sensors.Single(x => x.Id == target.SensorNodeId);
        if (mission.Uav is { IsActive: false } || mission.Gateway is { IsActive: false })
            throw new BusinessRuleException("Assigned UAV and gateway must be active.");
        if (ready && (mission.Uav is null || mission.Gateway is null || mission.Targets.Count == 0 || mission.Waypoints.Count == 0))
            throw new BusinessRuleException("Scheduling and starting require a UAV, gateway, at least one target and at least one waypoint.");
        if (ready && mission.Targets.Any(t => t.Waypoint is null || !mission.Waypoints.Contains(t.Waypoint)))
            throw new BusinessRuleException("Every target must be assigned to a waypoint in this mission before scheduling or starting.");
        if (ready && sensors.Any(x => !x.IsActive)) throw new BusinessRuleException("Target sensors must be active.");
    }

    public void ValidateFutureSchedule(Mission mission)
    {
        if (mission.ScheduledStartAt <= clock.GetUtcNow()) throw new BusinessRuleException("Scheduled start must be in the future.");
    }
    public async Task CheckOverlap(Mission mission, bool starting, CancellationToken ct)
    {
        if (await repository.HasOverlapAsync(mission, starting, ct))
            throw new ConflictException("The UAV or gateway is already reserved for an overlapping mission.");
    }
    // PostgreSQL timestamps have microsecond precision; first response and retries must agree.
    private DateTimeOffset Timestamp()
    {
        var now = clock.GetUtcNow();
        return new DateTimeOffset(now.UtcTicks - now.UtcTicks % 10, TimeSpan.Zero);
    }
    public void Log(Mission mission, int? userId, int? gatewayId, string type, string message) => repository.AddLog(new MissionLog
    { Mission = mission, UserId = userId, GatewayId = gatewayId, LogType = type, Message = message.Trim(), CreatedAt = clock.GetUtcNow() });
}
