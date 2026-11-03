using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Thresholds;

namespace FarmMonitoring.Application.Interfaces;

public interface IThresholdRepository
{
    Task<SensorThreshold?> GetAsync(int channelId, CancellationToken ct);
    Task<SensorThreshold?> UpsertAsync(SensorThreshold threshold, int actor, CancellationToken ct);
    Task<PagedResult<ThresholdChangeResponse>> HistoryAsync(int channelId, PageQuery page, CancellationToken ct);
}
