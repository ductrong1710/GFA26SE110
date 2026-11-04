using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace FarmMonitoring.IntegrationTests;

public sealed class MaintenanceFarmMigrationTests
{
    [Fact]
    public async Task Upgrade_preserves_legacy_configuration_but_disables_it_without_guessing_a_farm()
    {
        await using var factory = new ApiFactory { InitialMigration = "20261103153523_ExternalConsultationDossier" };
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var actor = await db.Users.Where(x => x.Email == "admin@example.com").Select(x => x.Id).SingleAsync();
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO maintenance_schedules (device_type,policy,interval_days,next_due_at,technician_id,enabled,revision,created_by_id,updated_by_id,created_at,updated_at) VALUES ('UAV','Legacy policy',30,{now},{actor},TRUE,1,{actor},{actor},{now},{now})");
        await db.GetService<IMigrator>().MigrateAsync();
        var schedule = await db.MaintenanceSchedules.SingleAsync();
        Assert.False(schedule.Enabled);
        Assert.Null(schedule.FarmId);
        Assert.Equal("UAV", schedule.DeviceType);
        Assert.Equal("Legacy policy", schedule.Policy);
        Assert.Equal(2, schedule.Revision);
        Assert.Equal(now, schedule.NextDueAt);
    }
}
