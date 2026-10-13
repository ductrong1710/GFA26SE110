using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FarmMonitoring.IntegrationTests;

public sealed class CorsTests
{
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("OPTIONS")]
    public async Task Configured_origin_can_preflight_protected_endpoint_without_authentication(string method)
    {
        await using var factory = CreateFactory("http://localhost:5173");
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var request = Preflight("http://localhost:5173", method);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains(method, string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods")));
        var headers = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")).ToLowerInvariant();
        Assert.Contains("authorization", headers);
        Assert.Contains("content-type", headers);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Theory]
    [InlineData("https://untrusted.example", "https://farm.example")]
    [InlineData("http://localhost:5173", null)]
    public async Task Unlisted_origin_does_not_receive_cors_permission(string origin, string? configuredOrigin)
    {
        await using var factory = CreateFactory(configuredOrigin);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var request = Preflight(origin, "GET");
        using var response = await client.SendAsync(request);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Configured_production_origin_receives_cors_headers_on_unauthorized_response()
    {
        await using var factory = CreateFactory("https://farm.example");
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/farms");
        request.Headers.Add("Origin", "https://farm.example");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("https://farm.example", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    private static HttpRequestMessage Preflight(string origin, string method)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/farms");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return request;
    }

    // Preflight and unauthenticated requests must finish without querying a database.
    private static WebApplicationFactory<Program> CreateFactory(string? origin) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=cors_unused",
                ["Jwt:Issuer"] = "Cors.Tests", ["Jwt:Audience"] = "Cors.Tests.Client",
                ["Jwt:SecretKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
                ["Cors:AllowedOrigins:0"] = origin,
                ["Monitoring:Enabled"] = "false", ["Email:WorkerEnabled"] = "false"
            }));
        });
}
