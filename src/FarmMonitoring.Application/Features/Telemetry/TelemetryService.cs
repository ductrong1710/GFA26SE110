using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Telemetry;

public sealed class TelemetryService(FarmAccessService access, ITelemetryRepository repository, IMissionRepository missions,
    IValidator<TelemetryRequest> validator, IValidator<TelemetryQuery> queries, TimeProvider clock)
{
    public async Task<TelemetryResponse> RecordAsync(int authenticatedGateway, TelemetryRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return await missions.InTransactionAsync(async () =>
        {
            var mission = await missions.FindAsync(request.MissionId, true, ct) ?? throw new NotFoundException("Mission not found.");
            if ((request.GatewayId.HasValue && request.GatewayId != authenticatedGateway) || mission.GatewayId != authenticatedGateway)
                throw new AccessDeniedException("Gateway is not assigned to this mission.");
            if (request.UavId.HasValue && request.UavId != mission.UavId)
                throw new BusinessRuleException("UAV must match the mission assignment.");
            if (mission.UavId is int uavId) await missions.LockUavAsync(uavId, ct);
            var gateway = await missions.LockGatewayAsync(authenticatedGateway, ct);
            if (gateway is not { IsActive: true }) throw new AccessDeniedException("Gateway is inactive.");
            gateway.LastSeenAt = clock.GetUtcNow();
            var record = new TelemetryRecord
            {
                MissionId = request.MissionId, GatewayId = authenticatedGateway, UavId = mission.UavId,
                RecordedAt = request.RecordedAt.ToUniversalTime(), Latitude = request.Latitude, Longitude = request.Longitude,
                LocalX = request.LocalX, LocalY = request.LocalY, AltitudeM = request.AltitudeM,
                BatteryPercent = request.BatteryPercent, CurrentWaypointNo = request.CurrentWaypointNo, FlightStatus = request.FlightStatus?.Trim()
            };
            await repository.AddAsync(record, ct);
            return new TelemetryResponse(record.Id, record.MissionId, record.UavId, record.GatewayId, record.RecordedAt,
                record.Latitude, record.Longitude, record.LocalX, record.LocalY, record.AltitudeM, record.BatteryPercent, record.CurrentWaypointNo, record.FlightStatus);
        }, ct);
    }
    public async Task<TelemetryResponse> LatestAsync(int missionId, CancellationToken ct)
    {
        await RequireMission(missionId, ct);
        return await repository.LatestAsync(missionId, ct) ?? throw new NotFoundException("Mission telemetry not found.");
    }
    public async Task<PagedResult<TelemetryResponse>> ListAsync(int missionId, TelemetryQuery query, CancellationToken ct)
    {
        await queries.ValidateAndThrowAsync(query, ct);
        await RequireMission(missionId, ct);
        return await repository.ListAsync(missionId, query, ct);
    }
    private async Task RequireMission(int id, CancellationToken ct)
    {
        await access.EnsureAsync(FarmResource.Mission, id, ct);
    }
}
