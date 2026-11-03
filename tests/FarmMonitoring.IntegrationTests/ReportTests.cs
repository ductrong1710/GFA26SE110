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
public class ReportTests(ApiFactory factory)
{
    private async Task<HttpClient> Login(string email)
    {
        var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" });
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("accessToken").GetString());
        return client;
    }
    [Fact]
    public async Task Dashboard_operational_summary_uses_current_farm_and_owner_scope()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerId = await db.Users.Where(x => x.Email == "owner@example.com").Select(x => x.Id).SingleAsync();
        var adminId = await db.Users.Where(x => x.Email == "admin@example.com").Select(x => x.Id).SingleAsync();
        var farmIds = new List<int>();
        foreach (var name in new[] { "Visible dashboard farm", "Private dashboard farm" })
        {
            var farm = new Farm { Name = name, CreatedAt = DateTimeOffset.UtcNow };
            var uav = new Uav { Code = Guid.NewGuid().ToString("N"), Name = name, CreatedAt = DateTimeOffset.UtcNow };
            var gateway = new Gateway { Code = Guid.NewGuid().ToString("N"), Name = name, GatewayType = "ESP32", Uav = uav, CreatedAt = DateTimeOffset.UtcNow };
            db.Farms.Add(farm); db.Gateways.Add(gateway); await db.SaveChangesAsync();
            await factory.SeedRentalAsync(farm.Id, uav.Id, gateway.Id);
            var rental = await db.RentalContracts.Include(x => x.InstallationJob).Include(x => x.Devices).SingleAsync(x => x.FarmId == farm.Id);
            rental.EndAt = DateTimeOffset.UtcNow.AddDays(1);
            rental.InstallationJob.Status = InstallationJobStatus.ASSIGNED;
            db.ServiceJobs.Add(new ServiceJob { RentalDeviceAssignmentId = rental.Devices.First().Id, TechnicianId = adminId,
                Type = ServiceJobType.REPAIR, Status = ServiceJobStatus.ASSIGNED, ScheduledAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow });
            db.CropSeasons.Add(new CropSeason { FarmId = farm.Id, OwnerId = ownerId, Name = name, Crop = "Rice", CreatedAt = DateTimeOffset.UtcNow,
                Activities = [new CultivationActivity { UserId = ownerId, Date = new DateOnly(2026, 11, 1), Type = CultivationActivityType.WATERING, Description = name, CreatedAt = DateTimeOffset.UtcNow }] });
            await db.SaveChangesAsync(); farmIds.Add(farm.Id);
        }
        await factory.AssignFarmAsync(farmIds[0], "owner@example.com");
        db.CropSeasons.Add(new CropSeason { FarmId = farmIds[0], OwnerId = adminId, Name = "Other owner's season", Crop = "Corn", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        using var owner = await Login("owner@example.com");
        var result = (await owner.GetFromJsonAsync<JsonElement>($"/api/dashboard/overview?farmId={farmIds[0]}")).GetProperty("data").GetProperty("operations");
        Assert.Equal(1, result.GetProperty("openInstallations").GetInt32());
        Assert.Equal(1, result.GetProperty("openRepairs").GetInt32());
        Assert.Equal(0, result.GetProperty("openMaintenance").GetInt32());
        Assert.Equal(1, result.GetProperty("activeRentals").GetInt32());
        Assert.Equal(1, result.GetProperty("expiringRentals").GetInt32());
        Assert.Equal(1, result.GetProperty("activeSeasons").GetInt32());
        Assert.Equal("Visible dashboard farm", Assert.Single(result.GetProperty("recentCultivation").EnumerateArray()).GetProperty("description").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/dashboard/overview?farmId={farmIds[1]}")).StatusCode);
        using var admin = await Login("admin@example.com");
        var adminResult = (await admin.GetFromJsonAsync<JsonElement>($"/api/dashboard/overview?farmId={farmIds[0]}")).GetProperty("data").GetProperty("operations");
        Assert.Equal(2, adminResult.GetProperty("activeSeasons").GetInt32());
        await db.UserFarms.Where(x => x.FarmId == farmIds[0] && x.UserId == ownerId).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/dashboard/overview?farmId={farmIds[0]}")).StatusCode);
    }

    [Fact]
    public async Task Reports_aggregate_persisted_data_and_enforce_report_permissions()
    {
        int farmId, farmCount, missionId;
        var now = DateTimeOffset.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var farm = new Farm { Name = "Report farm", CreatedAt = now };
            var node = new SensorNode { DeviceCode = Guid.NewGuid().ToString("N"), Name = "Report sensor", Status = "ONLINE", CreatedAt = now,
                Zone = new Zone { Name = "Report zone", Farm = farm, CreatedAt = now } };
            var channel = new SensorChannel { ChannelCode = "temperature", SensorNode = node, CreatedAt = now,
                SensorType = new SensorType { Code = Guid.NewGuid().ToString("N"), Name = "Temperature", Unit = "C" } };
            var mission = new Mission { Name = "Report mission", Farm = farm, Status = MissionStatus.COMPLETED, CreatedAt = now,
                StartedAt = now.AddMinutes(-5), CompletedAt = now, CreatedByUserId = await db.Users.Where(x => x.Email == "owner@example.com").Select(x => x.Id).SingleAsync(),
                Targets = [new MissionTarget { SensorNode = node, Status = MissionTargetStatus.COLLECTED }] };
            db.Missions.Add(mission);
            foreach (var value in new[] { 10m, 14m }) db.SensorReadings.Add(new SensorReading { SensorChannel = channel, Mission = mission, SourceRecordKey = Guid.NewGuid().ToString("N"),
                Value = value, MeasuredAt = now.AddMinutes(-1), CollectedAt = now, ReceivedAt = now });
            db.SensorReadings.Add(new SensorReading { SensorChannel = channel, Mission = mission, SourceRecordKey = Guid.NewGuid().ToString("N"),
                Value = 100m, IsValid = false, ValidationError = "Test invalid measurement", MeasuredAt = now.AddMinutes(-1), CollectedAt = now, ReceivedAt = now });
            db.Alerts.AddRange(new Alert { AlertType = AlertType.MISSION_ERROR, Severity = AlertSeverity.CRITICAL, Mission = mission, Message = "Mission issue", OpenedAt = now },
                new Alert { AlertType = AlertType.SENSOR_THRESHOLD, Severity = AlertSeverity.WARNING, SensorNode = node, SensorChannel = channel, Message = "Sensor issue", OpenedAt = now });
            await db.SaveChangesAsync();
            await factory.AssignFarmAsync(farm.Id, "owner@example.com", "engineer@example.com");
            farmId = farm.Id; missionId = mission.Id; farmCount = await db.Farms.CountAsync();
        }
        using var admin = await Login("admin@example.com");
        var overview = await admin.GetAsync("/api/dashboard/overview");
        Assert.Equal(HttpStatusCode.OK, overview.StatusCode);
        Assert.Equal(farmCount, (await overview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("farms").GetInt32());
        var sensors = (await admin.GetFromJsonAsync<JsonElement>($"/api/reports/sensor-data?farmId={farmId}")).GetProperty("data");
        Assert.Single(sensors.EnumerateArray());
        Assert.Equal(2, sensors[0].GetProperty("readingCount").GetInt64());
        Assert.Equal(12m, sensors[0].GetProperty("average").GetDecimal());
        var missions = (await admin.GetFromJsonAsync<JsonElement>($"/api/reports/missions?farmId={farmId}")).GetProperty("data");
        Assert.Single(missions.EnumerateArray());
        Assert.Equal(missionId, missions[0].GetProperty("id").GetInt32());
        Assert.Equal(1, missions[0].GetProperty("successfulTargets").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/reports/devices?pageSize=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/reports/alerts")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/reports/sensor-data?from=2020-01-01T00:00:00Z&to=2026-01-01T00:00:00Z")).StatusCode);
        using var op = await Login("owner@example.com");
        Assert.Equal(HttpStatusCode.OK, (await op.GetAsync("/api/reports/sensor-data")).StatusCode);
        foreach (var path in new[] { "/api/reports/missions", "/api/reports/devices", "/api/reports/alerts", "/api/dashboard/overview" })
            Assert.Equal(HttpStatusCode.OK, (await op.GetAsync(path)).StatusCode);
        var adminAlerts = (await admin.GetFromJsonAsync<JsonElement>($"/api/reports/alerts?farmId={farmId}")).GetProperty("data");
        Assert.Equal(2, adminAlerts.GetArrayLength());
        var ownerAlerts = (await op.GetFromJsonAsync<JsonElement>($"/api/reports/alerts?farmId={farmId}")).GetProperty("data");
        Assert.Equal(2, ownerAlerts.GetArrayLength());
        using var engineer = await Login("engineer@example.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await engineer.GetAsync($"/api/reports/alerts?farmId={farmId}")).StatusCode);
    }
}
