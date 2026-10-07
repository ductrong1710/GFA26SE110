using System.Security.Cryptography;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FarmMonitoring.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string databaseName = "farm_auth_tests_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
    private readonly string adminConnection = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")
        ?? throw new InvalidOperationException("Set TEST_POSTGRES_CONNECTION to an isolated PostgreSQL server (database creation permission required), or use scripts/Test-Postgres.ps1.");
    public string SigningKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    public string DeviceKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    public bool SeedAccounts { get; init; } = true;
    public string? InitialMigration { get; init; }
    private bool databaseCreated;
    public string ConnectionString => new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = ConnectionString,
            ["Jwt:Issuer"] = "FarmMonitoring.Tests", ["Jwt:Audience"] = "FarmMonitoring.Tests.Client",
            ["Jwt:SecretKey"] = SigningKey,
            ["DeviceAuthentication:Credentials:0:GatewayCode"] = "integration-gateway",
            ["DeviceAuthentication:Credentials:0:KeyHash"] = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(DeviceKey))),
            ["DeviceAuthentication:Credentials:1:GatewayCode"] = "sync-gateway",
            ["DeviceAuthentication:Credentials:1:KeyHash"] = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(DeviceKey))),
            ["DeviceAuthentication:Credentials:2:GatewayCode"] = "sync-gateway-2",
            ["DeviceAuthentication:Credentials:2:KeyHash"] = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(DeviceKey))),
            ["DeviceAuthentication:Credentials:3:GatewayCode"] = "alerts-gateway",
            ["DeviceAuthentication:Credentials:3:KeyHash"] = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(DeviceKey))),
            ["DeviceAuthentication:Credentials:4:GatewayCode"] = "role-matrix-gateway",
            ["DeviceAuthentication:Credentials:4:KeyHash"] = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(DeviceKey))),
            ["SEED_ADMIN_EMAIL"] = "", ["SEED_ADMIN_PASSWORD"] = "",
            ["Logging:LogLevel:Default"] = "Warning",
            ["Monitoring:Enabled"] = "false"
        }));
        builder.ConfigureServices(services => services.AddControllers().AddApplicationPart(typeof(RoleProbeController).Assembly));
    }

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        // Identifier is generated only from a fixed prefix plus random hexadecimal bytes.
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
        databaseCreated = true;
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.GetService<IMigrator>().MigrateAsync(InitialMigration);
        if (!SeedAccounts) return;
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        foreach (var (email, active, roleId) in new[] { ("admin@example.com", true, 1), ("disabled@example.com", false, 1), ("owner@example.com", true, 3), ("engineer@example.com", true, 4) })
        {
            var user = new User { Email = email, FullName = "Test Account", IsActive = active, CreatedAt = DateTimeOffset.UtcNow };
            user.PasswordHash = passwords.Hash(user, "Test-password-123!");
            user.UserRoles.Add(new UserRole { RoleId = roleId });
            db.Users.Add(user);
        }
        await db.SaveChangesAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (!databaseCreated) return;
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
        databaseCreated = false;
    }

    public async Task AssignFarmAsync(int farmId, params string[] emails)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = await db.Users.Where(x => emails.Contains(x.Email)).Select(x => x.Id).ToArrayAsync();
        Assert.Equal(emails.Length, ids.Length);
        foreach (var id in ids) db.UserFarms.Add(new UserFarm { FarmId = farmId, UserId = id, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    // Direct application integrity tests supply an actor, but still use real DB role/membership checks.
    public static T HumanService<T>(IServiceProvider services, int userId) where T : class
    {
        var access = new FarmMonitoring.Application.Features.Farms.FarmAccessService(
            services.GetRequiredService<IFarmAccessRepository>(), services.GetRequiredService<IAuthRepository>(), new TestUser(userId));
        return ActivatorUtilities.CreateInstance<T>(services, access);
    }
    private sealed record TestUser(int Id) : ICurrentUser { public int? UserId => Id; }

    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
    });
}

[CollectionDefinition("PostgreSQL Auth")]
public sealed class AuthCollection : ICollectionFixture<ApiFactory>;
