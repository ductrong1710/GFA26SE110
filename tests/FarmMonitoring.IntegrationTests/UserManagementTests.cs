using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class UserManagementTests(ApiFactory factory)
{
    private async Task<HttpClient> Login(string email = "admin@example.com")
    {
        var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("data").GetProperty("accessToken").GetString());
        return client;
    }

    [Fact]
    public async Task Administrator_can_manage_account_and_roles_without_exposing_secrets()
    {
        using var admin = await Login();
        var email = $"managed-{Guid.NewGuid():N}@example.com";
        var created = await admin.PostAsJsonAsync("/api/users", new { email = email.ToUpperInvariant(), fullName = "New Operator", password = "Test-password-123!", roles = new[] { "UavDeviceOperator" } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var user = body.GetProperty("data");
        var id = user.GetProperty("id").GetInt32();
        Assert.Equal(email, user.GetProperty("email").GetString());
        Assert.False(user.TryGetProperty("passwordHash", out _));
        Assert.False(user.TryGetProperty("password", out _));
        Assert.Equal($"/api/users/{id}", created.Headers.Location!.AbsolutePath);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/users/{id}")).StatusCode);
        using var account = await Login(email);
        Assert.Equal(HttpStatusCode.Forbidden, (await account.GetAsync("/api/users")).StatusCode);

        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/users?search={email}&page=1&pageSize=1");
        Assert.Single(list.GetProperty("data").EnumerateArray());
        Assert.Equal(1, list.GetProperty("pagination").GetProperty("totalItems").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/users/{id}", new { email, fullName = "Updated Operator" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/users/{id}/roles", new { roles = new[] { "FarmAdministrator", "UavDeviceOperator" } })).StatusCode);
        using var promoted = await Login(email);
        Assert.Equal(HttpStatusCode.OK, (await promoted.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/users/{id}/roles", new { roles = new[] { "UavDeviceOperator" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await promoted.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/users/{id}/status", new { isActive = false })).StatusCode);
        var denied = await admin.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/users/{id}/status", new { isActive = true })).StatusCode);
        using var reactivated = await Login(email);

        await using var scope = factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.NotEqual("Test-password-123!", stored.PasswordHash);
        Assert.Equal("Updated Operator", stored.FullName);
        Assert.NotNull(stored.UpdatedAt);
    }

    [Fact]
    public async Task Management_validates_input_duplicates_and_missing_resources()
    {
        using var admin = await Login();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/users", new { email = "bad", fullName = "", password = "short", roles = new[] { "unknown" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/users", new { email = "ADMIN@example.com", fullName = "Duplicate", password = "Test-password-123!", roles = new[] { "FarmAdministrator" } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/users/2147483647")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/users?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/users?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PatchAsJsonAsync("/api/users/2147483647/status", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/users/2147483647/roles", new { roles = new[] { "unknown" } })).StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/users")]
    [InlineData("GET", "/api/users/1")]
    [InlineData("POST", "/api/users")]
    [InlineData("PUT", "/api/users/1")]
    [InlineData("PATCH", "/api/users/1/status")]
    [InlineData("PUT", "/api/users/1/roles")]
    public async Task All_management_routes_require_administrator(string method, string path)
    {
        using var anonymous = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) })).StatusCode);
        using var op = await Login("operator@example.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await op.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) })).StatusCode);
    }

    [Fact]
    public async Task Disabled_administrator_loses_access_and_role_replacement_can_remove_all_roles()
    {
        using var admin = await Login();
        var email = $"temporary-admin-{Guid.NewGuid():N}@example.com";
        var created = await admin.PostAsJsonAsync("/api/users", new { email, fullName = "Temporary Admin", password = "Test-password-123!", roles = new[] { "FarmAdministrator" } });
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        using var target = await Login(email);
        (await admin.PatchAsJsonAsync($"/api/users/{id}/status", new { isActive = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await target.GetAsync("/api/users")).StatusCode);
        (await admin.PutAsJsonAsync($"/api/users/{id}/roles", new { roles = Array.Empty<string>() })).EnsureSuccessStatusCode();
        var user = (await admin.GetFromJsonAsync<JsonElement>($"/api/users/{id}")).GetProperty("data");
        Assert.Empty(user.GetProperty("roles").EnumerateArray());
    }

    [Fact]
    public async Task Concurrent_normalized_duplicate_emails_create_only_one_account()
    {
        using var admin = await Login();
        var email = $"concurrent-{Guid.NewGuid():N}@example.com";
        var responses = await Task.WhenAll(new[] { email, email.ToUpperInvariant() }.Select(x =>
            admin.PostAsJsonAsync("/api/users", new { email = x, fullName = "Concurrent", password = "Test-password-123!", roles = new[] { "UavDeviceOperator" } })));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Concurrent_role_replacements_do_not_merge_privileges()
    {
        using var admin = await Login();
        var created = await admin.PostAsJsonAsync("/api/users", new { email = $"role-race-{Guid.NewGuid():N}@example.com", fullName = "Role Race", password = "Test-password-123!", roles = Array.Empty<string>() });
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetInt32();
        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("SELECT id FROM users WHERE id = @id FOR UPDATE", connection, transaction))
        {
            command.Parameters.AddWithValue("id", id);
            await command.ExecuteScalarAsync();
        }
        var first = admin.PutAsJsonAsync($"/api/users/{id}/roles", new { roles = new[] { "FarmAdministrator" } });
        var second = admin.PutAsJsonAsync($"/api/users/{id}/roles", new { roles = new[] { "UavDeviceOperator" } });
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (true)
            {
                await using var waiting = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'", connection, transaction);
                if (Convert.ToInt32(await waiting.ExecuteScalarAsync(timeout.Token)) >= 2) break;
                // PostgreSQL statistics are cached per transaction; refresh the snapshot.
                await using var clear = new NpgsqlCommand("SELECT pg_stat_clear_snapshot()", connection, transaction);
                await clear.ExecuteNonQueryAsync(timeout.Token);
                await Task.Delay(25, timeout.Token);
            }
        }
        finally { await transaction.CommitAsync(); }
        foreach (var response in await Task.WhenAll(first, second)) response.EnsureSuccessStatusCode();
        var user = (await admin.GetFromJsonAsync<JsonElement>($"/api/users/{id}")).GetProperty("data");
        Assert.Single(user.GetProperty("roles").EnumerateArray());
    }
}
