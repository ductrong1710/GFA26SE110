using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class TokenLockExpiryTests(ApiFactory factory)
{
    [Theory]
    [InlineData("refresh")]
    [InlineData("logout")]
    public async Task Token_that_expires_while_waiting_for_row_lock_is_rejected(string action)
    {
        var clock = new AdjustableClock(DateTimeOffset.UtcNow);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "Test-password-123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var data = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var raw = data.GetProperty("refreshToken").GetString()!;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", data.GetProperty("accessToken").GetString());

        await using var blocker = new NpgsqlConnection(factory.ConnectionString);
        await blocker.OpenAsync();
        await using (var update = new NpgsqlCommand("UPDATE refresh_tokens SET expires_at = @expiry WHERE token_hash = @hash", blocker))
        {
            update.Parameters.AddWithValue("expiry", clock.GetUtcNow().AddSeconds(1));
            update.Parameters.AddWithValue("hash", hash);
            await update.ExecuteNonQueryAsync();
        }
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (var rowLock = new NpgsqlCommand("SELECT id FROM refresh_tokens WHERE token_hash = @hash FOR UPDATE", blocker, transaction))
        {
            rowLock.Parameters.AddWithValue("hash", hash);
            await rowLock.ExecuteScalarAsync();
        }
        var pending = client.PostAsJsonAsync("/api/auth/" + action, new { refreshToken = raw });

        await using var observer = new NpgsqlConnection(factory.ConnectionString);
        await observer.OpenAsync();
        var watch = Stopwatch.StartNew();
        var waiting = false;
        while (watch.Elapsed < TimeSpan.FromSeconds(10))
        {
            await using var probe = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%refresh_tokens%'", observer);
            if ((long)(await probe.ExecuteScalarAsync())! > 0) { waiting = true; break; }
            await Task.Delay(20);
        }
        Assert.True(waiting, "Auth request must actually be waiting on the token row lock.");
        clock.Advance(TimeSpan.FromSeconds(2));
        await transaction.CommitAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await pending).StatusCode);
    }

    private sealed class AdjustableClock(DateTimeOffset now) : TimeProvider
    {
        private long utcTicks = now.UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref utcTicks), TimeSpan.Zero);
        public void Advance(TimeSpan amount) => Interlocked.Add(ref utcTicks, amount.Ticks);
    }
}
