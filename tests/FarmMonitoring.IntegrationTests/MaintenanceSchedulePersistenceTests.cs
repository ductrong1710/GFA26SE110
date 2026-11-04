using FarmMonitoring.Domain.Entities;
using FarmMonitoring.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FarmMonitoring.IntegrationTests;
[Collection("PostgreSQL Auth")]
public sealed class MaintenanceSchedulePersistenceTests(ApiFactory factory)
{
    [Fact]
    public async Task Schedule_requires_scope_and_occurrence_preserves_unique_history()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = await db.Users.SingleAsync(x => x.Email == "admin@example.com");
        var now = DateTimeOffset.UtcNow;
        var schedule = new MaintenanceSchedule { Policy = "Periodic UAV inspection", IntervalDays = 30,
            NextDueAt = now, TechnicianId = admin.Id, CreatedById = admin.Id, UpdatedById = admin.Id, CreatedAt = now, UpdatedAt = now };
        db.MaintenanceSchedules.Add(schedule);
        var missing = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, ((PostgresException)missing.InnerException!).SqlState);
        schedule.Enabled = false; await db.SaveChangesAsync();
        var farm = new Farm { Name = "Maintenance history", CreatedAt = now };
        var uav = new Uav { Code = Guid.NewGuid().ToString("N"), Name = "Maintenance UAV", CreatedAt = now };
        var gateway = new Gateway { Code = Guid.NewGuid().ToString("N"), Name = "Maintenance gateway", GatewayType = "ESP32", CreatedAt = now };
        db.AddRange(farm, uav, gateway); await db.SaveChangesAsync();
        schedule.FarmId = farm.Id; schedule.Enabled = true; await db.SaveChangesAsync();
        await factory.AssignFarmAsync(farm.Id, "owner@example.com"); await factory.SeedRentalAsync(farm.Id, uav.Id, gateway.Id);
        var assignment = await db.RentalDeviceAssignments.SingleAsync(x => x.UavId == uav.Id);
        var job = new ServiceJob { RentalDeviceAssignmentId = assignment.Id, Type = ServiceJobType.MAINTENANCE, TechnicianId = admin.Id, ScheduledAt = now, CreatedAt = now };
        var otherJob = new ServiceJob { RentalDeviceAssignmentId = assignment.Id, Type = ServiceJobType.MAINTENANCE, TechnicianId = admin.Id, ScheduledAt = now, CreatedAt = now };
        db.ServiceJobs.AddRange(job, otherJob); await db.SaveChangesAsync();
        var occurrence = new MaintenanceOccurrence { ScheduleId = schedule.Id, DueAt = now, RentalDeviceAssignmentId = assignment.Id, JobId = job.Id, Policy = schedule.Policy, CreatedAt = now };
        db.MaintenanceOccurrences.Add(occurrence); await db.SaveChangesAsync();
        var duplicate = new MaintenanceOccurrence { ScheduleId = schedule.Id, DueAt = now, RentalDeviceAssignmentId = assignment.Id, JobId = otherJob.Id, Policy = schedule.Policy, CreatedAt = now };
        db.MaintenanceOccurrences.Add(duplicate);
        var conflict = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)conflict.InnerException!).SqlState);
        db.Entry(duplicate).State = EntityState.Detached;
        schedule.Policy = "Updated policy"; schedule.Enabled = false; await db.SaveChangesAsync();
        Assert.Equal("Periodic UAV inspection", (await db.MaintenanceOccurrences.AsNoTracking().SingleAsync(x => x.Id == occurrence.Id)).Policy);
        var deletion = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM maintenance_schedules WHERE id = {schedule.Id}"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, deletion.SqlState);
        Assert.True(await db.MaintenanceOccurrences.AnyAsync(x => x.Id == occurrence.Id));
    }
}
