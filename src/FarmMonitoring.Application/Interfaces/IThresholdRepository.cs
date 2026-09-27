using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface IThresholdRepository
{
    Task<SensorThreshold?> GetAsync(int channelId, CancellationToken ct);
    Task<SensorThreshold?> UpsertAsync(SensorThreshold threshold, CancellationToken ct);
}
