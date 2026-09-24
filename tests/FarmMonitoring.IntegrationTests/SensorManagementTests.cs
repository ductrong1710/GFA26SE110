using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class SensorManagementTests(ApiFactory factory)
{
    private async Task<HttpClient> Login(string email)
    {
        var client = factory.CreateApiClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" });
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<int> CreatedId(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Operator_registers_nodes_and_channels_and_admin_has_read_access()
    {
        using var admin = await Login("admin@example.com");
        using var op = await Login("operator@example.com");
        var farm = await CreatedId(await admin.PostAsJsonAsync("/api/farms", new { name = "Sensor farm" }));
        var zone = await CreatedId(await admin.PostAsJsonAsync($"/api/farms/{farm}/zones", new { name = "Sensor zone" }));
        var typeCode = $"temperature-{Guid.NewGuid():N}";
        var type = await CreatedId(await op.PostAsJsonAsync("/api/sensor-types", new { code = typeCode, name = "Air Temperature", unit = "C" }));
        Assert.Equal(HttpStatusCode.Conflict, (await op.PostAsJsonAsync("/api/sensor-types", new { code = typeCode, name = "Duplicate" })).StatusCode);
        var code = $"SN-{Guid.NewGuid():N}";
        var nodeRequest = new { zoneId = zone, deviceCode = code, name = "Node", status = "OFFLINE", localX = 12.5m, localY = 3m, batteryPercent = 75m };
        var node = await CreatedId(await op.PostAsJsonAsync("/api/sensor-nodes", nodeRequest));
        Assert.Equal(HttpStatusCode.Conflict, (await op.PostAsJsonAsync("/api/sensor-nodes", nodeRequest)).StatusCode);
        var channelRequest = new { sensorTypeId = type, channelCode = "temperature", name = "Temperature" };
        var channel = await CreatedId(await op.PostAsJsonAsync($"/api/sensor-nodes/{node}/channels", channelRequest));
        Assert.Equal(HttpStatusCode.Conflict, (await op.PostAsJsonAsync($"/api/sensor-nodes/{node}/channels", channelRequest)).StatusCode);
        (await op.PutAsJsonAsync($"/api/sensor-channels/{channel}", new { sensorTypeId = type, channelCode = "temperature", name = "Updated", isActive = false })).EnsureSuccessStatusCode();
        (await op.PutAsJsonAsync($"/api/sensor-nodes/{node}", new { zoneId = zone, deviceCode = code, name = "Updated node", status = "ONLINE", batteryPercent = 80m })).EnsureSuccessStatusCode();
        (await op.PatchAsJsonAsync($"/api/sensor-nodes/{node}/status", new { status = "OFFLINE", isActive = false })).EnsureSuccessStatusCode();
        var result = (await admin.GetFromJsonAsync<JsonElement>($"/api/sensor-nodes/{node}")).GetProperty("data");
        Assert.False(result.GetProperty("isActive").GetBoolean());
        Assert.Equal("Updated node", result.GetProperty("name").GetString());
        Assert.Equal(80m, result.GetProperty("batteryPercent").GetDecimal());
        var channels = await admin.GetFromJsonAsync<JsonElement>($"/api/sensor-nodes/{node}/channels");
        Assert.False(channels.GetProperty("data")[0].GetProperty("isActive").GetBoolean());
        var nodes = await admin.GetFromJsonAsync<JsonElement>($"/api/sensor-nodes?zoneId={zone}&search={code}");
        Assert.Single(nodes.GetProperty("data").EnumerateArray());
        Assert.Equal(1, nodes.GetProperty("pagination").GetProperty("totalItems").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/sensor-types")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/sensor-nodes", nodeRequest)).StatusCode);
    }

    [Fact]
    public async Task Sensor_validation_foreign_keys_and_authorization_are_enforced()
    {
        using var op = await Login("operator@example.com");
        Assert.Equal(HttpStatusCode.BadRequest, (await op.PostAsJsonAsync("/api/sensor-types", new { code = "", name = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await op.PostAsJsonAsync("/api/sensor-nodes", new { zoneId = 1, deviceCode = "X", name = "X", status = "ONLINE", batteryPercent = 101 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await op.PostAsJsonAsync("/api/sensor-nodes", new { zoneId = int.MaxValue, deviceCode = "X", name = "X", status = "OFFLINE" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await op.GetAsync("/api/sensor-nodes/2147483647/channels")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await op.PutAsJsonAsync("/api/sensor-channels/2147483647", new { sensorTypeId = 1, channelCode = "x", isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await op.GetAsync("/api/sensor-nodes?pageSize=101")).StatusCode);
        using var anonymous = factory.CreateApiClient();
        foreach (var path in new[] { "/api/sensor-types", "/api/sensor-nodes", "/api/sensor-nodes/1/channels" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
    }
}
