using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.SensorData;

namespace FarmMonitoring.Application.Interfaces;

public interface ISensorDataRepository
{
    Task<PagedResult<ReadingResponse>> ListAsync(ReadingQuery query, int? channelId, CancellationToken ct);
    Task<IReadOnlyList<LatestChannelResponse>> LatestAsync(int nodeId, CancellationToken ct);
    Task<PagedResult<CollectionAttemptResponse>> AttemptsAsync(int missionId, PageQuery query, CancellationToken ct);
    Task<IReadOnlyList<ZoneComparisonResponse>> CompareAsync(int[] zoneIds, int typeId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
