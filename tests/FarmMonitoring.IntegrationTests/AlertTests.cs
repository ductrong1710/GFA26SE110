using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class AlertTests(ApiFactory factory)
{
    [Fact]
    public async Task Monitoring_detects_timeout_and_low_battery_once_and_ignores_inactive_sensors()
    {
        int activeNode, inactiveNode, gatewayId, uavId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var zone = new Zone { Name = "Monitoring zone", CreatedAt = DateTimeOffset.UtcNow,
                Farm = new Farm { Name = "Monitoring farm", CreatedAt = DateTimeOffset.UtcNow } };
            SensorThreshold Threshold(bool active) => new()
            {
                DataTimeoutMinutes = 1, LowBatteryPercent = 20, CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                SensorChannel = new SensorChannel { ChannelCode = "value", CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                    SensorType = new SensorType { Code = Guid.NewGuid().ToString("N"), Name = "Monitoring value" },
                    SensorNode = new SensorNode { DeviceCode = Guid.NewGuid().ToString("N"), Name = "Monitor sensor", Status = "OFFLINE", IsActive = active,
                        BatteryPercent = 5, CreatedAt = DateTimeOffset.UtcNow.AddHours(-1), Zone = zone } }
            };
            var active = Threshold(true); var inactive = Threshold(false);
            db.SensorThresholds.AddRange(active, inactive);
            var gateway = new Gateway { Code = Guid.NewGuid().ToString("N"), Name = "Monitoring gateway", GatewayType = "ESP32", Status = "ERROR",
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-1), LastSeenAt = DateTimeOffset.UtcNow.AddHours(-1) };
            var uav = new Uav { Code = Guid.NewGuid().ToString("N"), Name = "Monitoring UAV", Status = "ONLINE", BatteryPercent = 5, CreatedAt = DateTimeOffset.UtcNow };
            db.Gateways.Add(gateway); db.Uavs.Add(uav);
            await db.SaveChangesAsync();
            activeNode = active.SensorChannel.SensorNodeId; inactiveNode = inactive.SensorChannel.SensorNodeId;
            gatewayId = gateway.Id; uavId = uav.Id;
        }
        await using var monitored = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Monitoring:Enabled"] = "true", ["Monitoring:IntervalSeconds"] = "1" })));
        using var client = monitored.CreateClient();
        var count = 0;
        for (var attempt = 0; attempt < 30 && count < 2; attempt++)
        {
            await Task.Delay(200);
            await using var scope = factory.Services.CreateAsyncScope();
            count = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Alerts.CountAsync(x => x.SensorNodeId == activeNode);
        }
        Assert.Equal(2, count);
        await Task.Delay(1200);
        await using var finalScope = factory.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await finalDb.Alerts.CountAsync(x => x.SensorNodeId == activeNode));
        Assert.Equal(0, await finalDb.Alerts.CountAsync(x => x.SensorNodeId == inactiveNode));
        Assert.Equal(2, await finalDb.Alerts.CountAsync(x => x.GatewayId == gatewayId));
        Assert.Equal(1, await finalDb.Alerts.CountAsync(x => x.UavId == uavId));
    }

    private async Task<HttpClient> Login(string email)
    {
        var client = factory.CreateApiClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" });
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("accessToken").GetString());
        return client;
    }
    [Fact]
    public async Task Threshold_alerts_deduplicate_and_preserve_lifecycle_and_private_notifications()
    {
        int gatewayId, nodeId; string nodeCode;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var channel = new SensorChannel { ChannelCode = "temperature", CreatedAt = DateTimeOffset.UtcNow,
                SensorType = new SensorType { Code = Guid.NewGuid().ToString("N"), Name = "Temperature" },
                SensorNode = new SensorNode { DeviceCode = Guid.NewGuid().ToString("N"), Name = "Alert sensor", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow,
                    Zone = new Zone { Name = "Alert zone", CreatedAt = DateTimeOffset.UtcNow, Farm = new Farm { Name = "Alert farm", CreatedAt = DateTimeOffset.UtcNow } } } };
            db.SensorThresholds.Add(new SensorThreshold { SensorChannel = channel, MaxValue = 30m, CreatedAt = DateTimeOffset.UtcNow });
            var gateway = new Gateway { Code = "alerts-gateway", Name = "Alert gateway", Status = "ONLINE", GatewayType = "ESP32", CreatedAt = DateTimeOffset.UtcNow };
            db.Gateways.Add(gateway);
            await db.SaveChangesAsync();
            gatewayId = gateway.Id; nodeId = channel.SensorNodeId; nodeCode = channel.SensorNode.DeviceCode;
        }
        using var device = factory.CreateApiClient();
        device.DefaultRequestHeaders.Add("X-Gateway-Code", "alerts-gateway");
        device.DefaultRequestHeaders.Add("X-Api-Key", factory.DeviceKey);
        object Batch(string key) => new { batchKey = key, records = new[] { new { sensorNodeCode = nodeCode, channelCode = "temperature", sourceRecordKey = key, value = 40m, measuredAt = DateTimeOffset.UtcNow.AddMinutes(-1), collectedAt = DateTimeOffset.UtcNow } } };
        var syncs = await Task.WhenAll(new[] { "alert-one", "alert-two" }.Select(key => device.PostAsJsonAsync($"/api/device/gateways/{gatewayId}/sync", Batch(key))));
        foreach (var response in syncs) response.EnsureSuccessStatusCode();
        using var admin = await Login("admin@example.com");
        var listResponse = await admin.GetAsync($"/api/alerts?sensorNodeId={nodeId}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = (await listResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Single(list.EnumerateArray());
        var id = list[0].GetProperty("id").GetInt32();
        Assert.Equal("OPEN", list[0].GetProperty("status").GetString());
        var ack = await admin.PostAsJsonAsync($"/api/alerts/{id}/acknowledge", new { note = "Investigating" });
        ack.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/alerts/{id}/acknowledge", new { note = "Again" })).StatusCode);
        (await admin.PostAsJsonAsync($"/api/alerts/{id}/notes", new { note = "Checked sensor" })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/alerts/{id}/close", new { note = "Resolved" })).EnsureSuccessStatusCode();
        var detail = (await admin.GetFromJsonAsync<JsonElement>($"/api/alerts/{id}")).GetProperty("data");
        Assert.Equal("CLOSED", detail.GetProperty("status").GetString());
        Assert.Equal(4, detail.GetProperty("history").GetArrayLength());
        var notifications = (await admin.GetFromJsonAsync<JsonElement>($"/api/notifications?alertId={id}")).GetProperty("data");
        Assert.Single(notifications.EnumerateArray());
        var notificationId = notifications[0].GetProperty("id").GetInt32();
        using var op = await Login("owner@example.com");
        Assert.Equal(HttpStatusCode.NotFound, (await op.PatchAsJsonAsync($"/api/notifications/{notificationId}/read", new { })).StatusCode);
        (await admin.PatchAsJsonAsync($"/api/notifications/{notificationId}/read", new { })).EnsureSuccessStatusCode();
        (await device.PostAsJsonAsync($"/api/device/gateways/{gatewayId}/sync", Batch("alert-three"))).EnsureSuccessStatusCode();
        Assert.Equal(2, (await admin.GetFromJsonAsync<JsonElement>($"/api/alerts?sensorNodeId={nodeId}")).GetProperty("pagination").GetProperty("totalItems").GetInt32());
    }
}
