using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Missions;

public sealed class DeviceMissionService(IMissionRepository repository, MissionLifecycle lifecycle, IValidator<MissionActionRequest> actions)
{
    private static void CheckIdentity(int gatewayId, int authenticatedGateway)
    {
        if (gatewayId != authenticatedGateway) throw new AccessDeniedException("Gateway identity does not match the route.");
    }
    public async Task<DeviceMissionResponse?> GetNextForGatewayAsync(int gatewayId, int authenticatedGateway, CancellationToken ct)
    {
        CheckIdentity(gatewayId, authenticatedGateway);
        var mission = await repository.GetNextScheduledForGatewayAsync(gatewayId, ct);
        return mission is null ? null : new(mission.Id, mission.Name, mission.Status.ToString(), mission.GatewayId, mission.UavId,
            mission.ScheduledStartAt, mission.ScheduledEndAt,
            mission.Waypoints.OrderBy(w => w.SequenceNo).Select(w => new WaypointResponse(w.Id, w.SequenceNo, w.Latitude, w.Longitude, w.LocalX, w.LocalY, w.AltitudeM, w.ActionType, w.PlannedHoldSeconds)).ToArray(),
            mission.Targets.OrderBy(t => t.SequenceNo).Select(t => new DeviceMissionTargetResponse(t.Id, t.SensorNodeId, t.SensorNode.DeviceCode,
                t.WaypointId, mission.Waypoints.SingleOrDefault(w => w.Id == t.WaypointId)?.SequenceNo, t.SequenceNo, t.Status.ToString())).ToArray());
    }
    public async Task<DeviceMissionStateResponse> MarkDownloadedAsync(int gatewayId, int authenticatedGateway, int missionId, CancellationToken ct)
    {
        CheckIdentity(gatewayId, authenticatedGateway);
        return await repository.InTransactionAsync(async () =>
        {
            var mission = await FindAssigned(missionId, gatewayId, ct);
            await LockEquipment(mission, gatewayId, ct);
            if (mission.Status != MissionStatus.SCHEDULED) throw new ConflictException("Only scheduled missions can be acknowledged as downloaded.");
            if (!await repository.HasDeviceLogAsync(missionId, gatewayId, "DOWNLOADED", ct))
            {
                lifecycle.Log(mission, null, gatewayId, "DOWNLOADED", "Gateway verified and persisted the mission download.");
                await repository.SaveAsync(ct);
            }
            return State(mission);
        }, ct);
    }
    public Task<DeviceMissionStateResponse> StartAsync(int gatewayId, int authenticatedGateway, int missionId, CancellationToken ct) =>
        Transition(gatewayId, authenticatedGateway, missionId, MissionStatus.RUNNING, new(null, null, null, null), ct);
    public Task<DeviceMissionStateResponse> CompleteAsync(int gatewayId, int authenticatedGateway, int missionId, CancellationToken ct) =>
        Transition(gatewayId, authenticatedGateway, missionId, MissionStatus.COMPLETED, new(null, null, null, null), ct);
    public Task<DeviceMissionStateResponse> FailAsync(int gatewayId, int authenticatedGateway, int missionId, DeviceMissionFailureRequest request, CancellationToken ct) =>
        Transition(gatewayId, authenticatedGateway, missionId, MissionStatus.FAILED, new(null, request.FailureReason, request.Note, null), ct);
    public Task<DeviceMissionStateResponse> CancelAsync(int gatewayId, int authenticatedGateway, int missionId, DeviceMissionCancelRequest request, CancellationToken ct) =>
        Transition(gatewayId, authenticatedGateway, missionId, MissionStatus.CANCELLED, new(null, null, null, request.Reason), ct);

    private async Task<DeviceMissionStateResponse> Transition(int gatewayId, int authenticatedGateway, int missionId,
        MissionStatus status, MissionActionRequest request, CancellationToken ct)
    {
        CheckIdentity(gatewayId, authenticatedGateway);
        return await repository.InTransactionAsync(async () =>
        {
            var mission = await FindAssigned(missionId, gatewayId, ct);
            await actions.ValidateAndThrowAsync(request, ct);
            if (status == MissionStatus.FAILED && string.IsNullOrWhiteSpace(request.FailureReason)) throw new ValidationException("A failure reason is required.");
            await LockEquipment(mission, gatewayId, ct);
            if (mission.Status == status)
            {
                if (status == MissionStatus.FAILED && mission.FailureReason != request.FailureReason!.Trim())
                    throw new ConflictException("Mission already failed with a different reason.");
                return State(mission);
            }
            await lifecycle.ChangeStatusAsync(mission, status, request, null, gatewayId, ct);
            await repository.SaveAsync(ct);
            return State(mission);
        }, ct);
    }
    private async Task<Mission> FindAssigned(int missionId, int gatewayId, CancellationToken ct)
    {
        var mission = await repository.FindAsync(missionId, true, ct) ?? throw new NotFoundException("Mission not found.");
        if (mission.GatewayId != gatewayId) throw new AccessDeniedException("Gateway is not assigned to this mission.");
        return mission;
    }
    private async Task LockEquipment(Mission mission, int gatewayId, CancellationToken ct)
    {
        // Match SyncService and human transitions: mission -> UAV -> gateway.
        if (mission.UavId is int uavId) await repository.LockUavAsync(uavId, ct);
        if (await repository.LockGatewayAsync(gatewayId, ct) is not { IsActive: true }) throw new AccessDeniedException("Gateway is inactive.");
    }
    private static DeviceMissionStateResponse State(Mission m) => new(m.Id, m.Status.ToString(), m.StartedAt, m.CompletedAt, m.FailureReason);
}
