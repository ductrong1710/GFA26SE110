using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class FarmZoneTests(ApiFactory factory)
{
    private async Task<HttpClient> Login(string email = "admin@example.com")
    {
        var client = factory.CreateApiClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" });
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<int> CreateFarm(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/farms", new { name, description = "Demo", latitude = 10.1234567m, longitude = 106.1234567m });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Farm_and_zone_lifecycle_preserves_location_and_history()
    {
        using var admin = await Login();
        var name = $"Farm {Guid.NewGuid():N}";
        var farmId = await CreateFarm(admin, name);
        var farm = (await admin.GetFromJsonAsync<JsonElement>($"/api/farms/{farmId}")).GetProperty("data");
        Assert.Equal(10.1234567m, farm.GetProperty("latitude").GetDecimal());
        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/farms?search={Uri.EscapeDataString(name)}&pageSize=1");
        Assert.Single(list.GetProperty("data").EnumerateArray());
        Assert.Equal(1, list.GetProperty("pagination").GetProperty("totalItems").GetInt32());
        var zoneRequest = new { name = "North", description = "North field", centerLatitude = 10.1m, centerLongitude = 106.2m };
        var created = await admin.PostAsJsonAsync($"/api/farms/{farmId}/zones", zoneRequest);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var zoneId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/farms/{farmId}/zones", zoneRequest)).StatusCode);
        var anotherFarm = await CreateFarm(admin, name + " Second");
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/farms/{anotherFarm}/zones", zoneRequest)).StatusCode);
        var zone = (await admin.GetFromJsonAsync<JsonElement>($"/api/zones/{zoneId}")).GetProperty("data");
        Assert.Equal(farmId, zone.GetProperty("farmId").GetInt32());
        (await admin.PutAsJsonAsync($"/api/zones/{zoneId}", new { name = "Renamed", description = "Updated", centerLatitude = (decimal?)null, centerLongitude = (decimal?)null })).EnsureSuccessStatusCode();
        var zones = await admin.GetFromJsonAsync<JsonElement>($"/api/farms/{farmId}/zones?pageSize=1");
        Assert.Equal("Renamed", zones.GetProperty("data")[0].GetProperty("name").GetString());
        (await admin.PutAsJsonAsync($"/api/farms/{farmId}", new { name = name + " Updated", description = "Changed" })).EnsureSuccessStatusCode();
        (await admin.PatchAsJsonAsync($"/api/farms/{farmId}/status", new { isActive = false })).EnsureSuccessStatusCode();
        Assert.False((await admin.GetFromJsonAsync<JsonElement>($"/api/farms/{farmId}")).GetProperty("data").GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/zones/{zoneId}")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await admin.DeleteAsync($"/api/farms/{farmId}")).StatusCode);
    }

    [Fact]
    public async Task Engineer_can_read_but_cannot_write_farms_or_zones()
    {
        using var admin = await Login();
        var id = await CreateFarm(admin, "Read only");
        await factory.AssignFarmAsync(id, "engineer@example.com");
        using var op = await Login("engineer@example.com");
        foreach (var path in new[] { "/api/farms", $"/api/farms/{id}", $"/api/farms/{id}/zones" })
            Assert.Equal(HttpStatusCode.OK, (await op.GetAsync(path)).StatusCode);
        foreach (var (method, path) in new[] { ("POST", "/api/farms"), ("PUT", $"/api/farms/{id}"), ("PATCH", $"/api/farms/{id}/status"), ("POST", $"/api/farms/{id}/zones"), ("PUT", "/api/zones/1") })
            Assert.Equal(HttpStatusCode.Forbidden, (await op.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) })).StatusCode);
        using var anonymous = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/farms")).StatusCode);
    }

    [Fact]
    public async Task Invalid_coordinates_pagination_and_missing_parents_are_rejected()
    {
        using var admin = await Login();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/farms", new { name = "", latitude = 91, longitude = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/farms", new { name = "Invalid", latitude = 0, longitude = 181 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/farms?pageSize=1000")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/farms/2147483647/zones")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/farms/2147483647/zones", new { name = "Missing parent" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/zones/2147483647", new { name = "Missing zone" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PatchAsJsonAsync("/api/farms/2147483647/status", new { })).StatusCode);
    }
}
