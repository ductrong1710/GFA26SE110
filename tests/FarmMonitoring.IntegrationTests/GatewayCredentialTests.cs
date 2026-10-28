using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public sealed class GatewayCredentialTests(ApiFactory factory)
{
    [Fact]
    public async Task Heartbeat_authenticates_device_and_catalog_expires_contact()
    {
        var gateway = await Gateway();
        using var human = await Login();
        var key = (await Create(human, gateway.Id)).GetProperty("apiKey").GetString()!;
        using var device = Device(gateway.Code, key);
        var path = $"/api/device/gateways/{gateway.Id}/heartbeat";
        Assert.Equal("OFFLINE", (await Data(await human.GetAsync($"/api/gateways/{gateway.Id}"))).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await human.PostAsync(path, null)).StatusCode);
        using var wrong = Device(gateway.Code, "invalid-key");
        Assert.Equal(HttpStatusCode.Unauthorized, (await wrong.PostAsync(path, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await device.PostAsync($"/api/device/gateways/{gateway.Id + 10000}/heartbeat", null)).StatusCode);
        var beat = await Data(await device.PostAsync(path, null));
        Assert.Equal("ONLINE", beat.GetProperty("status").GetString());
        Assert.Equal("ONLINE", (await Data(await human.GetAsync($"/api/gateways/{gateway.Id}"))).GetProperty("status").GetString());
        var list = await Data(await human.GetAsync($"/api/gateways?search={gateway.Code}"));
        Assert.Equal("ONLINE", list[0].GetProperty("status").GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Gateways.Where(x => x.Id == gateway.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.LastSeenAt, DateTimeOffset.UtcNow.AddSeconds(-91)));
        Assert.Equal("OFFLINE", (await Data(await human.GetAsync($"/api/gateways/{gateway.Id}"))).GetProperty("status").GetString());
        // Human metadata cannot spoof connectivity.
        (await human.PutAsJsonAsync($"/api/gateways/{gateway.Id}", new { code = gateway.Code, name = gateway.Name,
            gatewayType = gateway.GatewayType, status = "ONLINE", lastSeenAt = DateTimeOffset.UtcNow })).EnsureSuccessStatusCode();
        Assert.Equal("OFFLINE", (await Data(await human.GetAsync($"/api/gateways/{gateway.Id}"))).GetProperty("status").GetString());
        var uav = new Uav { Code = Guid.NewGuid().ToString("N"), Name = "Heartbeat reserved UAV", CreatedAt = DateTimeOffset.UtcNow };
        db.Uavs.Add(uav);
        await db.SaveChangesAsync();
        var reserved = new FlightTest { GatewayId = gateway.Id, UavId = uav.Id,
            RequestedByUserId = await db.Users.Where(x => x.Email == "admin@example.com").Select(x => x.Id).SingleAsync(),
            RequestKey = Guid.NewGuid().ToString("N"), Status = FlightTestStatus.CLAIMED,
            CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2) };
        db.FlightTests.Add(reserved);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await device.PostAsync(path, null)).StatusCode);
        Assert.Equal(FlightTestStatus.CLAIMED, (await db.FlightTests.AsNoTracking().SingleAsync(x => x.Id == reserved.Id)).Status);
        await db.Gateways.Where(x => x.Id == gateway.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.PostAsync(path, null)).StatusCode);
    }

    [Fact]
    public async Task Create_returns_key_once_and_persists_only_hash_with_creator()
    {
        var gateway = await Gateway();
        using var human = await Login();
        var empty = await Data(await human.GetAsync(Path(gateway.Id) + "/status"));
        Assert.False(empty.GetProperty("hasCredential").GetBoolean());
        Assert.False(empty.GetProperty("isActive").GetBoolean());
        var created = await Create(human, gateway.Id);
        var key = created.GetProperty("apiKey").GetString()!;
        Assert.True(key.Length >= 43); // At least the encoded length of 32 random bytes.
        Assert.Equal(gateway.Id, created.GetProperty("gatewayId").GetInt32());
        Assert.Equal(gateway.Code, created.GetProperty("gatewayCode").GetString());
        Assert.True(created.GetProperty("createdAt").GetDateTimeOffset() <= DateTimeOffset.UtcNow);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.GatewayCredentials.SingleAsync(x => x.GatewayId == gateway.Id);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))), stored.KeyHash, ignoreCase: true);
        Assert.NotEqual(key, stored.KeyHash);
        Assert.Equal(64, stored.KeyHash.Length);
        Assert.True(stored.IsActive);
        Assert.Null(stored.RevokedAt);
        Assert.Null(stored.LastUsedAt);
        Assert.Equal(await db.Users.Where(x => x.Email == "admin@example.com").Select(x => x.Id).SingleAsync(), stored.CreatedByUserId);
        Assert.Equal(HttpStatusCode.Conflict, (await human.PostAsync(Path(gateway.Id), null)).StatusCode);
    }

    [Fact]
    public async Task Raw_key_authenticates_existing_sync_and_telemetry_headers_and_updates_last_used()
    {
        var gateway = await Gateway();
        using var human = await Login();
        var key = (await Create(human, gateway.Id)).GetProperty("apiKey").GetString()!;
        using var device = Device(gateway.Code, key);
        Assert.Equal(HttpStatusCode.OK, (await Sync(device, gateway.Id)).StatusCode);
        int missionId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var mission = new Mission { Name = "Credential telemetry", GatewayId = gateway.Id,
                Farm = new Farm { Name = "Credential farm", CreatedAt = DateTimeOffset.UtcNow },
                Status = MissionStatus.RUNNING, CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = await db.Users.Where(x => x.Email == "admin@example.com").Select(x => x.Id).SingleAsync() };
            db.Missions.Add(mission);
            await db.SaveChangesAsync();
            missionId = mission.Id;
        }
        var telemetry = new { missionId, gatewayId = gateway.Id, recordedAt = DateTimeOffset.UtcNow, batteryPercent = 80 };
        Assert.Equal(HttpStatusCode.Created, (await device.PostAsJsonAsync("/api/device/telemetry", telemetry)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await human.PostAsJsonAsync("/api/device/telemetry", telemetry)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Sync(human, gateway.Id)).StatusCode);
        var status = await Data(await human.GetAsync(Path(gateway.Id) + "/status"));
        Assert.NotEqual(JsonValueKind.Null, status.GetProperty("lastUsedAt").ValueKind);
        Assert.Equal(HttpStatusCode.Unauthorized, (await device.GetAsync(Path(gateway.Id) + "/status")).StatusCode);
    }

    [Theory]
    [InlineData("wrong-key")]
    [InlineData("wrong-code")]
    [InlineData("missing-key")]
    [InlineData("missing-code")]
    public async Task Invalid_or_missing_device_headers_are_rejected(string fault)
    {
        var gateway = await Gateway();
        using var human = await Login();
        var key = (await Create(human, gateway.Id)).GetProperty("apiKey").GetString()!;
        using var device = Device(fault == "wrong-code" ? "unknown-" + gateway.Code : gateway.Code,
            fault == "wrong-key" ? key + "invalid" : key);
        if (fault == "missing-key") device.DefaultRequestHeaders.Remove("X-Api-Key");
        if (fault == "missing-code") device.DefaultRequestHeaders.Remove("X-Gateway-Code");
        Assert.Equal(HttpStatusCode.Unauthorized, (await Sync(device, gateway.Id)).StatusCode);
    }

    [Fact]
    public async Task Revoke_immediately_rejects_key_preserves_history_and_is_idempotent()
    {
        var gateway = await Gateway();
        using var human = await Login();
        var key = (await Create(human, gateway.Id)).GetProperty("apiKey").GetString()!;
        using var device = Device(gateway.Code, key);
        Assert.Equal(HttpStatusCode.OK, (await Sync(device, gateway.Id)).StatusCode);
        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.NoContent, (await human.DeleteAsync(Path(gateway.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Sync(device, gateway.Id)).StatusCode);
        var status = await Data(await human.GetAsync(Path(gateway.Id) + "/status"));
        Assert.True(status.GetProperty("hasCredential").GetBoolean());
        Assert.False(status.GetProperty("isActive").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, status.GetProperty("revokedAt").ValueKind);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var credential = await db.GatewayCredentials.SingleAsync(x => x.GatewayId == gateway.Id);
        Assert.False(credential.IsActive);
        Assert.NotNull(credential.RevokedAt);
        Assert.Equal(HttpStatusCode.Conflict, (await human.PostAsync(Path(gateway.Id) + "/rotate", null)).StatusCode);
        await Create(human, gateway.Id);
    }

    [Fact]
    public async Task Rotation_invalidates_previous_key_and_new_key_authenticates()
    {
        var gateway = await Gateway();
        using var human = await Login();
        var previous = (await Create(human, gateway.Id)).GetProperty("apiKey").GetString()!;
        var response = await human.PostAsync(Path(gateway.Id) + "/rotate", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rotated = await Data(response);
        var key = rotated.GetProperty("apiKey").GetString()!;
        Assert.NotEqual(previous, key);
        Assert.Equal(gateway.Code, rotated.GetProperty("gatewayCode").GetString());
        using var oldDevice = Device(gateway.Code, previous);
        using var newDevice = Device(gateway.Code, key);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Sync(oldDevice, gateway.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Sync(newDevice, gateway.Id)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<AppDbContext>().GatewayCredentials.Where(x => x.GatewayId == gateway.Id).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Single(rows, x => x.IsActive && x.RevokedAt == null);
        Assert.Single(rows, x => !x.IsActive && x.RevokedAt != null);
    }

    [Fact]
    public async Task Status_and_gateway_catalog_never_disclose_key_or_hash()
    {
        var gateway = await Gateway();
        using var human = await Login();
        var key = (await Create(human, gateway.Id)).GetProperty("apiKey").GetString()!;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        foreach (var path in new[] { Path(gateway.Id) + "/status", $"/api/gateways/{gateway.Id}", "/api/gateways" })
        {
            var response = await human.GetAsync(path);
            response.EnsureSuccessStatusCode();
            var text = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(key, text);
            Assert.DoesNotContain(hash, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("keyHash", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("apiKey", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("engineer@example.com", HttpStatusCode.Forbidden)]
    public async Task All_management_operations_require_ManageDevices(string? email, HttpStatusCode expected)
    {
        var gateway = await Gateway();
        using var client = email == null ? factory.CreateApiClient() : await Login(email);
        Assert.Equal(expected, (await client.GetAsync(Path(gateway.Id) + "/status")).StatusCode);
        Assert.Equal(expected, (await client.PostAsync(Path(gateway.Id), null)).StatusCode);
        Assert.Equal(expected, (await client.PostAsync(Path(gateway.Id) + "/rotate", null)).StatusCode);
        Assert.Equal(expected, (await client.DeleteAsync(Path(gateway.Id))).StatusCode);
    }

    [Fact]
    public async Task Owner_retains_existing_global_ManageDevices_permission()
    {
        var gateway = await Gateway();
        using var owner = await Login("owner@example.com");
        await Create(owner, gateway.Id);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(Path(gateway.Id) + "/status")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync(Path(gateway.Id) + "/rotate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(Path(gateway.Id))).StatusCode);
    }

    [Fact]
    public async Task Inactive_gateway_rejects_authentication_and_create_or_rotate()
    {
        var gateway = await Gateway();
        using var human = await Login();
        var key = (await Create(human, gateway.Id)).GetProperty("apiKey").GetString()!;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Gateways.FindAsync(gateway.Id))!.IsActive = false;
            await db.SaveChangesAsync();
        }
        using var device = Device(gateway.Code, key);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Sync(device, gateway.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await human.PostAsync(Path(gateway.Id), null)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await human.PostAsync(Path(gateway.Id) + "/rotate", null)).StatusCode);
        var status = await Data(await human.GetAsync(Path(gateway.Id) + "/status"));
        Assert.True(status.GetProperty("hasCredential").GetBoolean());
        Assert.False(status.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Missing_gateways_return_404_and_rotate_without_credential_returns_409()
    {
        using var human = await Login();
        var missing = Path(int.MaxValue);
        Assert.Equal(HttpStatusCode.NotFound, (await human.GetAsync(missing + "/status")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await human.PostAsync(missing, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await human.PostAsync(missing + "/rotate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await human.DeleteAsync(missing)).StatusCode);
        var gateway = await Gateway();
        Assert.Equal(HttpStatusCode.Conflict, (await human.PostAsync(Path(gateway.Id) + "/rotate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await human.DeleteAsync(Path(gateway.Id))).StatusCode);
    }

    [Fact]
    public async Task Concurrent_creates_have_one_winner_and_database_rejects_second_active_credential()
    {
        var gateway = await Gateway();
        using var human = await Login();
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => human.PostAsync(Path(gateway.Id), null)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Equal(3, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.GatewayCredentials.CountAsync(x => x.GatewayId == gateway.Id && x.IsActive));
        db.GatewayCredentials.Add(new GatewayCredential { GatewayId = gateway.Id, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow, KeyHash = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Concurrent_rotation_and_revocation_leave_no_active_key()
    {
        var gateway = await Gateway();
        using var human = await Login();
        var original = (await Create(human, gateway.Id)).GetProperty("apiKey").GetString()!;
        var results = await Task.WhenAll(human.PostAsync(Path(gateway.Id) + "/rotate", null), human.DeleteAsync(Path(gateway.Id)));
        Assert.Contains(results[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        Assert.Equal(HttpStatusCode.NoContent, results[1].StatusCode);
        using var oldDevice = Device(gateway.Code, original);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Sync(oldDevice, gateway.Id)).StatusCode);
        if (results[0].StatusCode == HttpStatusCode.OK)
        {
            var rotated = (await Data(results[0])).GetProperty("apiKey").GetString()!;
            using var newDevice = Device(gateway.Code, rotated);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Sync(newDevice, gateway.Id)).StatusCode);
        }
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().GatewayCredentials.AnyAsync(x => x.GatewayId == gateway.Id && x.IsActive));
    }

    [Fact]
    public async Task Legacy_configured_key_cannot_authenticate_without_database_credential()
    {
        var gateway = await Gateway();
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DeviceAuthentication:Credentials:0:GatewayCode"] = gateway.Code,
                ["DeviceAuthentication:Credentials:0:KeyHash"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))
            })));
        using var device = configured.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        device.DefaultRequestHeaders.Add("X-Gateway-Code", gateway.Code);
        device.DefaultRequestHeaders.Add("X-Api-Key", key);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Sync(device, gateway.Id)).StatusCode);
    }

    [Fact]
    public async Task Migration_upgrades_existing_gateway_without_automatically_issuing_credentials()
    {
        await using var upgrading = new ApiFactory { InitialMigration = "20261013014617_SeedSampleAdministrator" };
        await upgrading.InitializeAsync();
        await using var scope = upgrading.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gateway = new Gateway { Code = "existing-gateway", Name = "Existing", GatewayType = "ESP32", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow };
        db.Gateways.Add(gateway);
        await db.SaveChangesAsync();
        await db.GetService<IMigrator>().MigrateAsync();
        Assert.Empty(await db.GatewayCredentials.ToListAsync());
        Assert.True(await db.Gateways.AnyAsync(x => x.Id == gateway.Id && x.Code == "existing-gateway"));
        var columns = await db.Database.SqlQueryRaw<string>("SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name='gateway_credentials'").ToArrayAsync();
        Assert.DoesNotContain("api_key", columns);
        Assert.DoesNotContain("raw_key", columns);
        Assert.Contains("key_hash", columns);
        Assert.Equal("YES", await db.Database.SqlQueryRaw<string>("SELECT is_identity AS \"Value\" FROM information_schema.columns WHERE table_name='gateway_credentials' AND column_name='id'").SingleAsync());
        var indexes = await db.Database.SqlQueryRaw<string>("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename='gateway_credentials'").ToArrayAsync();
        Assert.Contains(indexes, x => x.Contains("UNIQUE") && x.Contains("(gateway_id)") && x.Contains("WHERE is_active"));
        await db.GetService<IMigrator>().MigrateAsync();
        Assert.Empty(await db.GatewayCredentials.ToListAsync());
    }

    private static string Path(int id) => $"/api/gateways/{id}/credential";
    private async Task<Gateway> Gateway()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gateway = new Gateway { Code = "credential-" + Guid.NewGuid().ToString("N"), Name = "Credential test gateway",
            GatewayType = "ESP32", Status = "ONLINE", CreatedAt = DateTimeOffset.UtcNow };
        db.Gateways.Add(gateway);
        await db.SaveChangesAsync();
        return gateway;
    }
    private async Task<HttpClient> Login(string email = "admin@example.com")
    {
        var client = factory.CreateApiClient();
        var data = await Data(await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());
        return client;
    }
    private HttpClient Device(string code, string key)
    {
        var client = factory.CreateApiClient();
        client.DefaultRequestHeaders.Add("X-Gateway-Code", code);
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }
    private static Task<HttpResponseMessage> Sync(HttpClient client, int gatewayId) =>
        client.PostAsJsonAsync($"/api/device/gateways/{gatewayId}/sync", new { batchKey = Guid.NewGuid().ToString("N"), records = Array.Empty<object>() });
    private static async Task<JsonElement> Create(HttpClient client, int id)
    {
        var response = await client.PostAsync(Path(id), null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Data(response);
    }
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }
}
