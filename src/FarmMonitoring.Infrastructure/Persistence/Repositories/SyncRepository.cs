using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class SyncRepository(AppDbContext db) : ISyncRepository
{
    public Task<SyncBatch?> FindBatchAsync(int gatewayId, string key, CancellationToken ct) =>
        db.SyncBatches.SingleOrDefaultAsync(x => x.GatewayId == gatewayId && x.BatchKey == key, ct);
    public async Task<IReadOnlyList<SensorChannel>> LockChannelsAsync(string[] nodeCodes, CancellationToken ct)
    {
        var nodes = await db.SensorNodes.FromSqlInterpolated($"SELECT * FROM sensor_nodes WHERE device_code = ANY ({nodeCodes}) ORDER BY id FOR UPDATE").ToListAsync(ct);
        foreach (var node in nodes)
        {
            await db.Entry(node).ReloadAsync(ct);
            node.Zone = await db.Zones.SingleAsync(x => x.Id == node.ZoneId, ct);
        }
        // Consistent ascending channel locks also serialize source-key reuse across different gateways.
        var channels = await db.SensorChannels.FromSqlInterpolated($"SELECT c.* FROM sensor_channels c JOIN sensor_nodes n ON n.id = c.sensor_node_id WHERE n.device_code = ANY ({nodeCodes}) ORDER BY c.id FOR UPDATE OF c")
            .ToListAsync(ct);
        var ids = channels.Select(x => x.Id).ToArray();
        return await db.SensorChannels.Include(x => x.SensorNode).ThenInclude(x => x.Zone).Where(x => ids.Contains(x.Id)).ToListAsync(ct);
    }
    public async Task<SensorReading?> FindReadingAsync(int channelId, string key, CancellationToken ct) =>
        db.SensorReadings.Local.SingleOrDefault(x => x.SensorChannelId == channelId && x.SourceRecordKey == key)
        ?? await db.SensorReadings.SingleOrDefaultAsync(x => x.SensorChannelId == channelId && x.SourceRecordKey == key, ct);
    public async Task<IReadOnlyList<CollectionAttempt>> AttemptsAsync(int missionId, CancellationToken ct) =>
        await db.CollectionAttempts.Where(x => x.MissionId == missionId).ToListAsync(ct);
    public void AddReading(SensorReading reading) => db.SensorReadings.Add(reading);
    public void AddAttempt(CollectionAttempt attempt) => db.CollectionAttempts.Add(attempt);
    public void AddBatch(SyncBatch batch) => db.SyncBatches.Add(batch);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
