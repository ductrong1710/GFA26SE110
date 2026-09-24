using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class AuthSecurityTests(ApiFactory factory)
{
    [Fact]
    public async Task Login_normalizes_email_and_stores_only_token_hash()
    {
        using var client = factory.CreateApiClient();
        var login = await Login(client, "  ADMIN@EXAMPLE.COM ");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var raw = login.GetProperty("refreshToken").GetString()!;
        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        var stored = await db.RefreshTokens.SingleAsync(x => x.TokenHash == expectedHash);
        Assert.NotEqual(raw, stored.TokenHash);
        Assert.Null(stored.RevokedAt);
        var user = await db.Users.FindAsync(stored.UserId);
        Assert.NotEqual("Test-password-123!", user!.PasswordHash);
        Assert.Equal("admin@example.com", login.GetProperty("user").GetProperty("email").GetString());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(login.GetProperty("accessToken").GetString());
        Assert.Contains(jwt.Claims, c => c.Type == "sub" && c.Value == user.Id.ToString());
        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "FarmAdministrator");
        Assert.DoesNotContain(jwt.Claims, c => c.Type.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(900, login.GetProperty("expiresIn").GetInt32());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Expired_or_revoked_refresh_token_is_rejected(bool expired)
    {
        using var client = factory.CreateApiClient();
        var login = await Login(client);
        var token = login.GetProperty("refreshToken").GetString()!;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var stored = await db.RefreshTokens.SingleAsync(x => x.TokenHash == hash);
        if (expired) stored.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        else stored.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token })).StatusCode);
    }

    [Fact]
    public async Task Concurrent_refresh_creates_exactly_one_successor()
    {
        using var client = factory.CreateApiClient();
        var login = await Login(client);
        var token = login.GetProperty("refreshToken").GetString()!;
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token })));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Equal(7, responses.Count(x => x.StatusCode == HttpStatusCode.Unauthorized));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var old = await db.RefreshTokens.SingleAsync(x => x.TokenHash == hash);
        Assert.NotNull(old.RevokedAt);
        Assert.NotNull(old.ReplacedByTokenId);
        var successor = await db.RefreshTokens.FindAsync(old.ReplacedByTokenId.Value);
        Assert.NotNull(successor);
        Assert.Null(successor.RevokedAt);
        var successBody = await responses.Single(x => x.IsSuccessStatusCode).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = successBody.GetProperty("data").GetProperty("refreshToken").GetString()
        })).StatusCode);
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task Logout_cannot_revoke_another_users_token()
    {
        using var client = factory.CreateApiClient();
        var admin = await Login(client);
        var other = await Login(client, "operator@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.GetProperty("accessToken").GetString());
        var token = new { refreshToken = other.GetProperty("refreshToken").GetString() };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/logout", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/refresh", token)).StatusCode);
    }

    [Fact]
    public async Task Role_authorization_allows_admin_and_forbids_operator()
    {
        using var client = factory.CreateApiClient();
        var admin = await Login(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/test/admin")).StatusCode);
        var other = await Login(client, "operator@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", other.GetProperty("accessToken").GetString());
        var forbidden = await client.GetAsync("/test/admin");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.False((await forbidden.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("success").GetBoolean());
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("wrong-signature")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("missing-sub")]
    public async Task Invalid_access_tokens_are_rejected(string kind)
    {
        using var client = factory.CreateApiClient();
        var login = await Login(client);
        var id = login.GetProperty("user").GetProperty("id").GetInt32().ToString();
        var key = kind == "wrong-signature" ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)) : factory.SigningKey;
        var token = new JwtSecurityToken(
            kind == "wrong-issuer" ? "wrong" : "FarmMonitoring.Tests",
            kind == "wrong-audience" ? "wrong" : "FarmMonitoring.Tests.Client",
            kind == "missing-sub" ? [] : new[] { new Claim("sub", id) },
            DateTime.UtcNow.AddMinutes(-10),
            kind == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.DoesNotContain("exception", body.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_refresh_and_public_registration_are_rejected()
    {
        using var client = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "not-issued" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/auth/register", new { })).StatusCode);
    }

    private static async Task<JsonElement> Login(HttpClient client, string email = "admin@example.com")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Test-password-123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").Clone();
    }
}
