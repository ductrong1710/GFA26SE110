using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Missions;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class MissionRepository(AppDbContext db) : IMissionRepository
{
    public async Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await FlightReservation.LockAsync(db, ct);
        var result = await action();
        await transaction.CommitAsync(ct);
        return result;
    }
    public async Task<Mission?> FindAsync(int id, bool forUpdate, CancellationToken ct)
    {
        if (forUpdate)
        {
            // Lock before loading the graph so concurrent transitions see the committed predecessor state.
            var locked = await db.Missions.FromSqlInterpolated($"SELECT * FROM missions WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
            if (locked is null) return null;
            await db.Entry(locked).ReloadAsync(ct);
        }
        var query = db.Missions.AsQueryable();
        if (!forUpdate) query = query.AsNoTracking();
        return await query.Include(x => x.Farm).Include(x => x.Uav).Include(x => x.Gateway)
            .Include(x => x.Targets).ThenInclude(x => x.SensorNode).ThenInclude(x => x.Zone).Include(x => x.Waypoints)
            .AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id, ct);
    }
    public async Task<Mission?> GetNextScheduledForGatewayAsync(int gatewayId, CancellationToken ct)
    {
        // All split-query SELECTs must see the same plan even during a web update.
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var mission = await db.Missions.AsNoTracking().Where(x => x.GatewayId == gatewayId && x.Status == MissionStatus.SCHEDULED)
            .OrderBy(x => x.ScheduledStartAt).ThenBy(x => x.Id)
            .Include(x => x.Gateway).Include(x => x.Uav).Include(x => x.Waypoints)
            .Include(x => x.Targets).ThenInclude(x => x.SensorNode)
            .AsSplitQuery().FirstOrDefaultAsync(ct);
        await snapshot.CommitAsync(ct);
        return mission;
    }
    public Task<bool> HasDeviceLogAsync(int missionId, int gatewayId, string logType, CancellationToken ct) =>
        db.MissionLogs.AnyAsync(x => x.MissionId == missionId && x.GatewayId == gatewayId && x.UserId == null && x.LogType == logType, ct);
    public Task<Farm?> GetFarmAsync(int id, CancellationToken ct) => db.Farms.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<SensorNode>> GetSensorsAsync(int[] ids, CancellationToken ct)
    {
        var nodes = await db.SensorNodes.FromSqlInterpolated($"SELECT * FROM sensor_nodes WHERE id = ANY ({ids}) ORDER BY id FOR UPDATE").ToListAsync(ct);
        foreach (var node in nodes)
        {
            await db.Entry(node).ReloadAsync(ct);
            node.Zone = await db.Zones.SingleAsync(x => x.Id == node.ZoneId, ct);
        }
        return nodes;
    }
    public async Task<Uav?> LockUavAsync(int id, CancellationToken ct)
    {
        var uav = await db.Uavs.FromSqlInterpolated($"SELECT * FROM uavs WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (uav is not null) await db.Entry(uav).ReloadAsync(ct);
        return uav;
    }
    public async Task<Gateway?> LockGatewayAsync(int id, CancellationToken ct)
    {
        var gateway = await db.Gateways.FromSqlInterpolated($"SELECT * FROM gateways WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (gateway is not null) await db.Entry(gateway).ReloadAsync(ct);
        return gateway;
    }
    public async Task<bool> HasOverlapAsync(Mission mission, bool starting, CancellationToken ct) =>
        await FlightReservation.Active(db, DateTimeOffset.UtcNow).AnyAsync(x => x.UavId == mission.UavId || x.GatewayId == mission.GatewayId, ct) ||
        await db.Missions.AnyAsync(x =>
        x.Id != mission.Id && (x.Status == MissionStatus.SCHEDULED || x.Status == MissionStatus.RUNNING) &&
        ((mission.UavId != null && x.UavId == mission.UavId) || (mission.GatewayId != null && x.GatewayId == mission.GatewayId)) &&
        ((starting && x.Status == MissionStatus.RUNNING) || (x.ScheduledStartAt < mission.ScheduledEndAt && x.ScheduledEndAt > mission.ScheduledStartAt)), ct);
    public async Task ReplacePlanAsync(Mission mission, IReadOnlyList<MissionTarget> targets, IReadOnlyList<MissionWaypoint> waypoints, CancellationToken ct)
    {
        if (mission.Id != 0)
        {
            db.MissionTargets.RemoveRange(mission.Targets);
            db.MissionWaypoints.RemoveRange(mission.Waypoints);
            await db.SaveChangesAsync(ct);
        }
        mission.Targets = targets.ToList();
        mission.Waypoints = waypoints.ToList();
    }
    public void Add(Mission mission) => db.Missions.Add(mission);
    public void AddLog(MissionLog log) => db.MissionLogs.Add(log);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public async Task<PagedResult<MissionSummary>> ListAsync(MissionQuery query, FarmAccessScope scope, CancellationToken ct)
    {
        var missions = db.Missions.AsNoTracking().ForFarms(db, scope, x => x.FarmId);
        if (query.FarmId.HasValue) missions = missions.Where(x => x.FarmId == query.FarmId);
        if (query.UavId.HasValue) missions = missions.Where(x => x.UavId == query.UavId);
        if (query.GatewayId.HasValue) missions = missions.Where(x => x.GatewayId == query.GatewayId);
        if (query.Status is not null) { var status = Enum.Parse<MissionStatus>(query.Status); missions = missions.Where(x => x.Status == status); }
        if (query.ScheduledFrom.HasValue) { var from = query.ScheduledFrom.Value.ToUniversalTime(); missions = missions.Where(x => x.ScheduledStartAt >= from); }
        if (query.ScheduledTo.HasValue) { var to = query.ScheduledTo.Value.ToUniversalTime(); missions = missions.Where(x => x.ScheduledStartAt <= to); }
        if (!string.IsNullOrWhiteSpace(query.Search)) missions = missions.Where(x => x.Name.Contains(query.Search.Trim()));
        var count = await missions.CountAsync(ct);
        var items = await missions.OrderByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new MissionSummary(x.Id, x.Name, x.FarmId, x.UavId, x.GatewayId, x.Status.ToString(), x.ScheduledStartAt, x.ScheduledEndAt, x.StartedAt, x.CompletedAt)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<PagedResult<MissionLogResponse>> LogsAsync(int id, PageQuery query, CancellationToken ct)
    {
        var logs = db.MissionLogs.AsNoTracking().Where(x => x.MissionId == id);
        if (!string.IsNullOrWhiteSpace(query.Search)) logs = logs.Where(x => x.Message.Contains(query.Search.Trim()));
        var count = await logs.CountAsync(ct);
        var items = await logs.OrderByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new MissionLogResponse(x.Id, x.MissionId, x.UserId, x.GatewayId, x.LogType, x.Message, x.CreatedAt)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
}
