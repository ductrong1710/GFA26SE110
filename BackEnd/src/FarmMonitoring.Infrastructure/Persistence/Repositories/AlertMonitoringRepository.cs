using FarmMonitoring.Application.Features.Alerts;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class AlertMonitoringRepository(AppDbContext db) : IAlertMonitoringRepository
{
    public Task<int[]> ListIdsAsync(MonitoringResource resource, int after, CancellationToken ct)
    {
        var ids = resource switch
        {
            MonitoringResource.SensorChannel => db.SensorThresholds.Where(x => x.IsEnabled && x.SensorChannel.IsActive && x.SensorChannel.SensorNode.IsActive)
                .Select(x => x.SensorChannelId),
            MonitoringResource.Gateway => db.Gateways.Where(x => x.IsActive).Select(x => x.Id),
            _ => db.Uavs.Where(x => x.IsActive).Select(x => x.Id)
        };
        return ids.Where(id => id > after).OrderBy(id => id).Take(100).ToArrayAsync(ct);
    }
    public async Task<SensorMonitor?> LockSensorAsync(int channelId, CancellationToken ct)
    {
        await db.SensorChannels.FromSqlInterpolated($"SELECT * FROM sensor_channels WHERE id = {channelId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct);
        var threshold = await db.SensorThresholds.AsNoTracking().Include(x => x.SensorChannel).ThenInclude(x => x.SensorNode)
            .SingleOrDefaultAsync(x => x.SensorChannelId == channelId && x.IsEnabled && x.SensorChannel.IsActive && x.SensorChannel.SensorNode.IsActive, ct);
        if (threshold is null) return null;
        var latest = await db.SensorReadings.Where(x => x.SensorChannelId == channelId && x.IsValid).MaxAsync(x => (DateTimeOffset?)x.MeasuredAt, ct);
        return new(threshold, latest);
    }
    public Task<Gateway?> LockGatewayAsync(int id, CancellationToken ct) => db.Gateways
        .FromSqlInterpolated($"SELECT * FROM gateways WHERE id = {id} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct);
    public Task<Uav?> LockUavAsync(int id, CancellationToken ct) => db.Uavs
        .FromSqlInterpolated($"SELECT * FROM uavs WHERE id = {id} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct);
}
