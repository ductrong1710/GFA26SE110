using System.Globalization;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Interfaces;
using FluentValidation;

namespace FarmMonitoring.Application.Features.SensorData;

public sealed class SensorDataService(ISensorDataRepository repository, ISensorRepository sensors, IMissionRepository missions,
    IValidator<ReadingQuery> queries, IValidator<PageQuery> pages, TimeProvider clock)
{
    public async Task<PagedResult<ReadingResponse>> ListAsync(ReadingQuery query, int? channelId, CancellationToken ct)
    {
        await queries.ValidateAndThrowAsync(query, ct);
        if (channelId.HasValue && await sensors.FindChannelAsync(channelId.Value, ct) is null) throw new NotFoundException("Sensor channel not found.");
        if (channelId.HasValue && query.SensorChannelId.HasValue && query.SensorChannelId != channelId)
            throw new ValidationException("Channel filter does not match the route.");
        return await repository.ListAsync(query, channelId, ct);
    }
    public async Task<IReadOnlyList<LatestChannelResponse>> LatestAsync(int nodeId, CancellationToken ct)
    {
        if (await sensors.FindNodeAsync(nodeId, ct) is null) throw new NotFoundException("Sensor node not found.");
        return await repository.LatestAsync(nodeId, ct);
    }
    public async Task<PagedResult<CollectionAttemptResponse>> AttemptsAsync(int missionId, PageQuery query, CancellationToken ct)
    {
        await pages.ValidateAndThrowAsync(query, ct);
        if (await missions.FindAsync(missionId, false, ct) is null) throw new NotFoundException("Mission not found.");
        return await repository.AttemptsAsync(missionId, query, ct);
    }
    public async Task<IReadOnlyList<ZoneComparisonResponse>> CompareAsync(ZoneComparisonQuery query, CancellationToken ct)
    {
        var parts = (query.ZoneIds ?? "").Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 20 || parts.Any(x => !int.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0) || query.SensorTypeId <= 0)
            throw new ValidationException("Provide 1 to 20 positive zone IDs and a sensor type ID.");
        var ids = parts.Select(x => int.Parse(x, CultureInfo.InvariantCulture)).Distinct().ToArray();
        var to = (query.To ?? clock.GetUtcNow()).ToUniversalTime();
        var from = (query.From ?? (to >= DateTimeOffset.MinValue.AddDays(30) ? to.AddDays(-30) : DateTimeOffset.MinValue)).ToUniversalTime();
        if (from > to || to - from > TimeSpan.FromDays(366)) throw new ValidationException("Comparison range must be chronological and at most 366 days.");
        return await repository.CompareAsync(ids, query.SensorTypeId, from, to, ct);
    }
}
