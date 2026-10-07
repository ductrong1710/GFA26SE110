using FarmMonitoring.Application.Features.Farms;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Sensors;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class SensorRepository(AppDbContext db) : ISensorRepository
{
    public async Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await action();
        await transaction.CommitAsync(ct);
        return result;
    }
    public async Task<SensorNode?> LockNodeAsync(int id, CancellationToken ct)
    {
        var node = await db.SensorNodes.FromSqlInterpolated($"SELECT * FROM sensor_nodes WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (node is not null) await db.Entry(node).ReloadAsync(ct);
        return node;
    }
    public async Task<SensorChannel?> LockChannelAsync(int id, CancellationToken ct)
    {
        var channel = await db.SensorChannels.FromSqlInterpolated($"SELECT * FROM sensor_channels WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (channel is not null) await db.Entry(channel).ReloadAsync(ct);
        return channel;
    }
    public Task<bool> ConflictsWithMissionAsync(int nodeId, int destinationZoneId, CancellationToken ct) =>
        db.MissionTargets.AnyAsync(x => x.SensorNodeId == nodeId
            && (x.Mission.Status == MissionStatus.PENDING || x.Mission.Status == MissionStatus.SCHEDULED || x.Mission.Status == MissionStatus.RUNNING)
            && !db.Zones.Any(z => z.Id == destinationZoneId && z.FarmId == x.Mission.FarmId), ct);
    public Task<bool> HasReadingsAsync(int channelId, CancellationToken ct) => db.SensorReadings.AnyAsync(x => x.SensorChannelId == channelId, ct);
    public async Task<PagedResult<SensorTypeResponse>> ListTypesAsync(PageQuery query, CancellationToken ct)
    {
        var rows = db.SensorTypes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            rows = rows.Where(x => x.Code.ToLower().Contains(term) || x.Name.ToLower().Contains(term));
        }
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new SensorTypeResponse(x.Id, x.Code, x.Name, x.Unit, x.Description)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<PagedResult<SensorNodeResponse>> ListNodesAsync(SensorNodeQuery query, FarmAccessScope scope, CancellationToken ct)
    {
        var rows = db.SensorNodes.AsNoTracking().ForFarms(db, scope, x => x.Zone.FarmId);
        if (query.ZoneId is not null) rows = rows.Where(x => x.ZoneId == query.ZoneId);
        if (query.FarmId is not null) rows = rows.Where(x => x.Zone.FarmId == query.FarmId);
        if (query.IsActive is not null) rows = rows.Where(x => x.IsActive == query.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            rows = rows.Where(x => x.DeviceCode.ToLower().Contains(term) || x.Name.ToLower().Contains(term));
        }
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new SensorNodeResponse(x.Id, x.ZoneId, x.DeviceCode, x.Name, x.Status, x.Latitude, x.Longitude,
                x.LocalX, x.LocalY, x.LastSeenAt, x.BatteryPercent, x.IsActive, x.CreatedAt, x.UpdatedAt)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<PagedResult<SensorChannelResponse>> ListChannelsAsync(int nodeId, PageQuery query, CancellationToken ct)
    {
        var rows = db.SensorChannels.AsNoTracking().Where(x => x.SensorNodeId == nodeId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            rows = rows.Where(x => x.ChannelCode.ToLower().Contains(term) || (x.Name != null && x.Name.ToLower().Contains(term)));
        }
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new SensorChannelResponse(x.Id, x.SensorNodeId, x.SensorTypeId, x.ChannelCode, x.Name, x.IsActive, x.CreatedAt, x.UpdatedAt)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public Task<SensorNode?> FindNodeAsync(int id, CancellationToken ct) => db.SensorNodes.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<SensorChannel?> FindChannelAsync(int id, CancellationToken ct) => db.SensorChannels.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> ZoneExistsAsync(int id, CancellationToken ct) => db.Zones.AnyAsync(x => x.Id == id, ct);
    public Task<bool> TypeExistsAsync(int id, CancellationToken ct) => db.SensorTypes.AnyAsync(x => x.Id == id, ct);
    public void AddType(SensorType type) => db.SensorTypes.Add(type);
    public void AddNode(SensorNode node) => db.SensorNodes.Add(node);
    public void AddChannel(SensorChannel channel) => db.SensorChannels.Add(channel);
    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
            && pg.ConstraintName is "IX_sensor_types_code" or "IX_sensor_nodes_device_code" or "IX_sensor_channels_sensor_node_id_channel_code")
        { throw new ConflictException("The code is already registered for this resource."); }
    }
}
