using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class ExceptionResponseTests(ApiFactory factory)
{
    [Fact]
    public async Task Infrastructure_failure_returns_generic_error_without_internal_details()
    {
        await using var failing = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthRepository>();
            services.AddScoped<IAuthRepository, FailingRepository>();
        }));
        using var client = failing.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "test" });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<JsonElement>(content);
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("internal-database-detail", content);
        Assert.False(body.TryGetProperty("stackTrace", out _));
    }

    private sealed class FailingRepository : IAuthRepository
    {
        public Task<User?> GetUserByEmailAsync(string email, CancellationToken ct) => throw new InvalidOperationException("internal-database-detail");
        public Task<User?> GetUserByIdAsync(int id, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveLoginAsync(RefreshToken token, CancellationToken ct) => throw new NotSupportedException();
        public Task<User?> RotateAsync(string hash, RefreshToken replacement, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> RevokeAsync(int id, string hash, CancellationToken ct) => throw new NotSupportedException();
    }
}
