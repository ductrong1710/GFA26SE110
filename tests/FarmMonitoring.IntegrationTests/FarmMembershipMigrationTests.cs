using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FarmMonitoring.IntegrationTests;

public sealed class FarmMembershipMigrationTests
{
    [Fact]
    public async Task Upgrade_adds_identity_foreign_keys_unique_indexes_and_no_automatic_assignments()
    {
        await using var factory = new ApiFactory { InitialMigration = "20261007041338_HumanUserRoles" };
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Farms.Add(new Farm { Name = "Existing farm", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var hashes = await db.Users.OrderBy(x => x.Id).Select(x => x.PasswordHash).ToArrayAsync();
        await db.GetService<IMigrator>().MigrateAsync();
        Assert.Empty(await db.UserFarms.ToArrayAsync());
        Assert.Equal(hashes, await db.Users.OrderBy(x => x.Id).Select(x => x.PasswordHash).ToArrayAsync());
        var columns = await db.Database.SqlQueryRaw<string>("SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name='user_farms' ORDER BY ordinal_position").ToArrayAsync();
        Assert.Equal(new[] { "id", "user_id", "farm_id", "created_at" }, columns);
        Assert.Equal("YES", await db.Database.SqlQueryRaw<string>("SELECT is_identity AS \"Value\" FROM information_schema.columns WHERE table_name='user_farms' AND column_name='id'").SingleAsync());
        Assert.Equal("timestamp with time zone", await db.Database.SqlQueryRaw<string>("SELECT data_type AS \"Value\" FROM information_schema.columns WHERE table_name='user_farms' AND column_name='created_at'").SingleAsync());
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM information_schema.table_constraints WHERE table_name='user_farms' AND constraint_type='FOREIGN KEY'").SingleAsync());
        var indexes = await db.Database.SqlQueryRaw<string>("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename='user_farms'").ToArrayAsync();
        Assert.Contains(indexes, x => x.Contains("UNIQUE") && x.Contains("(user_id, farm_id)"));
        Assert.Contains(indexes, x => x.Contains("(farm_id)"));
        var user = await db.Users.Where(x => x.Email == "owner@example.com").Select(x => x.Id).SingleAsync();
        var farm = await db.Farms.Select(x => x.Id).SingleAsync();
        db.UserFarms.Add(new UserFarm { UserId = user, FarmId = farm, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        db.UserFarms.Add(new UserFarm { UserId = user, FarmId = farm, CreatedAt = DateTimeOffset.UtcNow });
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
        db.ChangeTracker.Clear();
        db.UserFarms.Add(new UserFarm { UserId = int.MaxValue, FarmId = farm, CreatedAt = DateTimeOffset.UtcNow });
        var foreign = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(foreign.InnerException).SqlState);
        db.ChangeTracker.Clear();
        await db.GetService<IMigrator>().MigrateAsync();
        Assert.Single(await db.UserFarms.ToArrayAsync());
    }
}
