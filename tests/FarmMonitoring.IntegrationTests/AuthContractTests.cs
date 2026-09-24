using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class AuthContractTests(ApiFactory factory)
{
    [Theory]
    [InlineData("/api/auth/login", "{}")]
    [InlineData("/api/auth/login", "{\"email\":\"bad-email\",\"password\":\"secret\"}")]
    [InlineData("/api/auth/refresh", "{}")]
    [InlineData("/api/auth/login", "{broken")]
    [InlineData("/api/auth/login", "null")]
    public async Task Invalid_request_returns_consistent_400(string path, string json)
    {
        using var client = factory.CreateApiClient();
        var response = await client.PostAsync(path, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.NotEmpty(body.GetProperty("errors").EnumerateArray());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Logout_requires_authentication()
    {
        using var client = factory.CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = "token" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Development_swagger_exposes_auth_contract_and_bearer_scheme()
    {
        await using var development = factory.WithWebHostBuilder(builder =>
            Microsoft.AspNetCore.Hosting.HostingAbstractionsWebHostBuilderExtensions.UseEnvironment(builder, "Development"));
        using var client = development.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var spec = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = spec.GetProperty("paths");
        foreach (var path in new[] { "/api/auth/login", "/api/auth/refresh", "/api/auth/logout", "/api/auth/me" })
            Assert.True(paths.TryGetProperty(path, out _));
        Assert.False(paths.TryGetProperty("/api/auth/register", out _));
        var scheme = spec.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        foreach (var path in new[] { "/api/auth/login", "/api/auth/refresh" })
        {
            var operation = paths.GetProperty(path).GetProperty("post");
            var security = operation.TryGetProperty("security", out var local) ? local :
                spec.TryGetProperty("security", out var inherited) ? inherited : default;
            Assert.True(security.ValueKind == JsonValueKind.Undefined || security.GetArrayLength() == 0,
                $"Anonymous endpoint {path} must not require a bearer token in OpenAPI.");
        }
        foreach (var (path, method) in new[] { ("/api/auth/logout", "post"), ("/api/auth/me", "get") })
        {
            var operation = paths.GetProperty(path).GetProperty(method);
            var security = operation.TryGetProperty("security", out var local) ? local : spec.GetProperty("security");
            Assert.Contains(security.EnumerateArray(), entry => entry.TryGetProperty("Bearer", out _));
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/swagger/index.html")).StatusCode);
    }
}

