using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Sensors;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface ISensorRepository
{
    Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct);
    Task<SensorNode?> LockNodeAsync(int id, CancellationToken ct);
    Task<SensorChannel?> LockChannelAsync(int id, CancellationToken ct);
    Task<bool> ConflictsWithMissionAsync(int nodeId, int destinationZoneId, CancellationToken ct);
    Task<bool> HasReadingsAsync(int channelId, CancellationToken ct);
    Task<PagedResult<SensorTypeResponse>> ListTypesAsync(PageQuery query, CancellationToken ct);
    Task<PagedResult<SensorNodeResponse>> ListNodesAsync(SensorNodeQuery query, CancellationToken ct);
    Task<PagedResult<SensorChannelResponse>> ListChannelsAsync(int nodeId, PageQuery query, CancellationToken ct);
    Task<SensorNode?> FindNodeAsync(int id, CancellationToken ct);
    Task<SensorChannel?> FindChannelAsync(int id, CancellationToken ct);
    Task<bool> ZoneExistsAsync(int id, CancellationToken ct);
    Task<bool> TypeExistsAsync(int id, CancellationToken ct);
    void AddType(SensorType type);
    void AddNode(SensorNode node);
    void AddChannel(SensorChannel channel);
    Task SaveAsync(CancellationToken ct);
}
