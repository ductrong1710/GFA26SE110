using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class ThresholdTests(ApiFactory factory)
{
    private async Task<HttpClient> Login(string email)
    {
        var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" });
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("accessToken").GetString());
        return client;
    }
    private async Task<int> CreateChannel()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var channel = new SensorChannel
        {
            ChannelCode = "temperature", CreatedAt = DateTimeOffset.UtcNow,
            SensorType = new SensorType { Code = Guid.NewGuid().ToString("N"), Name = "Temperature" },
            SensorNode = new SensorNode { DeviceCode = Guid.NewGuid().ToString("N"), Name = "Node", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow,
                Zone = new Zone { Name = "Zone", CreatedAt = DateTimeOffset.UtcNow, Farm = new Farm { Name = "Threshold farm", CreatedAt = DateTimeOffset.UtcNow } } }
        };
        db.SensorChannels.Add(channel);
        await db.SaveChangesAsync();
        return channel.Id;
    }

    [Fact]
    public async Task Administrator_upserts_threshold_with_bounds_and_operator_cannot_write()
    {
        var id = await CreateChannel();
        using var admin = await Login("admin@example.com");
        var path = $"/api/sensor-channels/{id}/threshold";
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(path)).StatusCode);
        var initial = await admin.PutAsJsonAsync(path, new { minValue = 10m, maxValue = 35m, dataTimeoutMinutes = 60, lowBatteryPercent = 15m, isEnabled = true });
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        var old = (await initial.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        (await admin.PutAsJsonAsync(path, new { minValue = (decimal?)null, maxValue = 40m, dataTimeoutMinutes = 30, lowBatteryPercent = 20m, isEnabled = false })).EnsureSuccessStatusCode();
        var current = (await admin.GetFromJsonAsync<JsonElement>(path)).GetProperty("data");
        Assert.Equal(old.GetProperty("id").GetInt32(), current.GetProperty("id").GetInt32());
        Assert.Equal(old.GetProperty("createdAt").GetDateTimeOffset(), current.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(40m, current.GetProperty("maxValue").GetDecimal());
        Assert.Equal(JsonValueKind.Null, current.GetProperty("minValue").ValueKind);
        Assert.False(current.GetProperty("isEnabled").GetBoolean());
        using var op = await Login("operator@example.com");
        Assert.Equal(HttpStatusCode.OK, (await op.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PutAsJsonAsync(path, new { maxValue = 50 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(path, new { minValue = 40, maxValue = 10 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(path, new { lowBatteryPercent = 101, dataTimeoutMinutes = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/sensor-channels/2147483647/threshold", new { maxValue = 40 })).StatusCode);
    }

    [Fact]
    public async Task Concurrent_configuration_creates_one_threshold()
    {
        var id = await CreateChannel();
        using var admin = await Login("admin@example.com");
        var path = $"/api/sensor-channels/{id}/threshold";
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => admin.PutAsJsonAsync(path, new { maxValue = 30 + i })));
        var ids = new List<int>();
        foreach (var response in responses)
        {
            response.EnsureSuccessStatusCode();
            ids.Add((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32());
        }
        Assert.Single(ids.Distinct());
    }
}
