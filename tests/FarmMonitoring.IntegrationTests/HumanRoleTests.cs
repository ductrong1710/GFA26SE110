using System.IdentityModel.Tokens.Jwt;
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
public class HumanRoleTests(ApiFactory factory)
{
    private async Task<(HttpClient Client, JsonElement Data)> Login(string email)
    {
        var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" });
        response.EnsureSuccessStatusCode();
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        return (client, data);
    }

    [Theory]
    [InlineData("FarmAdministrator", true, true, true, true, true)]
    [InlineData("FarmOwner", false, true, true, false, true)]
    [InlineData("FarmEngineer", false, false, false, true, false)]
    public async Task Human_auth_and_policy_matrix_preserve_device_boundary(string role, bool users, bool farms, bool devices, bool thresholds, bool missions)
    {
        var (admin, _) = await Login("admin@example.com");
        using var adminClient = admin;
        var email = $"matrix-{Guid.NewGuid():N}@example.com";
        var created = await admin.PostAsJsonAsync("/api/users", new { email, fullName = role, password = "Test-password-123!", roles = new[] { role } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var (client, login) = await Login(email);
        using var human = client;
        Assert.Equal(role, Assert.Single(login.GetProperty("user").GetProperty("roles").EnumerateArray()).GetString());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(login.GetProperty("accessToken").GetString());
        Assert.Equal(role, Assert.Single(jwt.Claims, x => x.Type == "role").Value);
        var me = (await human.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("data");
        Assert.Equal(role, Assert.Single(me.GetProperty("roles").EnumerateArray()).GetString());

        int farmId, channelId, gatewayId, missionId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var farm = new Farm { Name = "Policy farm", CreatedAt = DateTimeOffset.UtcNow };
            var channel = new SensorChannel { ChannelCode = "temperature", CreatedAt = DateTimeOffset.UtcNow,
                SensorType = new SensorType { Code = Guid.NewGuid().ToString("N"), Name = "Temperature" },
                SensorNode = new SensorNode { DeviceCode = Guid.NewGuid().ToString("N"), Name = "Policy node", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow,
                    Zone = new Zone { Name = "Policy zone", Farm = farm, CreatedAt = DateTimeOffset.UtcNow } } };
            var gateway = await db.Gateways.SingleOrDefaultAsync(x => x.Code == "role-matrix-gateway")
                ?? new Gateway { Code = "role-matrix-gateway", Name = "Policy gateway", GatewayType = "ESP32", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow };
            var mission = new Mission { Name = "Device boundary", Farm = farm, Gateway = gateway, Status = MissionStatus.RUNNING,
                CreatedByUserId = login.GetProperty("user").GetProperty("id").GetInt32(), CreatedAt = DateTimeOffset.UtcNow };
            db.SensorChannels.Add(channel); db.Missions.Add(mission);
            await db.SaveChangesAsync();
            if (role != "FarmAdministrator") await factory.AssignFarmAsync(farm.Id, email);
            farmId = farm.Id; channelId = channel.Id; gatewayId = gateway.Id; missionId = mission.Id;
        }
        Assert.Equal(users ? HttpStatusCode.OK : HttpStatusCode.Forbidden, (await human.GetAsync("/api/users")).StatusCode);
        Assert.Equal(farms ? HttpStatusCode.Created : HttpStatusCode.Forbidden, (await human.PostAsJsonAsync("/api/farms", new { name = "Role-created farm" })).StatusCode);
        Assert.Equal(devices ? HttpStatusCode.Created : HttpStatusCode.Forbidden, (await human.PostAsJsonAsync("/api/uavs", new { code = Guid.NewGuid().ToString("N"), name = "Policy UAV", status = "ONLINE" })).StatusCode);
        Assert.Equal(thresholds ? HttpStatusCode.OK : HttpStatusCode.Forbidden, (await human.PutAsJsonAsync($"/api/sensor-channels/{channelId}/threshold", new { minValue = 0, maxValue = 50, isEnabled = true })).StatusCode);
        Assert.Equal(missions ? HttpStatusCode.Created : HttpStatusCode.Forbidden, (await human.PostAsJsonAsync("/api/missions", new { name = "Policy mission", farmId, sensorNodeIds = Array.Empty<int>(), waypoints = Array.Empty<object>() })).StatusCode);
        foreach (var path in new[] { "/api/farms", "/api/sensor-readings", "/api/missions", "/api/alerts", "/api/notifications", "/api/dashboard/overview",
            "/api/reports/sensor-data", "/api/reports/devices", "/api/reports/missions", "/api/reports/alerts" })
            Assert.Equal(HttpStatusCode.OK, (await human.GetAsync(path)).StatusCode);

        var telemetry = new { missionId, gatewayId, recordedAt = DateTimeOffset.UtcNow, batteryPercent = 80 };
        var sync = new { batchKey = Guid.NewGuid().ToString("N"), records = Array.Empty<object>() };
        Assert.Equal(HttpStatusCode.Unauthorized, (await human.PostAsJsonAsync("/api/device/telemetry", telemetry)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await human.PostAsJsonAsync($"/api/device/gateways/{gatewayId}/sync", sync)).StatusCode);
        using var device = factory.CreateApiClient();
        device.DefaultRequestHeaders.Add("X-Gateway-Code", "role-matrix-gateway");
        device.DefaultRequestHeaders.Add("X-Api-Key", factory.DeviceKey);
        (await device.PostAsJsonAsync("/api/device/telemetry", telemetry)).EnsureSuccessStatusCode();
        (await device.PostAsJsonAsync($"/api/device/gateways/{gatewayId}/sync", sync)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync("/api/farms")).StatusCode);
    }

    [Theory]
    [InlineData("UavDeviceOperator")]
    [InlineData("UnknownRole")]
    public async Task Unsupported_roles_are_rejected_by_user_endpoints(string role)
    {
        var (client, data) = await Login("admin@example.com");
        using var admin = client;
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/users", new { email = "invalid-role@example.com", fullName = "Invalid", password = "Test-password-123!", roles = new[] { role } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/users/{data.GetProperty("user").GetProperty("id").GetInt32()}/roles", new { roles = new[] { role } })).StatusCode);
    }

    [Fact]
    public async Task Removing_last_supported_role_blocks_login_refresh_me_and_old_privileges()
    {
        var (admin, _) = await Login("admin@example.com");
        using var administrator = admin;
        var email = $"removed-{Guid.NewGuid():N}@example.com";
        var created = await admin.PostAsJsonAsync("/api/users", new { email, fullName = "Removed role", password = "Test-password-123!", roles = new[] { "FarmOwner" } });
        created.EnsureSuccessStatusCode();
        var (client, login) = await Login(email);
        using var user = client;
        var id = login.GetProperty("user").GetProperty("id").GetInt32();
        (await admin.PutAsJsonAsync($"/api/users/{id}/roles", new { roles = Array.Empty<string>() })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = login.GetProperty("refreshToken").GetString() })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/farms")).StatusCode);
    }
}
