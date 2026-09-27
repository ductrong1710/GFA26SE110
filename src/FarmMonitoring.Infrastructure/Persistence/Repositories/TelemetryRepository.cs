using System.Linq.Expressions;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Telemetry;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class TelemetryRepository(AppDbContext db) : ITelemetryRepository
{
    private static readonly Expression<Func<TelemetryRecord, TelemetryResponse>> Projection = x =>
        new(x.Id, x.MissionId, x.UavId, x.GatewayId, x.RecordedAt, x.Latitude, x.Longitude, x.LocalX, x.LocalY,
            x.AltitudeM, x.BatteryPercent, x.CurrentWaypointNo, x.FlightStatus);
    public async Task AddAsync(TelemetryRecord record, CancellationToken ct)
    {
        db.TelemetryRecords.Add(record);
        await db.SaveChangesAsync(ct);
        await db.Entry(record).ReloadAsync(ct);
    }
    public Task<TelemetryResponse?> LatestAsync(int missionId, CancellationToken ct) => db.TelemetryRecords.AsNoTracking()
        .Where(x => x.MissionId == missionId).OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id).Select(Projection).FirstOrDefaultAsync(ct);
    public async Task<PagedResult<TelemetryResponse>> ListAsync(int missionId, TelemetryQuery query, CancellationToken ct)
    {
        var records = db.TelemetryRecords.AsNoTracking().Where(x => x.MissionId == missionId);
        if (query.From.HasValue) { var from = query.From.Value.ToUniversalTime(); records = records.Where(x => x.RecordedAt >= from); }
        if (query.To.HasValue) { var to = query.To.Value.ToUniversalTime(); records = records.Where(x => x.RecordedAt <= to); }
        var count = await records.CountAsync(ct);
        var items = await records.OrderByDescending(x => x.RecordedAt).ThenByDescending(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(Projection).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
}
