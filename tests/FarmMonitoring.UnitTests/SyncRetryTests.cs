using System.Reflection;
using System.Text.Json;
using FarmMonitoring.Application.Features.Alerts;
using FarmMonitoring.Application.Features.Sync;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;

namespace FarmMonitoring.UnitTests;

public class SyncRetryTests
{
    [Fact]
    public async Task Partial_batch_retries_rejected_records_after_provisioning_without_duplicating_accepted_records()
    {
        var channels = new List<SensorChannel> { Channel(1, "temperature") };
        var readings = new List<SensorReading>();
        SyncBatch? batch = null;
        var repo = Proxy<ISyncRepository>((method, args) => method.Name switch
        {
            "FindBatchAsync" => Task.FromResult(batch),
            "LockChannelsAsync" => Task.FromResult<IReadOnlyList<SensorChannel>>(channels),
            "FindReadingAsync" => Task.FromResult(readings.SingleOrDefault(x => x.SensorChannelId == (int)args[0]! && x.SourceRecordKey == (string)args[1]!)),
            "AddReading" => Add(readings, (SensorReading)args[0]!),
            "AddBatch" => batch is null ? batch = (SyncBatch)args[0]! : throw new InvalidOperationException("Duplicate batch"),
            "SaveAsync" => Task.CompletedTask,
            _ => throw new NotSupportedException(method.Name)
        });
        var missions = Proxy<IMissionRepository>((method, args) => method.Name switch
        {
            "InTransactionAsync" => ((Delegate)args[0]!).DynamicInvoke(),
            "LockGatewayAsync" => Task.FromResult<Gateway?>(new Gateway { Id = 1, IsActive = true }),
            _ => throw new NotSupportedException(method.Name)
        });
        var alerts = new AlertService(null!, Proxy<IAlertRepository>((_, _) => Task.FromResult<SensorThreshold?>(null)), null!, null!, TimeProvider.System, null!);
        var service = new SyncService(repo, missions, new SyncValidator(), TimeProvider.System, alerts);
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O");
        SyncRecordRequest Record(string code) => new("node", code, "node-1", JsonSerializer.SerializeToElement(20), timestamp, timestamp, "VALID");
        var request = new SyncRequest("same-batch", null, [Record("temperature"), Record("humidity")], null);
        var first = await service.SynchronizeAsync(1, 1, request, default);
        Assert.Equal(1, first.Accepted); Assert.Equal(1, first.Rejected);
        channels.Add(Channel(2, "humidity"));
        var retry = await service.SynchronizeAsync(1, 1, request, default);
        Assert.Equal(2, retry.Accepted); Assert.Equal(0, retry.Rejected);
        Assert.Equal(2, readings.Count);
        Assert.Equal(JsonSerializer.Serialize(retry), JsonSerializer.Serialize(await service.SynchronizeAsync(1, 1, request, default)));
        Assert.Equal(2, readings.Count);
    }

    private static SensorChannel Channel(int id, string code) => new() { Id = id, ChannelCode = code, IsActive = true,
        SensorNode = new SensorNode { DeviceCode = "node", IsActive = true } };
    private static object? Add<T>(List<T> list, T value) { list.Add(value); return null; }
    internal static T Proxy<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, ScriptedRepository>();
        ((ScriptedRepository)(object)proxy).Handler = handler;
        return proxy;
    }
    public class ScriptedRepository : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Handler = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args!);
    }
}
