using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FarmMonitoring.IntegrationTests;
[Collection("PostgreSQL Auth")]
public sealed class CropSupportPersistenceTests
{
    [Fact]
    public async Task Upgrade_keeps_legacy_observation_and_season_unknown_and_protects_new_history_on_downgrade()
    {
        await using var factory = new ApiFactory { InitialMigration = "20261104011126_AdminReviewedDossierCustomer" };
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO farms (name, is_active, created_at) VALUES ('MF04 legacy migration', true, now());
            INSERT INTO crop_condition_reports (farm_id, reporter_id, crop, description, reported_at, status, revision)
            SELECT f.id, u.id, 'Rice', 'Legacy symptoms', now(), 'RESOLVED', 1
            FROM farms f CROSS JOIN users u WHERE f.name = 'MF04 legacy migration' AND u.email = 'owner@example.com';
            """);
        await db.GetService<IMigrator>().MigrateAsync();
        var legacy = await db.CropConditionReports.SingleAsync();
        Assert.Null(legacy.CropSeasonId); Assert.Null(legacy.ObservedAt); Assert.Equal(CropConditionStatus.RESOLVED, legacy.Status);
        db.CropConsultationUpdates.Add(new() { ReportId = legacy.Id, UserId = legacy.ReporterId,
            Action = CropConsultationAction.REVIEW_REQUESTED, FollowUpType = "NOT_IMPROVED", Content = "Review legacy case", CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261104011126_AdminReviewedDossierCustomer"));
        Assert.Equal("NOT_IMPROVED", (await db.CropConsultationUpdates.AsNoTracking().SingleAsync()).FollowUpType);
    }
    [Fact]
    public async Task Crop_report_preserves_communication_and_image_ownership()
    {
        // Downgrades can commit newer migrations before an older guard rejects them.
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await db.Users.SingleAsync(x => x.Email == "owner@example.com");
        var farm = new Farm { Name = "Crop support evidence", CreatedAt = DateTimeOffset.UtcNow };
        db.Farms.Add(farm); await db.SaveChangesAsync();
        var report = new CropConditionReport { FarmId = farm.Id, ReporterId = owner.Id, Crop = "Tomato", Description = "Yellow leaves", ReportedAt = DateTimeOffset.UtcNow };
        report.Images.Add(new() { ContentType = "image/jpeg", Content = [255, 216, 255, 217] });
        report.Updates.Add(new() { UserId = owner.Id, Action = CropConsultationAction.OWNER_REPLIED, Content = "Observed after rain", CreatedAt = DateTimeOffset.UtcNow,
            Images = [new() { ContentType = "image/jpeg", Content = [255, 216, 255, 217] }] });
        db.CropConditionReports.Add(report); await db.SaveChangesAsync();
        var update = Assert.Single(report.Updates);
        Assert.Equal(report.Id, Assert.Single(report.Images).CropConditionReportId);
        Assert.Equal(update.Id, Assert.Single(update.Images).CropConsultationUpdateId);
        var invalid = new EvidenceImage { CropConditionReportId = report.Id, CropConsultationUpdateId = update.Id, ContentType = "image/jpeg", Content = [255, 216, 255, 217] };
        db.Add(invalid);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, ((PostgresException)error.InnerException!).SqlState);
        db.Entry(invalid).State = EntityState.Detached;
        report.Description = "Updated symptoms"; await db.SaveChangesAsync();
        Assert.Equal("Observed after rain", (await db.CropConsultationUpdates.AsNoTracking().SingleAsync(x => x.Id == update.Id)).Content);
        var deletion = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM crop_condition_reports WHERE id = {report.Id}"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, deletion.SqlState);
        Assert.Equal(1, await db.CropConsultationUpdates.CountAsync(x => x.ReportId == report.Id));
        await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261030123842_MaintenanceSchedules"));
        Assert.True(await db.CropConditionReports.AnyAsync(x => x.Id == report.Id));
    }
}
