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
public class SampleAdminMigrationTests
{
    private const string PreviousMigration = "20261007094620_AddUserFarmAssignments";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Migration_creates_active_admin_with_working_hashed_password_and_does_not_reset_it(bool upgrade)
    {
        await using var factory = new ApiFactory { SeedAccounts = false, InitialMigration = upgrade ? PreviousMigration : null };
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (upgrade) await db.GetService<IMigrator>().MigrateAsync();
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Email == "admin@sep.local");
        Assert.NotNull(user);
        Assert.True(user.IsActive);
        Assert.NotEqual("123456", user.PasswordHash);
        Assert.Equal("FarmAdministrator", Assert.Single(user.UserRoles).Role.Name);
        using var client = factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@sep.local", password = "123456" })).StatusCode);

        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        user.PasswordHash = passwords.Hash(user, "Changed-password-123!");
        await db.SaveChangesAsync();
        var hash = user.PasswordHash;
        // Rollback preserves an account that may now own data; reapply must not reset it.
        await db.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        await db.GetService<IMigrator>().MigrateAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(hash, (await db.Users.SingleAsync(x => x.Email == user.Email)).PasswordHash);
        Assert.Equal(1, await db.Users.CountAsync(x => x.Email == user.Email));
    }

    [Fact]
    public async Task Upgrade_does_not_promote_reactivate_or_reset_an_existing_email()
    {
        await using var factory = new ApiFactory { SeedAccounts = false, InitialMigration = PreviousMigration };
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User { Email = "admin@sep.local", FullName = "Existing owner", IsActive = false, CreatedAt = DateTimeOffset.UtcNow };
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(user, "Existing-password-123!");
        user.UserRoles.Add(new UserRole { RoleId = 3 });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var hash = user.PasswordHash;
        await db.GetService<IMigrator>().MigrateAsync();
        db.ChangeTracker.Clear();
        var existing = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleAsync(x => x.Email == user.Email);
        Assert.False(existing.IsActive);
        Assert.Equal(hash, existing.PasswordHash);
        Assert.Equal("Existing owner", existing.FullName);
        Assert.Equal("FarmOwner", Assert.Single(existing.UserRoles).Role.Name);
    }
}
