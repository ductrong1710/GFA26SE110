using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Telemetry;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface ITelemetryRepository
{
    Task AddAsync(TelemetryRecord record, CancellationToken ct);
    Task<TelemetryResponse?> LatestAsync(int missionId, CancellationToken ct);
    Task<PagedResult<TelemetryResponse>> ListAsync(int missionId, TelemetryQuery query, CancellationToken ct);
}
