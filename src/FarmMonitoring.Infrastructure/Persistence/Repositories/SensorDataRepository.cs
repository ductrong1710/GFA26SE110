using System.Linq.Expressions;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.SensorData;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FarmMonitoring.Infrastructure.Persistence.Repositories;

public sealed class SensorDataRepository(AppDbContext db) : ISensorDataRepository
{
    private static readonly Expression<Func<SensorReading, ReadingResponse>> Projection = x => new(x.Id, x.SensorChannelId,
        x.SensorChannel.SensorNodeId, x.SensorChannel.ChannelCode, x.SensorChannel.SensorType.Unit, x.GatewayId, x.MissionId,
        x.SourceRecordKey, x.Value, x.MeasuredAt, x.CollectedAt, x.ReceivedAt, x.QualityStatus, x.IsValid, x.ValidationError);
    public async Task<PagedResult<ReadingResponse>> ListAsync(ReadingQuery query, int? channelId, CancellationToken ct)
    {
        var rows = db.SensorReadings.AsNoTracking();
        var channel = channelId ?? query.SensorChannelId;
        if (channel.HasValue) rows = rows.Where(x => x.SensorChannelId == channel);
        if (query.SensorNodeId.HasValue) rows = rows.Where(x => x.SensorChannel.SensorNodeId == query.SensorNodeId);
        if (query.FarmId.HasValue) rows = rows.Where(x => x.SensorChannel.SensorNode.Zone.FarmId == query.FarmId);
        if (query.ZoneId.HasValue) rows = rows.Where(x => x.SensorChannel.SensorNode.ZoneId == query.ZoneId);
        if (query.MissionId.HasValue) rows = rows.Where(x => x.MissionId == query.MissionId);
        if (query.From.HasValue) { var from = query.From.Value.ToUniversalTime(); rows = rows.Where(x => x.MeasuredAt >= from); }
        if (query.To.HasValue) { var to = query.To.Value.ToUniversalTime(); rows = rows.Where(x => x.MeasuredAt <= to); }
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.MeasuredAt).ThenByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(Projection).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<IReadOnlyList<LatestChannelResponse>> LatestAsync(int nodeId, CancellationToken ct)
    {
        var channels = await db.SensorChannels.AsNoTracking().Where(x => x.SensorNodeId == nodeId).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.ChannelCode, x.SensorType.Unit }).ToArrayAsync(ct);
        var readings = await db.SensorReadings.AsNoTracking().Where(x => x.SensorChannel.SensorNodeId == nodeId && x.IsValid &&
            !db.SensorReadings.Any(other => other.SensorChannelId == x.SensorChannelId && other.IsValid &&
                (other.MeasuredAt > x.MeasuredAt || (other.MeasuredAt == x.MeasuredAt && other.Id > x.Id)))).Select(Projection).ToArrayAsync(ct);
        return channels.Select(x => new LatestChannelResponse(x.Id, x.ChannelCode, x.Unit, readings.SingleOrDefault(r => r.SensorChannelId == x.Id))).ToArray();
    }
    public async Task<PagedResult<CollectionAttemptResponse>> AttemptsAsync(int missionId, PageQuery query, CancellationToken ct)
    {
        var rows = db.CollectionAttempts.AsNoTracking().Where(x => x.MissionId == missionId);
        var count = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new CollectionAttemptResponse(x.Id, x.MissionId, x.MissionTargetId, x.GatewayId, x.AttemptNo, x.StartedAt, x.FinishedAt, x.Status, x.RecordsReceived, x.ErrorCode, x.ErrorMessage)).ToArrayAsync(ct);
        return new(items, count, query.Page, query.PageSize);
    }
    public async Task<IReadOnlyList<ZoneComparisonResponse>> CompareAsync(int[] zoneIds, int typeId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var type = await db.SensorTypes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == typeId, ct) ?? throw new NotFoundException("Sensor type not found.");
        var zones = await db.Zones.AsNoTracking().Where(x => zoneIds.Contains(x.Id)).OrderBy(x => x.Id).Select(x => new { x.Id, x.Name }).ToArrayAsync(ct);
        if (zones.Length != zoneIds.Length) throw new NotFoundException("A selected zone was not found.");
        var summaries = await db.SensorReadings.AsNoTracking().Where(x => x.IsValid && x.SensorChannel.SensorTypeId == typeId
            && zoneIds.Contains(x.SensorChannel.SensorNode.ZoneId) && x.MeasuredAt >= from && x.MeasuredAt <= to)
            .GroupBy(x => x.SensorChannel.SensorNode.ZoneId).Select(g => new { ZoneId = g.Key, Count = g.LongCount(), Min = g.Min(x => x.Value), Max = g.Max(x => x.Value), Average = g.Average(x => x.Value) }).ToArrayAsync(ct);
        return zones.Select(z => { var s = summaries.SingleOrDefault(x => x.ZoneId == z.Id); return new ZoneComparisonResponse(z.Id, z.Name, typeId, type.Unit, from, to, s?.Count ?? 0, s?.Min, s?.Max, s?.Average); }).ToArray();
    }
}
