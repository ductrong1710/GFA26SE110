using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class FarmZoneTests(ApiFactory factory)
{
    [Fact]
    public async Task Geographic_boundaries_round_trip_validate_and_preserve_legacy_records()
    {
        using var client = await Login();
        object Box(double x, double y, double size) => new { type = "Polygon",
            coordinates = new[] { new[] { new[] { x, y }, new[] { x + size, y }, new[] { x + size, y + size }, new[] { x, y + size }, new[] { x, y } } } };
        var name = $"Boundary {Guid.NewGuid():N}";
        var created = await client.PostAsJsonAsync("/api/farms", new { name, boundary = Box(106, 10, 1) });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var farm = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var farmId = farm.GetProperty("id").GetInt32();
        Assert.Equal(10.5m, farm.GetProperty("latitude").GetDecimal());
        Assert.Equal(106.5m, farm.GetProperty("longitude").GetDecimal());
        var listed = (await client.GetFromJsonAsync<JsonElement>($"/api/farms?search={Uri.EscapeDataString(name)}")).GetProperty("data")[0];
        Assert.Equal(farm.GetProperty("boundary").ToString(), listed.GetProperty("boundary").ToString());
        var zoneCreated = await client.PostAsJsonAsync($"/api/farms/{farmId}/zones", new { name = "Inside", boundary = Box(106.2, 10.2, 0.2) });
        Assert.Equal(HttpStatusCode.Created, zoneCreated.StatusCode);
        var zoneId = (await zoneCreated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        var zone = (await client.GetFromJsonAsync<JsonElement>($"/api/zones/{zoneId}")).GetProperty("data");
        Assert.Equal(10.3m, zone.GetProperty("centerLatitude").GetDecimal());
        Assert.Equal("Polygon", zone.GetProperty("boundary").GetProperty("type").GetString());
        var zones = (await client.GetFromJsonAsync<JsonElement>($"/api/farms/{farmId}/zones")).GetProperty("data");
        Assert.Equal(zone.GetProperty("boundary").ToString(), zones[0].GetProperty("boundary").ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/farms/{farmId}/zones", new { name = "Outside", boundary = Box(107, 11, 1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/zones/{zoneId}", new { name = "Outside", boundary = Box(107, 11, 1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/farms/{farmId}", new { name, boundary = Box(106, 10, 0.1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/farms", new { name = "Invalid", boundary = Box(181, 10, 1) })).StatusCode);
        var legacy = await CreateFarm(client, "Legacy boundary compatibility");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/farms/{legacy}/zones", new { name = "Unvalidated parent", boundary = Box(106, 10, 1) })).StatusCode);
        (await client.PutAsJsonAsync($"/api/zones/{zoneId}", new { name = "Cleared", boundary = (object?)null })).EnsureSuccessStatusCode();
        Assert.Equal(JsonValueKind.Null, (await client.GetFromJsonAsync<JsonElement>($"/api/zones/{zoneId}")).GetProperty("data").GetProperty("boundary").ValueKind);
    }

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
