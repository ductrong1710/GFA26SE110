using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Missions;

public sealed class MissionService(FarmAccessService access, IMissionRepository repository, IValidator<MissionRequest> plans,
    IValidator<ScheduleMissionRequest> schedules, IValidator<MissionActionRequest> actions,
    IValidator<MissionQuery> queries, IValidator<PageQuery> pages, TimeProvider clock, MissionLifecycle lifecycle)
{
    public async Task<PagedResult<MissionSummary>> ListAsync(MissionQuery query, CancellationToken ct)
    {
        await queries.ValidateAndThrowAsync(query, ct);
        await access.CheckFilterAsync(FarmResource.Farm, query.FarmId, ct);
        return await repository.ListAsync(query, await access.GetScopeAsync(ct), ct);
    }

    public async Task<MissionResponse> GetAsync(int id, CancellationToken ct) => await MapAsync(await Find(id, false, ct), ct);

    public async Task<MissionResponse> CreateAsync(MissionRequest request, int actor, CancellationToken ct)
    {
        await plans.ValidateAndThrowAsync(request, ct);
        return await repository.InTransactionAsync(async () =>
        {
            var mission = new Mission { CreatedByUserId = actor, CreatedAt = clock.GetUtcNow() };
            await ApplyPlan(mission, request, ct);
            repository.Add(mission);
            Log(mission, actor, "CREATED", "Mission created.");
            await repository.SaveAsync(ct);
            return await MapAsync(mission, ct);
        }, ct);
    }

    public async Task<MissionResponse> UpdateAsync(int id, MissionRequest request, int actor, CancellationToken ct)
    {
        await plans.ValidateAndThrowAsync(request, ct);
        return await repository.InTransactionAsync(async () =>
        {
            var mission = await Find(id, true, ct);
            if (mission.Status is not (MissionStatus.PENDING or MissionStatus.SCHEDULED))
                throw new ConflictException("Only pending or scheduled missions can be edited.");
            await ApplyPlan(mission, request, ct);
            mission.UpdatedAt = clock.GetUtcNow();
            Log(mission, actor, "UPDATED", "Mission plan updated.");
            await repository.SaveAsync(ct);
            return await MapAsync(mission, ct);
        }, ct);
    }

    private async Task ApplyPlan(Mission mission, MissionRequest request, CancellationToken ct)
    {
        await access.EnsureFarmAccessAsync(request.FarmId, ct);
        await access.EnsureManyAsync(FarmResource.SensorNode, request.TargetSensorIds(), ct);
        // Validate and reserve resources before mutating the tracked plan or flushing child replacements.
        var candidate = new Mission
        {
            Id = mission.Id, Name = request.Name.Trim(), FarmId = request.FarmId,
            UavId = request.UavId, GatewayId = request.GatewayId, OperatorNotes = request.OperatorNotes?.Trim(),
            ScheduledStartAt = request.ScheduledStartAt?.ToUniversalTime(), ScheduledEndAt = request.ScheduledEndAt?.ToUniversalTime()
        };
        var waypoints = request.Waypoints.Select(x => new MissionWaypoint
        {
            SequenceNo = x.SequenceNo, Latitude = x.Latitude, Longitude = x.Longitude, LocalX = x.LocalX,
            LocalY = x.LocalY, AltitudeM = x.AltitudeM, ActionType = x.ActionType?.Trim(), PlannedHoldSeconds = x.PlannedHoldSeconds
        }).ToArray();
        var targets = request.TargetAssignments is { } assignments
            ? assignments.Select(x => new MissionTarget { SensorNodeId = x.SensorNodeId, SequenceNo = x.SequenceNo,
                Waypoint = waypoints.Single(w => w.SequenceNo == x.WaypointSequenceNo) }).ToArray()
            : request.TargetSensorIds().Select((id, index) => new MissionTarget { SensorNodeId = id, SequenceNo = index + 1 }).ToArray();
        candidate.Targets = targets.ToList();
        candidate.Waypoints = waypoints.ToList();
        await lifecycle.ValidateResources(candidate, request.ScheduledStartAt.HasValue, ct);
        if (request.ScheduledStartAt.HasValue)
        {
            lifecycle.ValidateFutureSchedule(candidate);
            await lifecycle.CheckOverlap(candidate, false, ct);
            candidate.Status = MissionStatus.SCHEDULED;
        }
        mission.Name = candidate.Name;
        mission.FarmId = candidate.FarmId;
        mission.Farm = candidate.Farm;
        mission.UavId = candidate.UavId;
        mission.Uav = candidate.Uav;
        mission.GatewayId = candidate.GatewayId;
        mission.Gateway = candidate.Gateway;
        mission.OperatorNotes = candidate.OperatorNotes;
        mission.ScheduledStartAt = candidate.ScheduledStartAt;
        mission.ScheduledEndAt = candidate.ScheduledEndAt;
        mission.Status = candidate.Status;
        await repository.ReplacePlanAsync(mission, targets, waypoints, ct);
        MissionLaunchRules.Invalidate(mission);
    }

    public async Task<MissionResponse> ScheduleAsync(int id, ScheduleMissionRequest request, int actor, CancellationToken ct)
    {
        await schedules.ValidateAndThrowAsync(request, ct);
        return await repository.InTransactionAsync(async () =>
        {
            var mission = await Find(id, true, ct);
            if (mission.Status is not (MissionStatus.PENDING or MissionStatus.SCHEDULED))
                throw new ConflictException("Only pending or scheduled missions can be scheduled.");
            mission.ScheduledStartAt = request.ScheduledStartAt.ToUniversalTime();
            mission.ScheduledEndAt = request.ScheduledEndAt.ToUniversalTime();
            lifecycle.ValidateFutureSchedule(mission);
            await lifecycle.ValidateResources(mission, true, ct);
            await lifecycle.CheckOverlap(mission, false, ct);
            mission.Status = MissionStatus.SCHEDULED;
            MissionLaunchRules.Invalidate(mission);
            mission.UpdatedAt = clock.GetUtcNow();
            Log(mission, actor, "SCHEDULED", "Mission scheduled.");
            await repository.SaveAsync(ct);
            return await MapAsync(mission, ct);
        }, ct);
    }

    public async Task<MissionResponse> ChangeStatusAsync(int id, MissionStatus status, MissionActionRequest request, int actor, CancellationToken ct)
    {
        await actions.ValidateAndThrowAsync(request, ct);
        return await repository.InTransactionAsync(async () =>
        {
            var mission = await Find(id, true, ct);
            await lifecycle.ChangeStatusAsync(mission, status, request, actor, null, ct);
            await repository.SaveAsync(ct);
            return await MapAsync(mission, ct);
        }, ct);
    }

    public Task<MissionResponse> PatchStatusAsync(int id, MissionStatusRequest request, int actor, CancellationToken ct)
    {
        if (!Enum.GetNames<MissionStatus>().Contains(request.Status)) throw new ValidationException("Invalid mission status.");
        return ChangeStatusAsync(id, Enum.Parse<MissionStatus>(request.Status), new(null, null, request.Note, null), actor, ct);
    }

    private async Task<Mission> Find(int id, bool write, CancellationToken ct)
    {
        var mission = await repository.FindAsync(id, write, ct) ?? throw new NotFoundException("Mission not found.");
        await access.EnsureFarmAccessAsync(mission.FarmId, ct);
        return mission;
    }
    private void Log(Mission mission, int actor, string type, string message) => repository.AddLog(new MissionLog
    { Mission = mission, UserId = actor, LogType = type, Message = message.Trim(), CreatedAt = clock.GetUtcNow() });

    public async Task<MissionResults> ResultsAsync(int id, CancellationToken ct)
    {
        var mission = await Find(id, false, ct);
        var p = Progress(mission);
        return new(id, mission.Status.ToString(), p.TotalTargets, p.SuccessfulTargets, p.FailedTargets, p.SkippedTargets, p.PendingTargets, await TargetsAsync(mission, ct));
    }
    public async Task<IReadOnlyList<WaypointResponse>> WaypointsAsync(int id, CancellationToken ct) => Waypoints(await Find(id, false, ct));
    public async Task<PagedResult<MissionLogResponse>> LogsAsync(int id, PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        await Find(id, false, ct);
        return await repository.LogsAsync(id, query, ct);
    }
    private static MissionProgress Progress(Mission x) => new(x.Targets.Count, x.Targets.Count(t => t.Status == MissionTargetStatus.COLLECTED),
        x.Targets.Count(t => t.Status == MissionTargetStatus.FAILED), x.Targets.Count(t => t.Status == MissionTargetStatus.SKIPPED), x.Targets.Count(t => t.Status == MissionTargetStatus.PENDING));
    private async Task<TargetResponse[]> TargetsAsync(Mission mission, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var allowed = scope.IsAdministrator ? null : (await access.GetAccessibleFarmIdsAsync(ct)).ToHashSet();
        // Historical target identities remain part of the mission; live metadata follows the sensor's current farm.
        return mission.Targets.OrderBy(t => t.SequenceNo).Select(t => new TargetResponse(t.Id, t.SensorNodeId,
            scope.IsAdministrator || allowed!.Contains(t.SensorNode.Zone.FarmId) ? t.SensorNode.Name : null,
            t.WaypointId, t.SequenceNo, t.Status.ToString())).ToArray();
    }
    private static WaypointResponse[] Waypoints(Mission x) => x.Waypoints.OrderBy(w => w.SequenceNo).Select(w => new WaypointResponse(w.Id, w.SequenceNo, w.Latitude, w.Longitude, w.LocalX, w.LocalY, w.AltitudeM, w.ActionType, w.PlannedHoldSeconds)).ToArray();
    private async Task<MissionResponse> MapAsync(Mission x, CancellationToken ct) => new(x.Id, x.Name, x.Status.ToString(), new(x.FarmId, x.Farm.Name),
        x.Uav is null ? null : new(x.Uav.Id, x.Uav.Name), x.Gateway is null ? null : new(x.Gateway.Id, x.Gateway.Name),
        x.ScheduledStartAt, x.ScheduledEndAt, x.StartedAt, x.CompletedAt, x.CreatedByUserId, x.OperatorNotes, x.FailureReason,
        x.CreatedAt, x.UpdatedAt, await TargetsAsync(x, ct), Waypoints(x), Progress(x), x.PlanRevision);
}
