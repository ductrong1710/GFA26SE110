using FarmMonitoring.Application.Interfaces;
using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;

[Collection("PostgreSQL Auth")]
public class AdminSeedTests
{
    [Fact]
    public async Task Seed_creates_hashed_admin_once_and_does_not_reset_password()
    {
        await using var factory = new ApiFactory { SeedAccounts = false };
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = scope.ServiceProvider.GetRequiredService<DevelopmentAdminSeeder>();
        await seed.SeedAsync("  FIRST@EXAMPLE.COM ", "First-test-password!", default);
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleAsync();
        Assert.Equal("first@example.com", user.Email);
        Assert.True(scope.ServiceProvider.GetRequiredService<IPasswordService>().Verify(user, "First-test-password!").Succeeded);
        Assert.Equal("FarmAdministrator", Assert.Single(user.UserRoles).Role.Name);
        var hash = user.PasswordHash;
        await seed.SeedAsync("first@example.com", "A-different-password!", default);
        await seed.SeedAsync("another@example.com", "A-different-password!", default);
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(hash, (await db.Users.SingleAsync()).PasswordHash);
    }

    [Fact]
    public async Task Seed_does_not_promote_existing_account_and_missing_credentials_create_nothing()
    {
        await using var factory = new ApiFactory { SeedAccounts = false };
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = scope.ServiceProvider.GetRequiredService<DevelopmentAdminSeeder>();
        await seed.SeedAsync(null, null, default);
        Assert.Empty(await db.Users.ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => seed.SeedAsync("first@example.com", null, default));
        var user = new User { Email = "existing@example.com", FullName = "Existing", CreatedAt = DateTimeOffset.UtcNow };
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(user, "Existing-test-password!");
        user.UserRoles.Add(new UserRole { RoleId = 3 });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => seed.SeedAsync(user.Email, "A-different-password!", default));
        Assert.False(await db.UserRoles.AnyAsync(x => x.RoleId == 1));
    }
}
