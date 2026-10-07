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
        db.Users.AddRange(legacy, mixed);
        User? owner = null;
        if (targetRolesAlreadyExist) { owner = Account("existing-owner@example.com", 2); db.Users.Add(owner); }
        await db.SaveChangesAsync();
        var savedHash = legacy.PasswordHash;
        var refresh = scope.ServiceProvider.GetRequiredService<ITokenService>().CreateRefreshToken(DateTimeOffset.UtcNow);
        db.RefreshTokens.Add(new RefreshToken { UserId = legacy.Id, TokenHash = refresh.Hash, CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = refresh.ExpiresAt });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await db.GetService<IMigrator>().MigrateAsync();
        Assert.Equal(new[] { "FarmAdministrator", "FarmEngineer", "FarmOwner" }, await db.Roles.OrderBy(x => x.Name).Select(x => x.Name).ToArrayAsync());
        var persisted = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleAsync(x => x.Id == legacy.Id);
        Assert.True(persisted.IsActive);
        Assert.Equal(savedHash, persisted.PasswordHash);
        Assert.Empty(persisted.UserRoles);
        Assert.Equal("FarmAdministrator", Assert.Single(await db.UserRoles.Where(x => x.UserId == mixed.Id).Select(x => x.Role.Name).ToArrayAsync()));
        if (owner is not null) Assert.Equal("FarmOwner", Assert.Single(await db.UserRoles.Where(x => x.UserId == owner.Id).Select(x => x.Role.Name).ToArrayAsync()));
        Assert.Equal(targetRolesAlreadyExist ? 3 : 2, await db.Users.CountAsync());
        using var client = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = legacy.Email, password = "Migration-password-123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh.Token })).StatusCode);
        (await client.PostAsJsonAsync("/api/auth/login", new { email = mixed.Email, password = "Migration-password-123!" })).EnsureSuccessStatusCode();
        Assert.Equal(1, await db.RefreshTokens.CountAsync(x => x.UserId == legacy.Id));
        await db.GetService<IMigrator>().MigrateAsync();
        Assert.Equal(3, await db.Roles.CountAsync());
    }
}
