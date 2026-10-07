using FarmMonitoring.Application.Common;
using FarmMonitoring.Application.Features.Sensors;
using FarmMonitoring.Application.Features.Missions;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class SensorDataIntegrityTests(ApiFactory factory)
{
    [Fact]
    public async Task Concurrent_plan_and_reassignment_cannot_commit_conflicting_farms()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var node = Node();
        var other = Node();
        db.SensorNodes.AddRange(node, other);
        await db.SaveChangesAsync();
        var actor = await db.Users.Where(x => x.Email == "admin@example.com").Select(x => x.Id).SingleAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Plan()
        {
            await using var actionScope = factory.Services.CreateAsyncScope();
            await gate.Task;
            try
            {
                await ApiFactory.HumanService<MissionService>(actionScope.ServiceProvider, actor).CreateAsync(
                    new("Concurrent plan", node.Zone.FarmId, null, null, null, null, [node.Id], [], null), actor, default);
                return true;
            }
            catch (BusinessRuleException) { return false; }
        }
        async Task<bool> Move()
        {
            await using var actionScope = factory.Services.CreateAsyncScope();
            await gate.Task;
            try
            {
                await ApiFactory.HumanService<SensorService>(actionScope.ServiceProvider, actor).UpdateNodeAsync(node.Id,
                    new(other.ZoneId, node.DeviceCode, node.Name, node.Status, null, null, null, null, null, null), default);
                return true;
            }
            catch (ConflictException) { return false; }
        }
        var plan = Plan(); var move = Move(); gate.SetResult();
        var results = await Task.WhenAll(plan, move).WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Single(results, x => x);
        Assert.False(await db.MissionTargets.AnyAsync(x => x.SensorNodeId == node.Id && x.Mission.FarmId != x.SensorNode.Zone.FarmId));
    }

    [Fact]
    public async Task Channel_with_readings_cannot_change_measurement_type()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var channel = new SensorChannel { ChannelCode = "value", CreatedAt = DateTimeOffset.UtcNow,
            SensorType = new SensorType { Code = Guid.NewGuid().ToString("N"), Name = "Temperature", Unit = "C" },
            SensorNode = Node() };
        var newType = new SensorType { Code = Guid.NewGuid().ToString("N"), Name = "Humidity", Unit = "%" };
        db.SensorChannels.Add(channel); db.SensorTypes.Add(newType);
        await db.SaveChangesAsync();
        db.SensorReadings.Add(new SensorReading { SensorChannelId = channel.Id, SourceRecordKey = Guid.NewGuid().ToString("N"),
            Value = 25, MeasuredAt = DateTimeOffset.UtcNow, CollectedAt = DateTimeOffset.UtcNow, ReceivedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        await using var editScope = factory.Services.CreateAsyncScope();
        var service = ApiFactory.HumanService<SensorService>(editScope.ServiceProvider, await db.Users.Where(x => x.Email == "admin@example.com").Select(x => x.Id).SingleAsync());
        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateChannelAsync(channel.Id, new(newType.Id, "value", "Changed"), default));
        await service.UpdateChannelAsync(channel.Id, new(channel.SensorTypeId, "value", "Renamed"), default);
        Assert.Equal(channel.SensorTypeId, (await db.SensorChannels.AsNoTracking().SingleAsync(x => x.Id == channel.Id)).SensorTypeId);
    }

    [Theory]
    [InlineData(MissionStatus.PENDING)]
    [InlineData(MissionStatus.SCHEDULED)]
    [InlineData(MissionStatus.RUNNING)]
    public async Task Unfinished_mission_prevents_cross_farm_sensor_move(MissionStatus status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var node = Node();
        var otherZone = new Zone { Name = "Other zone", Farm = new Farm { Name = "Other farm", CreatedAt = DateTimeOffset.UtcNow }, CreatedAt = DateTimeOffset.UtcNow };
        db.SensorNodes.Add(node); db.Zones.Add(otherZone);
        await db.SaveChangesAsync();
        var mission = new Mission { Name = "Protected mission", FarmId = node.Zone.FarmId, Status = status,
            CreatedByUserId = await db.Users.Select(x => x.Id).FirstAsync(), CreatedAt = DateTimeOffset.UtcNow,
            Targets = [new MissionTarget { SensorNodeId = node.Id, SequenceNo = 1 }] };
        db.Missions.Add(mission); await db.SaveChangesAsync();
        await using var editScope = factory.Services.CreateAsyncScope();
        var service = ApiFactory.HumanService<SensorService>(editScope.ServiceProvider, await db.Users.Where(x => x.Email == "admin@example.com").Select(x => x.Id).SingleAsync());
        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateNodeAsync(node.Id,
            new(otherZone.Id, node.DeviceCode, node.Name, node.Status, null, null, null, null, null, null), default));
        Assert.Equal(node.ZoneId, (await db.SensorNodes.AsNoTracking().SingleAsync(x => x.Id == node.Id)).ZoneId);
    }

    private static SensorNode Node() => new() { DeviceCode = Guid.NewGuid().ToString("N"), Name = "Protected sensor", Status = "ONLINE",
        CreatedAt = DateTimeOffset.UtcNow, Zone = new Zone { Name = "Original zone", CreatedAt = DateTimeOffset.UtcNow,
            Farm = new Farm { Name = "Original farm", CreatedAt = DateTimeOffset.UtcNow } } };
}
