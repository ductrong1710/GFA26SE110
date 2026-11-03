using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FarmMonitoring.IntegrationTests;
[Collection("PostgreSQL Auth")]
public sealed class CropSeasonPersistenceTests
{
    [Fact]
    public async Task Season_retains_unknowns_activity_evidence_and_restricts_destructive_delete()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await db.Users.SingleAsync(x => x.Email == "owner@example.com");
        var farm = new Farm { Name = "Season persistence", CreatedAt = DateTimeOffset.UtcNow };
        db.Farms.Add(farm); await db.SaveChangesAsync();
        var season = new CropSeason { FarmId = farm.Id, OwnerId = owner.Id, Name = "Winter", Crop = "Rice", CreatedAt = DateTimeOffset.UtcNow };
        season.Activities.Add(new CultivationActivity { UserId = owner.Id, Date = new(2026, 11, 2), Type = CultivationActivityType.INSPECTION,
            Description = "Checked leaves", CostVnd = 0, CreatedAt = DateTimeOffset.UtcNow,
            Images = [new() { ContentType = "image/jpeg", Content = [255, 216, 255, 217] }] });
        db.CropSeasons.Add(season); await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var stored = await db.CropSeasons.Include(x => x.Activities).ThenInclude(x => x.Images).SingleAsync(x => x.Id == season.Id);
        Assert.Null(stored.StartDate); Assert.Null(stored.EndDate); Assert.Null(stored.AreaHectares); Assert.Null(stored.CropAgeDays);
        Assert.Null(stored.YieldKg); Assert.Null(stored.TotalCostVnd); Assert.Null(stored.RevenueVnd);
        var activity = Assert.Single(stored.Activities); Assert.Equal(0m, activity.CostVnd);
        Assert.Equal(activity.Id, Assert.Single(activity.Images).CultivationActivityId);
        var deletion = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM crop_seasons WHERE id = {season.Id}"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, deletion.SqlState);
        var invalid = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE crop_seasons SET total_cost_vnd = -1 WHERE id = {season.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalid.SqlState);
        stored.Status = CropSeasonStatus.COMPLETED; stored.CompletedAt = DateTimeOffset.UtcNow;
        stored.YieldKg = 0; stored.RevenueVnd = 0; await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var completed = await db.CropSeasons.SingleAsync(x => x.Id == season.Id);
        Assert.Null(completed.EndDate); Assert.Null(completed.TotalCostVnd); Assert.Equal(0m, completed.YieldKg); Assert.Equal(0m, completed.RevenueVnd);
        Assert.Equal(1, await db.CultivationActivities.CountAsync(x => x.SeasonId == season.Id));
        await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261101100505_CropConditionSupport"));
        Assert.True(await db.CropSeasons.AnyAsync(x => x.Id == season.Id));
    }
}
