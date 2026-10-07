using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class SyncTests(ApiFactory factory)
{
    [Fact]
    public async Task Sync_retries_partial_results_and_collection_attempts_preserve_data()
    {
        int missionId, gatewayId, nodeId, channelId, zoneId, typeId;
        string nodeCode;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var farm = new Farm { Name = "Sync farm", CreatedAt = DateTimeOffset.UtcNow };
            var node = new SensorNode { DeviceCode = Guid.NewGuid().ToString("N"), Name = "Sync sensor", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow,
                Zone = new Zone { Name = "Sync zone", Farm = farm, CreatedAt = DateTimeOffset.UtcNow } };
            var channel = new SensorChannel { SensorNode = node, ChannelCode = "temperature", CreatedAt = DateTimeOffset.UtcNow,
                SensorType = new SensorType { Code = Guid.NewGuid().ToString("N"), Name = "Temperature", Unit = "C" } };
            var gateway = new Gateway { Code = "sync-gateway", Name = "Sync gateway", GatewayType = "ESP32", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow };
            var mission = new Mission { Name = "Sync mission", Farm = farm, Gateway = gateway, CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = await db.Users.Where(x => x.Email == "owner@example.com").Select(x => x.Id).SingleAsync(), Status = MissionStatus.RUNNING,
                Targets = [new MissionTarget { SensorNode = node, SequenceNo = 1 }] };
            db.SensorChannels.Add(channel); db.Missions.Add(mission);
            await db.SaveChangesAsync();
            await factory.AssignFarmAsync(farm.Id, "owner@example.com");
            missionId = mission.Id; gatewayId = gateway.Id; nodeId = node.Id; channelId = channel.Id; nodeCode = node.DeviceCode;
            zoneId = node.ZoneId; typeId = channel.SensorTypeId;
        }
        using var device = factory.CreateApiClient();
        device.DefaultRequestHeaders.Add("X-Gateway-Code", "sync-gateway");
        device.DefaultRequestHeaders.Add("X-Api-Key", factory.DeviceKey);
        var measuredAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        object Record(string key, string code, object value) => new { sensorNodeCode = code, channelCode = "temperature", sourceRecordKey = key, value, measuredAt, collectedAt = measuredAt.AddSeconds(1) };
        var batch = new { batchKey = "partial-1", missionId, records = new[] { Record("r1", nodeCode, 31.5m), Record("r2", "unknown", 22m), Record("r3", nodeCode, "bad-value") },
            collectionResults = new[] { new { sensorNodeId = nodeId, status = "TIMEOUT", attemptNo = 1, recordsReceived = 0 } } };
        var path = $"/api/device/gateways/{gatewayId}/sync";
        var response = await device.PostAsJsonAsync(path, batch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var first = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(1, first.GetProperty("accepted").GetInt32());
        Assert.Equal(2, first.GetProperty("rejected").GetInt32());
        var retry = await device.PostAsJsonAsync(path, batch);
        retry.EnsureSuccessStatusCode();
        Assert.Equal(first.ToString(), (await retry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await device.PostAsJsonAsync(path, new { batchKey = "partial-1", missionId, records = new[] { Record("r1", nodeCode, 99m) } })).StatusCode);
        var next = new { batchKey = "success-2", missionId, records = new[] { Record("r1", nodeCode, 31.5m), Record("r4", nodeCode, 30m) },
            collectionResults = new[] { new { sensorNodeId = nodeId, status = "SUCCESS", attemptNo = 2, recordsReceived = 1 } } };
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => device.PostAsJsonAsync(path, next)));
        foreach (var outcome in outcomes)
        {
            outcome.EnsureSuccessStatusCode();
            var data = (await outcome.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
            Assert.Equal(1, data.GetProperty("accepted").GetInt32());
            Assert.Equal(1, data.GetProperty("duplicates").GetInt32());
        }
        using var human = factory.CreateApiClient();
        var login = await human.PostAsJsonAsync("/api/auth/login", new { email = "owner@example.com", password = "Test-password-123!" });
        human.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("accessToken").GetString());
        var history = await human.GetFromJsonAsync<JsonElement>($"/api/sensor-channels/{channelId}/history");
        Assert.Equal(2, history.GetProperty("pagination").GetProperty("totalItems").GetInt32());
        var latest = (await human.GetFromJsonAsync<JsonElement>($"/api/sensor-nodes/{nodeId}/latest")).GetProperty("data");
        Assert.Equal(30m, latest[0].GetProperty("reading").GetProperty("value").GetDecimal());
        var comparison = (await human.GetFromJsonAsync<JsonElement>($"/api/zones/compare?zoneIds={zoneId}&sensorTypeId={typeId}")).GetProperty("data")[0];
        Assert.Equal(2, comparison.GetProperty("readingCount").GetInt64());
        Assert.Equal(30.75m, comparison.GetProperty("average").GetDecimal());
        var attempts = await human.GetFromJsonAsync<JsonElement>($"/api/missions/{missionId}/collection-attempts");
        Assert.Equal(2, attempts.GetProperty("pagination").GetProperty("totalItems").GetInt32());
        var result = (await human.GetFromJsonAsync<JsonElement>($"/api/missions/{missionId}/results")).GetProperty("data");
        Assert.Equal(1, result.GetProperty("successfulTargets").GetInt32());
        (await human.PostAsJsonAsync($"/api/missions/{missionId}/complete", new { })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await human.PostAsJsonAsync(path, batch)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await device.PostAsJsonAsync($"/api/device/gateways/{gatewayId + 100}/sync", batch)).StatusCode);
        var edgeCases = await device.PostAsJsonAsync(path, new { batchKey = "edge-cases", missionId, records = new object?[]
        {
            null, new { sensorNodeCode = nodeCode, channelCode = "temperature", sourceRecordKey = "missing-value", measuredAt, collectedAt = measuredAt },
            Record("r5", nodeCode, 20m), Record("r5", nodeCode, 20m), Record("r1", nodeCode, 999m)
        } });
        edgeCases.EnsureSuccessStatusCode();
        var edge = (await edgeCases.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(1, edge.GetProperty("accepted").GetInt32());
        Assert.Equal(1, edge.GetProperty("duplicates").GetInt32());
        Assert.Equal(3, edge.GetProperty("rejected").GetInt32());

        int secondGateway;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gateway = new Gateway { Code = "sync-gateway-2", Name = "Second sync gateway", GatewayType = "ESP32", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow };
            db.Gateways.Add(gateway);
            await db.SaveChangesAsync();
            secondGateway = gateway.Id;
        }
        using var secondDevice = factory.CreateApiClient();
        secondDevice.DefaultRequestHeaders.Add("X-Gateway-Code", "sync-gateway-2");
        secondDevice.DefaultRequestHeaders.Add("X-Api-Key", factory.DeviceKey);
        var concurrent = new { batchKey = "cross-gateway", records = new[] { Record("cross-key", nodeCode, 25m) } };
        var crossResponses = await Task.WhenAll(device.PostAsJsonAsync(path, concurrent), secondDevice.PostAsJsonAsync($"/api/device/gateways/{secondGateway}/sync", concurrent));
        var accepted = 0; var duplicates = 0;
        foreach (var crossResponse in crossResponses)
        {
            crossResponse.EnsureSuccessStatusCode();
            var data = (await crossResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
            accepted += data.GetProperty("accepted").GetInt32(); duplicates += data.GetProperty("duplicates").GetInt32();
        }
        Assert.Equal(1, accepted); Assert.Equal(1, duplicates);
        var malformedText = await device.PostAsJsonAsync(path, new { batchKey = "malformed-text", missionId, records = new object[]
        {
            Record("safe", nodeCode, 21m), Record("bad\0key", nodeCode, 20m), Record("bad-node", "bad\0node", 20m),
            new { sensorNodeCode = nodeCode, channelCode = "temperature", sourceRecordKey = "bad-quality", value = 20m, measuredAt, collectedAt = measuredAt, qualityStatus = "bad\0quality" }
        }, collectionResults = new[] { new { sensorNodeId = nodeId, attemptNo = 3, status = "FAILED", recordsReceived = 0, errorMessage = "bad\0message" } } });
        malformedText.EnsureSuccessStatusCode();
        var malformed = (await malformedText.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(1, malformed.GetProperty("accepted").GetInt32());
        Assert.Equal(3, malformed.GetProperty("rejected").GetInt32());
        Assert.Equal("REJECTED", malformed.GetProperty("collectionResults")[0].GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await human.GetAsync($"/api/zones/compare?zoneIds={zoneId}&sensorTypeId={typeId}&to=0001-01-01T00:00:00Z")).StatusCode);
    }
}
