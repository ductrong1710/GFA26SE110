using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class AuthFlowTests(ApiFactory factory)
{
    [Fact]
    public async Task Login_returns_tokens_and_public_user_information()
    {
        using var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "Test-password-123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");
        Assert.NotEmpty(data.GetProperty("accessToken").GetString()!);
        Assert.NotEmpty(data.GetProperty("refreshToken").GetString()!);
        Assert.True(data.GetProperty("accessTokenExpiresAt").GetDateTimeOffset() > DateTimeOffset.UtcNow);
        Assert.Equal("admin@example.com", data.GetProperty("user").GetProperty("email").GetString());
        Assert.False(data.GetProperty("user").TryGetProperty("passwordHash", out _));
    }

    [Theory]
    [InlineData("admin@example.com", "wrong")]
    [InlineData("unknown@example.com", "Test-password-123!")]
    [InlineData("disabled@example.com", "Test-password-123!")]
    public async Task Invalid_credentials_or_inactive_account_return_generic_401(string email, string password)
    {
        using var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Invalid email or password.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Refresh_rotates_token_and_rejects_reuse()
    {
        using var client = factory.CreateApiClient();
        var login = await Login(client);
        var old = login.GetProperty("refreshToken").GetString();
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = old });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(old, body.GetProperty("data").GetProperty("refreshToken").GetString());
        Assert.NotEqual(login.GetProperty("accessToken").GetString(), body.GetProperty("data").GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = old })).StatusCode);
    }

    [Fact]
    public async Task Me_with_valid_token_returns_current_user()
    {
        using var client = factory.CreateApiClient();
        var login = await Login(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("admin@example.com", body.GetProperty("data").GetProperty("email").GetString());
    }

    [Fact]
    public async Task Logout_revokes_token()
    {
        using var client = factory.CreateApiClient();
        var login = await Login(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.GetProperty("accessToken").GetString());
        var token = new { refreshToken = login.GetProperty("refreshToken").GetString() };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/logout", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", token)).StatusCode);
    }

    private static async Task<JsonElement> Login(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "Test-password-123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").Clone();
    }
}

