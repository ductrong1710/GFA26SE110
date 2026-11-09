using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class EquipmentTests(ApiFactory factory)
{
    private async Task<HttpClient> Login(string email)
    {
        var client = factory.CreateApiClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" });
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("accessToken").GetString());
        return client;
    }
    private static async Task<int> Id(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Administrator_manages_equipment_and_assigns_or_detaches_gateway()
    {
        using var op = await Login("admin@example.com");
        var code = $"UAV-{Guid.NewGuid():N}";
        var uavRequest = new { code, name = "Demo UAV", model = "Prototype", status = "OFFLINE", batteryPercent = 75 };
        var uav = await Id(await op.PostAsJsonAsync("/api/uavs", uavRequest));
        Assert.Equal(HttpStatusCode.Conflict, (await op.PostAsJsonAsync("/api/uavs", uavRequest)).StatusCode);
        var gatewayCode = $"GW-{Guid.NewGuid():N}";
        var gatewayRequest = new { code = gatewayCode, name = "Demo Gateway", gatewayType = "ESP32", status = "OFFLINE", firmwareVersion = "1.0", batteryPercent = 60 };
        var gateway = await Id(await op.PostAsJsonAsync("/api/gateways", gatewayRequest));
        Assert.Equal(HttpStatusCode.Conflict, (await op.PostAsJsonAsync("/api/gateways", gatewayRequest)).StatusCode);
        (await op.PostAsJsonAsync($"/api/gateways/{gateway}/assign-uav", new { uavId = uav })).EnsureSuccessStatusCode();
        Assert.Equal(uav, (await op.GetFromJsonAsync<JsonElement>($"/api/gateways/{gateway}")).GetProperty("data").GetProperty("uavId").GetInt32());
        (await op.PostAsJsonAsync($"/api/gateways/{gateway}/assign-uav", new { uavId = (int?)null })).EnsureSuccessStatusCode();
        Assert.Equal(JsonValueKind.Null, (await op.GetFromJsonAsync<JsonElement>($"/api/gateways/{gateway}")).GetProperty("data").GetProperty("uavId").ValueKind);
        (await op.PutAsJsonAsync($"/api/uavs/{uav}", new { code, name = "Updated UAV", status = "ONLINE", batteryPercent = 90 })).EnsureSuccessStatusCode();
        (await op.PutAsJsonAsync($"/api/gateways/{gateway}", new { code = gatewayCode, name = "Updated Gateway", gatewayType = "Raspberry Pi", status = "ONLINE" })).EnsureSuccessStatusCode();
        (await op.PatchAsJsonAsync($"/api/uavs/{uav}/status", new { status = "OFFLINE", isActive = false })).EnsureSuccessStatusCode();
        (await op.PatchAsJsonAsync($"/api/gateways/{gateway}/status", new { status = "OFFLINE", isActive = false })).EnsureSuccessStatusCode();
        Assert.False((await op.GetFromJsonAsync<JsonElement>($"/api/uavs/{uav}")).GetProperty("data").GetProperty("isActive").GetBoolean());
        Assert.False((await op.GetFromJsonAsync<JsonElement>($"/api/gateways/{gateway}")).GetProperty("data").GetProperty("isActive").GetBoolean());
        Assert.Single((await op.GetFromJsonAsync<JsonElement>($"/api/uavs?search={code}")).GetProperty("data").EnumerateArray());
        Assert.Single((await op.GetFromJsonAsync<JsonElement>($"/api/gateways?search={gatewayCode}")).GetProperty("data").EnumerateArray());
    }

    [Fact]
    public async Task Owner_catalog_requires_current_rental_assignment_and_membership()
    {
        using var admin = await Login("admin@example.com");
        using var owner = await Login("owner@example.com");
        var code = Guid.NewGuid().ToString("N");
        var uav = await Id(await admin.PostAsJsonAsync("/api/uavs", new { code, name = "Rental UAV", status = "ONLINE" }));
        var gateway = await Id(await admin.PostAsJsonAsync("/api/gateways", new { code, name = "Rental gateway", gatewayType = "ESP32", status = "ONLINE" }));
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/uavs/{uav}")).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<JsonElement>($"/api/uavs?search={code}")).GetProperty("data").EnumerateArray());
        var farm = await Id(await admin.PostAsJsonAsync("/api/farms", new { name = "Rental catalog farm" }));
        await factory.AssignFarmAsync(farm, "owner@example.com");
        await factory.SeedRentalAsync(farm, uav, gateway);
        foreach (var client in new[] { owner, admin })
        {
            var devices = (await client.GetFromJsonAsync<JsonElement>($"/api/reports/devices?farmId={farm}")).GetProperty("data");
            Assert.Equal(2, devices.GetArrayLength());
            var dashboard = (await client.GetFromJsonAsync<JsonElement>($"/api/dashboard/overview?farmId={farm}")).GetProperty("data");
            Assert.Equal(1, dashboard.GetProperty("gateways").GetProperty("total").GetInt32());
        }
        foreach (var (route, id) in new[] { ("uavs", uav), ("gateways", gateway) })
        {
            var row = (await owner.GetFromJsonAsync<JsonElement>($"/api/{route}/{id}")).GetProperty("data");
            Assert.Equal(farm, row.GetProperty("rental").GetProperty("farmId").GetInt32());
            Assert.Equal("ACTIVE", row.GetProperty("rental").GetProperty("status").GetString());
            Assert.Single((await owner.GetFromJsonAsync<JsonElement>($"/api/{route}?search={code}")).GetProperty("data").EnumerateArray());
        }
        var ownerId = (await owner.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("data").GetProperty("id").GetInt32();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.RentalContracts.Where(x => x.FarmId == farm).ExecuteUpdateAsync(x => x.SetProperty(r => r.EndAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        var expired = (await owner.GetFromJsonAsync<JsonElement>($"/api/uavs/{uav}")).GetProperty("data");
        Assert.Equal("EXPIRED", expired.GetProperty("rental").GetProperty("status").GetString());
        (await admin.DeleteAsync($"/api/farms/{farm}/members/{ownerId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/gateways/{gateway}")).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<JsonElement>($"/api/gateways?search={code}")).GetProperty("data").EnumerateArray());
        await factory.AssignFarmAsync(farm, "owner@example.com");
        db.Missions.Add(new FarmMonitoring.Domain.Entities.Mission
        {
            Name = "Historical rental mission", FarmId = farm, UavId = uav, GatewayId = gateway,
            CreatedByUserId = ownerId, CreatedAt = DateTimeOffset.UtcNow,
            Status = FarmMonitoring.Domain.Entities.MissionStatus.COMPLETED
        });
        await db.SaveChangesAsync();
        await db.RentalDeviceAssignments.Where(x => x.Contract.FarmId == farm).ExecuteUpdateAsync(x => x.SetProperty(d => d.ReturnedAt, DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/uavs/{uav}")).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<JsonElement>($"/api/uavs?search={code}")).GetProperty("data").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/uavs/{uav}")).StatusCode);
        Assert.Empty((await owner.GetFromJsonAsync<JsonElement>($"/api/reports/devices?farmId={farm}")).GetProperty("data").EnumerateArray());
        var returned = (await owner.GetFromJsonAsync<JsonElement>($"/api/dashboard/overview?farmId={farm}")).GetProperty("data");
        Assert.Equal(0, returned.GetProperty("gateways").GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Equipment_validation_and_role_matrix_are_enforced()
    {
        using var op = await Login("admin@example.com");
        Assert.Equal(HttpStatusCode.BadRequest, (await op.PostAsJsonAsync("/api/uavs", new { code = "X", name = "X", status = "ONLINE", batteryPercent = 101 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await op.PostAsJsonAsync("/api/gateways", new { code = "X", name = "X", status = "ONLINE", gatewayType = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await op.PostAsJsonAsync("/api/gateways/2147483647/assign-uav", new { uavId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await op.GetAsync("/api/uavs/2147483647")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await op.GetAsync("/api/gateways?pageSize=101")).StatusCode);
        using var engineer = await Login("engineer@example.com");
        foreach (var route in new[] { "/api/uavs", "/api/gateways" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await engineer.GetAsync(route)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await engineer.PostAsJsonAsync(route, new { })).StatusCode);
        }
        using var anonymous = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/gateways")).StatusCode);
    }
}
