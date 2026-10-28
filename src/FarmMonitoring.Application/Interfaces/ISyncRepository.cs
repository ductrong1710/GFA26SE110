using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.Application.Interfaces;

public interface ISyncRepository
{
    Task<SyncBatch?> FindBatchAsync(int gatewayId, string key, CancellationToken ct);
    Task<IReadOnlyList<SensorChannel>> LockChannelsAsync(string[] nodeCodes, CancellationToken ct);
    Task<SensorReading?> FindReadingAsync(int channelId, string key, CancellationToken ct);
    Task<IReadOnlyList<CollectionAttempt>> AttemptsAsync(int missionId, CancellationToken ct);
    void AddReading(SensorReading reading);
    void AddAttempt(CollectionAttempt attempt);
    void AddBatch(SyncBatch batch);
    Task<SensorNode?> FindNodeAsync(string code, CancellationToken ct);
    Task<ArchivedSensorRecord?> FindArchivedAsync(int nodeId, string key, CancellationToken ct);
    void AddArchived(ArchivedSensorRecord record);
    Task SaveAsync(CancellationToken ct);
}
