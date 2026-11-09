using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class RotationTransactionTests(ApiFactory factory)
{
    [Fact]
    public async Task Failure_revoking_predecessor_rolls_back_inserted_successor()
    {
        using var client = factory.CreateApiClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "Test-password-123!" });
        login.EnsureSuccessStatusCode();
        var data = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var raw = data.GetProperty("refreshToken").GetString()!;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.RefreshTokens.CountAsync();
        // Only the disposable test database is modified. Force the second write
        // in rotation to fail, after the successor insertion has succeeded.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION test_reject_rotation() RETURNS trigger LANGUAGE plpgsql AS
            $$ BEGIN RAISE EXCEPTION 'rotation-test-failure'; END; $$;
            CREATE TRIGGER test_reject_rotation BEFORE UPDATE OF revoked_at ON refresh_tokens
            FOR EACH ROW EXECUTE FUNCTION test_reject_rotation();
            """);
        try
        {
            var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = raw });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.DoesNotContain("rotation-test-failure", await response.Content.ReadAsStringAsync());
            Assert.Equal(before, await db.RefreshTokens.CountAsync());
            var old = await db.RefreshTokens.AsNoTracking().SingleAsync(x => x.TokenHash == hash);
            Assert.Null(old.RevokedAt);
            Assert.Null(old.ReplacedByTokenId);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_reject_rotation ON refresh_tokens; DROP FUNCTION test_reject_rotation();");
        }
        // A rolled-back attempt must leave the original token usable.
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = raw })).StatusCode);
    }
}
