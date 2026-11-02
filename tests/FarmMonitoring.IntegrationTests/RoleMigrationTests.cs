using System.Net;
using System.Net.Http.Json;
using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class RoleMigrationTests
{
    [Fact]
    public async Task Operational_role_upgrade_preserves_history_without_granting_technician_privileges()
    {
        const string previous = "20261028022354_ArchiveUnresolvedSensorRecords";
        await using var factory = new ApiFactory { InitialMigration = previous };
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await db.Users.SingleAsync(x => x.Email == "owner@example.com");
        var hash = owner.PasswordHash;
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM user_roles WHERE user_id = {owner.Id}");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO user_roles (user_id, role_id) VALUES ({owner.Id}, 5)");
        var farm = new Farm { Name = "Historical membership", CreatedAt = DateTimeOffset.UtcNow };
        db.Farms.Add(farm);
        await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id, owner.Email);

        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync();
        Assert.True(await db.UserRoles.AnyAsync(x => x.UserId == owner.Id && x.RoleId == 5));
        Assert.True(await db.UserFarms.AnyAsync(x => x.UserId == owner.Id && x.FarmId == farm.Id));
        Assert.False(await db.UserRoles.AnyAsync(x => x.UserId == owner.Id && x.Role.Name == "Technician"));
        Assert.Equal(hash, await db.Users.Where(x => x.Id == owner.Id).Select(x => x.PasswordHash).SingleAsync());
        using var client = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
            new { email = owner.Email, password = "Test-password-123!" })).StatusCode);

        // An explicit assignment survives rollback/re-upgrade without deleting history.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO user_roles (user_id, role_id) VALUES ({owner.Id}, 6)");
        await migrator.MigrateAsync(previous);
        await migrator.MigrateAsync();
        Assert.Equal(new[] { 5, 6 }, await db.UserRoles.Where(x => x.UserId == owner.Id).OrderBy(x => x.RoleId).Select(x => x.RoleId).ToArrayAsync());
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = owner.Email, password = "Test-password-123!" });
        login.EnsureSuccessStatusCode();
        var payload = (await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("data");
        Assert.Equal("Technician", Assert.Single(payload.GetProperty("user").GetProperty("roles").EnumerateArray()).GetString());
        client.DefaultRequestHeaders.Authorization = new("Bearer", payload.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/farms/{farm.Id}")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Upgrade_removes_legacy_memberships_without_converting_or_resetting_accounts(bool targetRolesAlreadyExist)
    {
        await using var factory = new ApiFactory { SeedAccounts = false, InitialMigration = "20260927023840_MissionStatusIndex" };
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var legacyId = 2;
        if (targetRolesAlreadyExist)
        {
            // The old role need not retain its original ID; target roles can already exist.
            await db.Database.ExecuteSqlRawAsync("DELETE FROM roles WHERE name = 'UavDeviceOperator'; INSERT INTO roles(id,name) VALUES (7,'UavDeviceOperator'),(2,'FarmOwner'),(3,'FarmEngineer');");
            legacyId = 7;
        }
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        User Account(string email, params int[] roles)
        {
            var user = new User { Email = email, FullName = "Preserved account", CreatedAt = DateTimeOffset.UtcNow };
            user.PasswordHash = passwords.Hash(user, "Migration-password-123!");
            foreach (var role in roles) user.UserRoles.Add(new UserRole { RoleId = role });
            return user;
        }
        var legacy = Account("legacy@example.com", legacyId);
        var mixed = Account("mixed@example.com", 1, legacyId);
        User? owner = null;
        if (targetRolesAlreadyExist) owner = Account("existing-owner@example.com", 2);
        foreach (var user in new[] { legacy, mixed, owner }.OfType<User>())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO users (email, full_name, password_hash, is_active, created_at) VALUES ({user.Email}, {user.FullName}, {user.PasswordHash}, true, {user.CreatedAt})");
            user.Id = await db.Users.Where(x => x.Email == user.Email).Select(x => x.Id).SingleAsync();
            foreach (var role in user.UserRoles)
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO user_roles (user_id, role_id) VALUES ({user.Id}, {role.RoleId})");
        }
        var savedHash = legacy.PasswordHash;
        var refresh = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateRefreshToken(DateTimeOffset.UtcNow);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO refresh_tokens (user_id, token_hash, created_at, expires_at) VALUES ({legacy.Id}, {refresh.Hash}, {DateTimeOffset.UtcNow}, {refresh.ExpiresAt})");
        db.ChangeTracker.Clear();
        await db.GetService<IMigrator>().MigrateAsync("20261007041338_HumanUserRoles");
        Assert.Equal(new[] { "FarmAdministrator", "FarmEngineer", "FarmOwner" }, await db.Roles.OrderBy(x => x.Name).Select(x => x.Name).ToArrayAsync());
        var persisted = await db.Users.Where(x => x.Id == legacy.Id).Select(x => new {x.IsActive,x.PasswordHash}).SingleAsync();
        Assert.True(persisted.IsActive);
        Assert.Equal(savedHash, persisted.PasswordHash);
        Assert.False(await db.UserRoles.AnyAsync(x => x.UserId == legacy.Id));
        Assert.Equal("FarmAdministrator", Assert.Single(await db.UserRoles.Where(x => x.UserId == mixed.Id).Select(x => x.Role.Name).ToArrayAsync()));
        if (owner is not null) Assert.Equal("FarmOwner", Assert.Single(await db.UserRoles.Where(x => x.UserId == owner.Id).Select(x => x.Role.Name).ToArrayAsync()));
        Assert.Equal(targetRolesAlreadyExist ? 3 : 2, await db.Users.CountAsync());
        // Runtime authentication runs against the current schema, after checking the historical upgrade.
        await db.GetService<IMigrator>().MigrateAsync();
        using var client = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = legacy.Email, password = "Migration-password-123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh.Token })).StatusCode);
        (await client.PostAsJsonAsync("/api/auth/login", new { email = mixed.Email, password = "Migration-password-123!" })).EnsureSuccessStatusCode();
        Assert.Equal(1, await db.RefreshTokens.CountAsync(x => x.UserId == legacy.Id));
        await db.GetService<IMigrator>().MigrateAsync("20261007041338_HumanUserRoles");
        Assert.Equal(3, await db.Roles.CountAsync());
    }
}
