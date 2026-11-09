using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FarmMonitoring.IntegrationTests;
[Collection("PostgreSQL Auth")]
public class ServiceJobMigrationTests
{
    [Fact]
    public async Task Upgrade_backfills_existing_jobs_and_downgrade_refuses_to_discard_independent_work()
    {
        const string previous = "20261030070236_ServiceDeviceReplacements";
        await using var factory = new ApiFactory { InitialMigration = previous };
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var farm = new Farm { Name = "Historical service job", CreatedAt = DateTimeOffset.UtcNow };
        var uav = new Uav { Code = Guid.NewGuid().ToString("N"), Name = "Historical UAV", CreatedAt = DateTimeOffset.UtcNow };
        var gateway = new Gateway { Code = Guid.NewGuid().ToString("N"), Name = "Historical gateway", GatewayType = "ESP32", CreatedAt = DateTimeOffset.UtcNow };
        db.Farms.Add(farm); db.Uavs.Add(uav); db.Gateways.Add(gateway); await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id, "owner@example.com"); await factory.SeedRentalAsync(farm.Id, uav.Id, gateway.Id);
        var assignment = await db.RentalDeviceAssignments.SingleAsync(x => x.UavId == uav.Id);
        var admin = await db.Users.SingleAsync(x => x.Email == "admin@example.com");
        var incident = new DeviceIncident { FarmId = farm.Id, RentalDeviceAssignmentId = assignment.Id, DeviceCode = assignment.Code, ReporterId = admin.Id, Description = "Historical fault", ReportedAt = DateTimeOffset.UtcNow };
        db.DeviceIncidents.Add(incident); await db.SaveChangesAsync();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO service_jobs (incident_id,type,technician_id,scheduled_at,status,revision,created_at) VALUES ({incident.Id}, 'REPAIR', {admin.Id}, {now}, 'IN_PROGRESS', 7, {now})");
        var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync();
        var job = await db.ServiceJobs.SingleAsync(x => x.IncidentId == incident.Id);
        Assert.Equal(assignment.Id, job.RentalDeviceAssignmentId); Assert.Equal(7, job.Revision); Assert.Equal(ServiceJobStatus.IN_PROGRESS, job.Status);
        db.ServiceJobs.Add(new ServiceJob { RentalDeviceAssignmentId = assignment.Id, Type = ServiceJobType.MAINTENANCE, TechnicianId = admin.Id, ScheduledAt = now, CreatedAt = now });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous));
        Assert.Equal(2, await db.ServiceJobs.CountAsync());
        Assert.True(await db.ServiceJobs.AnyAsync(x => x.IncidentId == null && x.RentalDeviceAssignmentId == assignment.Id));
    }
}
