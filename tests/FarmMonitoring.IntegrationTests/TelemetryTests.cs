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
public class TelemetryTests(ApiFactory factory)
{
    [Fact]
    public async Task Device_ingestion_checks_identity_and_assignment_and_preserves_ordered_history()
    {
        int missionId, gatewayId, uavId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gateway = new Gateway { Code = "integration-gateway", Name = "Test gateway", GatewayType = "ESP32", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow,
                Uav = new Uav { Code = Guid.NewGuid().ToString("N"), Name = "Test UAV", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow } };
            var mission = new Mission { Name = "Telemetry mission", Gateway = gateway, Uav = gateway.Uav,
                Farm = new Farm { Name = "Telemetry farm", CreatedAt = DateTimeOffset.UtcNow }, CreatedByUserId = await db.Users.Where(x => x.Email == "owner@example.com").Select(x => x.Id).SingleAsync(),
                Status = MissionStatus.RUNNING, CreatedAt = DateTimeOffset.UtcNow, StartedAt = DateTimeOffset.UtcNow.AddHours(-1) };
            db.Missions.Add(mission);
            await db.SaveChangesAsync();
            await factory.AssignFarmAsync(mission.FarmId, "owner@example.com");
            missionId = mission.Id; gatewayId = gateway.Id; uavId = gateway.Uav!.Id;
        }
        using var human = factory.CreateApiClient();
        var login = await human.PostAsJsonAsync("/api/auth/login", new { email = "owner@example.com", password = "Test-password-123!" });
        human.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("accessToken").GetString());
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        object Payload(DateTimeOffset time, int? suppliedGateway = null, decimal battery = 70) => new { missionId, uavId, gatewayId = suppliedGateway ?? gatewayId, recordedAt = time, latitude = 10m, longitude = 106m, batteryPercent = battery, flightStatus = "AUTO" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await human.PostAsJsonAsync("/api/device/telemetry", Payload(now))).StatusCode);
        using var device = factory.CreateApiClient();
        device.DefaultRequestHeaders.Add("X-Gateway-Code", "integration-gateway");
        device.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.PostAsJsonAsync("/api/device/telemetry", Payload(now))).StatusCode);
        device.DefaultRequestHeaders.Remove("X-Api-Key");
        device.DefaultRequestHeaders.Add("X-Api-Key", factory.DeviceKey);
        Assert.Equal(HttpStatusCode.Forbidden, (await device.PostAsJsonAsync("/api/device/telemetry", Payload(now, gatewayId + 100))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await device.PostAsJsonAsync("/api/device/telemetry", Payload(now, battery: 101))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await device.PostAsJsonAsync("/api/device/telemetry", Payload(now))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await device.PostAsJsonAsync("/api/device/telemetry", Payload(now.AddMinutes(-1)))).StatusCode);
        var latest = (await human.GetFromJsonAsync<JsonElement>($"/api/missions/{missionId}/telemetry/latest")).GetProperty("data");
        Assert.Equal(now.ToUnixTimeMilliseconds(), latest.GetProperty("recordedAt").GetDateTimeOffset().ToUnixTimeMilliseconds());
        var history = await human.GetFromJsonAsync<JsonElement>($"/api/missions/{missionId}/telemetry?pageSize=1");
        Assert.Single(history.GetProperty("data").EnumerateArray());
        Assert.Equal(2, history.GetProperty("pagination").GetProperty("totalItems").GetInt32());
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync($"/api/missions/{missionId}/telemetry")).StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gateway = await db.Gateways.FindAsync(gatewayId);
            gateway!.IsActive = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.PostAsJsonAsync("/api/device/telemetry", Payload(now))).StatusCode);
    }
}
