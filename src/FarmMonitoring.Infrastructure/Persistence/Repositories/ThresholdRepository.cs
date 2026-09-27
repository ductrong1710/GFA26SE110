using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class ThresholdRepository(AppDbContext db) : IThresholdRepository
{
    public Task<SensorThreshold?> GetAsync(int channelId, CancellationToken ct) =>
        db.SensorThresholds.AsNoTracking().SingleOrDefaultAsync(x => x.SensorChannelId == channelId, ct);

    public async Task<SensorThreshold?> UpsertAsync(SensorThreshold threshold, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Lock the parent even on the first write, when there is no threshold row yet.
        var channel = await db.SensorChannels.FromSqlInterpolated($"SELECT * FROM sensor_channels WHERE id = {threshold.SensorChannelId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (channel is null) return null;
        var stored = await db.SensorThresholds.SingleOrDefaultAsync(x => x.SensorChannelId == threshold.SensorChannelId, ct);
        if (stored is null)
        {
            stored = threshold;
            db.SensorThresholds.Add(stored);
        }
        else
        {
            stored.MinValue = threshold.MinValue;
            stored.MaxValue = threshold.MaxValue;
            stored.DataTimeoutMinutes = threshold.DataTimeoutMinutes;
            stored.LowBatteryPercent = threshold.LowBatteryPercent;
            stored.IsEnabled = threshold.IsEnabled;
            stored.UpdatedAt = threshold.CreatedAt;
        }
        await db.SaveChangesAsync(ct);
        // Return PostgreSQL's persisted timestamp/numeric precision, consistently
        // with subsequent GET responses and with the existing creation timestamp.
        await db.Entry(stored).ReloadAsync(ct);
        await transaction.CommitAsync(ct);
        return stored;
    }
}
