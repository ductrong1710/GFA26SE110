using System.Text.Json;
using FarmMonitoring.Application.Features.Sync;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.UnitTests;

public class UnresolvedRecordTests
{
    [Fact]
    public async Task Unknown_time_is_archived_idempotently_and_invalid_identity_is_rejected()
    {
        var stored = new List<ArchivedSensorRecord>();
        var batches = new List<SyncBatch>();
        var node = new SensorNode { Id = 1, DeviceCode = "node", IsActive = true, Zone = new Zone { FarmId = 1 } };
        var repo = SyncRetryTests.Proxy<ISyncRepository>((method, args) => method.Name switch
        {
            "FindBatchAsync" => Task.FromResult(batches.SingleOrDefault(x => x.BatchKey == (string)args[1]!)),
            "LockChannelsAsync" => Task.FromResult<IReadOnlyList<SensorChannel>>([]),
            "FindNodeAsync" => Task.FromResult<SensorNode?>(node),
            "FindArchivedAsync" => Task.FromResult(stored.SingleOrDefault(x => x.SourceRecordKey == (string)args[1]!)),
            "AddArchived" => Add(stored, (ArchivedSensorRecord)args[0]!),
            "AddBatch" => Add(batches, (SyncBatch)args[0]!),
            "SaveAsync" => Task.CompletedTask,
            _ => throw new NotSupportedException(method.Name)
        });
        var missions = SyncRetryTests.Proxy<IMissionRepository>((method, args) => method.Name switch
        {
            "InTransactionAsync" => ((Delegate)args[0]!).DynamicInvoke(),
            "LockGatewayAsync" => Task.FromResult<Gateway?>(new Gateway { Id = 1, IsActive = true }),
            _ => throw new NotSupportedException(method.Name)
        });
        var service = new SyncService(repo, missions, new SyncValidator(), TimeProvider.System, null!);
        var payload = JsonSerializer.SerializeToElement(new { deviceCode = "node", recordId = "node-1", sequence = 1,
            measuredAt = 0, collectedAt = 0, timeSynced = false, collectedTimeSynced = false,
            temperature = 23, humidity = (double?)null });
        var request = new SyncRequest("first", null, [], null, [new("node", "node-1", payload)]);
        var response = await service.SynchronizeAsync(1, 1, request, default);
        Assert.Equal("ACCEPTED", Assert.Single(response.UnresolvedRecords!).Status);
        Assert.Equal(0, JsonDocument.Parse(Assert.Single(stored).PayloadJson).RootElement.GetProperty("measuredAt").GetInt64());
        var duplicate = await service.SynchronizeAsync(1, 1, request with { BatchKey = "second" }, default);
        Assert.Equal("DUPLICATE", Assert.Single(duplicate.UnresolvedRecords!).Status);
        Assert.Single(stored);
        var invalid = await service.SynchronizeAsync(1, 1, request with { BatchKey = "bad", UnresolvedRecords = [new("node", "node-2", payload)] }, default);
        Assert.Equal("REJECTED", Assert.Single(invalid.UnresolvedRecords!).Status);
        Assert.Single(stored);
        var wrongType = JsonSerializer.SerializeToElement(new { deviceCode = "node", recordId = "node-1", sequence = "1" });
        var malformed = await service.SynchronizeAsync(1, 1, request with { BatchKey = "wrong-type", UnresolvedRecords = [new("node", "node-1", wrongType)] }, default);
        Assert.Equal("REJECTED", Assert.Single(malformed.UnresolvedRecords!).Status);
        var changedPayload = JsonSerializer.SerializeToElement(new { deviceCode = "node", recordId = "node-1", sequence = 1, temperature = 99 });
        var conflict = await service.SynchronizeAsync(1, 1, request with { BatchKey = "conflict", UnresolvedRecords = [new("node", "node-1", changedPayload)] }, default);
        Assert.Equal("REJECTED", Assert.Single(conflict.UnresolvedRecords!).Status);
        var nullText = JsonSerializer.SerializeToElement(new { deviceCode = "node", recordId = "node-2", sequence = 2, nested = new[] { "bad\0text" } });
        var unsupported = await service.SynchronizeAsync(1, 1, request with { BatchKey = "null-text", UnresolvedRecords = [new("node", "node-2", nullText)] }, default);
        Assert.Equal("REJECTED", Assert.Single(unsupported.UnresolvedRecords!).Status);
        Assert.Single(stored);
        var missingPayload = await service.SynchronizeAsync(1, 1, request with { BatchKey = "missing-payload", UnresolvedRecords = [new("node", "node-2", null)] }, default);
        Assert.Equal("REJECTED", Assert.Single(missingPayload.UnresolvedRecords!).Status);
    }
    private static object? Add<T>(List<T> list, T item) { list.Add(item); return null; }
}
